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

    [Header("Modelos de personaje")]
    public GameObject[] previewModels;   // 5 modelos de preview en la pantalla de select
    public Button modelLeftButton;
    public Button modelRightButton;

    const string KEY_MODEL = "model";
    const string PREF_MODEL = "preferred_model";
    int currentModelIndex = 0;

    [Header("Ocultar arma en preview")]
    public string weaponTag = "Weapon";   // tag del arma a esconder en el select

    const string KEY_COLOR = "color";
    const string KEY_READY = "ready";
    const string PREF_COLOR = "preferred_color"; 

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

        if (modelLeftButton != null) modelLeftButton.onClick.AddListener(() => ChangeModel(-1));
        if (modelRightButton != null) modelRightButton.onClick.AddListener(() => ChangeModel(1));

        // Restaurar el modelo preferido de la sesión anterior
        SelectInitialModel();

        OcultarArmasEnPreview();
    }

    void ChangeModel(int dir)
    {
        if (confirmed) return;
        int len = previewModels.Length;
        if (len == 0) return;

        // Buscamos el proximo modelo LIBRE en esa direccion
        int next = currentModelIndex;
        for (int i = 0; i < len; i++)
        {
            next = (next + dir + len) % len;
            if (!IsModelTaken(next)) { SetModel(next); return; }
        }
    }

    // Un modelo esta ocupado si otro jugador ya lo eligio
    bool IsModelTaken(int index)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.IsLocal) continue;
            if (p.CustomProperties.TryGetValue(KEY_MODEL, out var m) && (int)m == index)
                return true;
        }
        return false;
    }

    // Elige un modelo libre al entrar (preferido guardado, o el primero disponible)
    void SelectInitialModel()
    {
        int len = previewModels.Length;
        if (len == 0) return;

        // 1) modelo preferido guardado, si esta libre
        int preferred = PlayerPrefs.GetInt(PREF_MODEL, -1);
        if (preferred >= 0 && preferred < len && !IsModelTaken(preferred))
        {
            SetModel(preferred);
            return;
        }

        // 2) buscar el primero libre empezando por el ActorNumber
        int start = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % len;
        if (start < 0) start = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = (start + i) % len;
            if (!IsModelTaken(idx)) { SetModel(idx); return; }
        }

        // 3) todos ocupados (no deberia pasar con modelos >= jugadores)
        SetModel(start);
    }

    // Si dos eligieron el mismo modelo casi a la vez, el de ActorNumber mayor cede
    void ResolveModelCollision()
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p.IsLocal) continue;
            if (p.CustomProperties.TryGetValue(KEY_MODEL, out var m) && (int)m == currentModelIndex
                && p.ActorNumber < PhotonNetwork.LocalPlayer.ActorNumber)
            {
                ChangeModel(1); // yo cedo, busco otro libre
                return;
            }
        }
    }

    // Oculta cualquier hijo con el tag del arma en los modelos de preview
    void OcultarArmasEnPreview()
    {
        if (previewModels == null || previewModels.Length == 0)
        {
            Debug.LogWarning("[Select] previewModels VACIO: asignalos en el Inspector.");
            return;
        }

        int total = 0;
        foreach (GameObject model in previewModels)
        {
            if (model == null) continue;

            // includeInactive = true: encuentra el arma aunque el modelo este apagado
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.CompareTag(weaponTag))
                {
                    t.gameObject.SetActive(false);
                    total++;
                    Debug.Log($"[Select] Arma ocultada en '{model.name}' -> '{t.name}'");
                }
            }
        }

        Debug.Log($"[Select] Total armas ocultadas: {total} | previewModels: {previewModels.Length} | tag: '{weaponTag}'");
    }

    void SetModel(int index)
    {
        currentModelIndex = index;

        // Preview: activamos solo el modelo elegido
        for (int i = 0; i < previewModels.Length; i++)
            if (previewModels[i] != null)
                previewModels[i].SetActive(i == index);

        // (Opcional) si querés seguir tiñendo con el color elegido, apuntamos
        // el renderer de preview al modelo activo y reaplicamos el color:
        // El color ya NO tine el modelo de preview: se usa solo para los nombres.

        // Guardamos la elección: se sincroniza a todos y persiste entre escenas
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { KEY_MODEL, index } });

        PlayerPrefs.SetInt(PREF_MODEL, index);
        PlayerPrefs.Save();
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

    // Arranca en un color distinto por jugador 
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

        // El color ya NO tine el modelo: solo guardamos la eleccion para los nombres.
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { KEY_COLOR, index } });

        PlayerPrefs.SetInt(PREF_COLOR, index); // NUEVO: recordar la preferencia
        PlayerPrefs.Save();
    }

    void Confirm()
    {
        if (currentIndex < 0 || confirmed) return;

        // anti duplicados
        string miNombre = PhotonNetwork.NickName;
        if (string.IsNullOrWhiteSpace(miNombre)) miNombre = "Agente";

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            
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
        if (modelLeftButton != null) modelLeftButton.interactable = false;
        if (modelRightButton != null) modelRightButton.interactable = false;
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

        // colision de MODELO: dos eligieron el mismo personaje casi a la vez
        if (changedProps.ContainsKey(KEY_MODEL) && !confirmed
            && IsModelTaken(currentModelIndex))
        {
            ResolveModelCollision();
        }

        UpdateStatus();

        if (changedProps.ContainsKey(KEY_READY))
            CheckAllReady();
    }

    public override void OnPlayerLeftRoom(Player p) { UpdateStatus(); CheckAllReady(); }
    public override void OnMasterClientSwitched(Player p) => CheckAllReady();

    
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