using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelector : MonoBehaviourPunCallbacks
{
    [Header("Preview")]
    public Renderer characterRenderer;   // material del modelo 3D de preview

    [Header("UI")]
    public Button leftButton;
    public Button rightButton;
    public Button confirmButton;
    public TMP_Text statusText;          // "2/4 listos"

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Gameplay";

    [Header("Input Nickname")]
    public TMP_InputField nameInputField; // Arrastrá tu InputField de la UI acá

    const string KEY_COLOR = "color";
    const string KEY_READY = "ready";
    const string PREF_COLOR = "preferred_color"; // NUEVO: clave de PlayerPrefs

    int currentIndex = -1;
    bool confirmed = false;

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true; // defensivo

        leftButton.onClick.AddListener(() => ChangeColor(-1));
        rightButton.onClick.AddListener(() => ChangeColor(1));
        confirmButton.onClick.AddListener(Confirm);

        // limpiar "listo" de una sesión anterior y elegir color inicial
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { KEY_READY, false } });
        SelectInitialColor();
        UpdateStatus();

        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<Renderer>();
        // Cargamos el nombre guardado (si existe) y actualizamos la red
        if (nameInputField != null)
        {
            string savedName = PlayerPrefs.GetString("nickname", "Agente Desconocido");
            nameInputField.text = savedName;
            PhotonNetwork.NickName = savedName;

            // Cada vez que tipeás una letra, se guarda automáticamente
            nameInputField.onValueChanged.AddListener((val) => {
                PhotonNetwork.NickName = val;
                PlayerPrefs.SetString("nickname", val);
            });
        }
    }

    bool IsTaken(int index)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.IsLocal) continue;
            if (p.CustomProperties.TryGetValue(KEY_COLOR, out var c) && (int)c == index)
                return true;
        }
        return false;
    }

    // Arranca en un color distinto por jugador (basado en ActorNumber) → casi sin colisiones
    void SelectInitialColor()
    {
        int len = GameColors.Palette.Length;

        // 1) intentar el color preferido guardado de la sesión anterior
        int preferred = PlayerPrefs.GetInt(PREF_COLOR, -1);
        if (preferred >= 0 && preferred < len && !IsTaken(preferred))
        {
            SetColor(preferred);
            return;
        }

        // 2) si no hay preferencia o está ocupado, caer al esquema por ActorNumber
        int start = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % len;
        if (start < 0) start = 0;

        for (int i = 0; i < len; i++)
        {
            int idx = (start + i) % len;
            if (!IsTaken(idx)) { SetColor(idx); return; }
        }
    }

    void ChangeColor(int dir)
    {
        if (confirmed) return;
        int len = GameColors.Palette.Length;
        int next = currentIndex;
        for (int i = 0; i < len; i++)
        {
            next = (next + dir + len) % len;
            if (!IsTaken(next)) { SetColor(next); return; }
        }
    }

    void SetColor(int index)
    {
        currentIndex = index;
        if (characterRenderer != null)
            characterRenderer.material.color = GameColors.Palette[index];

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { KEY_COLOR, index } });

        PlayerPrefs.SetInt(PREF_COLOR, index); // NUEVO: recordar la preferencia
        PlayerPrefs.Save();
    }

    void Confirm()
    {
        if (currentIndex < 0 || confirmed) return;

        // --- SISTEMA ANTI-DUPLICADOS ---
        string miNombre = PhotonNetwork.NickName;
        if (string.IsNullOrWhiteSpace(miNombre)) miNombre = "Agente";

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            // Si el jugador no soy yo, y tiene mi mismo nombre...
            if (!p.IsLocal && p.NickName == miNombre)
            {
                // Le agregamos nuestro número de Actor para evitar el clon
                miNombre = miNombre + "-" + PhotonNetwork.LocalPlayer.ActorNumber;
                PhotonNetwork.NickName = miNombre;
                PlayerPrefs.SetString("nickname", miNombre); // Actualizamos el guardado

                if (nameInputField != null) nameInputField.text = miNombre; // Actualizamos la UI
                break;
            }
        }
        // -------------------------------

        confirmed = true;
        leftButton.interactable = false;
        rightButton.interactable = false;
        confirmButton.interactable = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { KEY_READY, true } });
        CheckAllReady();
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
    {
        // colisión: dos eligieron el mismo color casi al mismo tiempo
        if (changedProps.ContainsKey(KEY_COLOR) && !confirmed
            && currentIndex >= 0 && IsTaken(currentIndex))
        {
            ResolveCollision();
        }

        UpdateStatus();

        if (changedProps.ContainsKey(KEY_READY))
            CheckAllReady();
    }

    public override void OnPlayerLeftRoom(Player p) { UpdateStatus(); CheckAllReady(); }
    public override void OnMasterClientSwitched(Player p) => CheckAllReady();

    // El de menor ActorNumber se queda el color, el otro cede. Determinístico en ambos clientes.
    void ResolveCollision()
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.IsLocal) continue;
            if (p.CustomProperties.TryGetValue(KEY_COLOR, out var c) && (int)c == currentIndex
                && p.ActorNumber < PhotonNetwork.LocalPlayer.ActorNumber)
            {
                ChangeColor(1); // yo cedo
                return;
            }
        }
    }

    void UpdateStatus()
    {
        int ready = 0;
        foreach (var p in PhotonNetwork.PlayerList)
            if (p.CustomProperties.TryGetValue(KEY_READY, out var r) && (bool)r) ready++;

        if (statusText != null)
            statusText.text = $"{ready}/{PhotonNetwork.PlayerList.Length} listos";
    }

    // Solo el master decide arrancar
    void CheckAllReady()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.PlayerList.Length == 0) return;

        foreach (var p in PhotonNetwork.PlayerList)
            if (!p.CustomProperties.TryGetValue(KEY_READY, out var r) || !(bool)r)
                return; // falta alguien

        PhotonNetwork.LoadLevel(gameSceneName);
    }
}