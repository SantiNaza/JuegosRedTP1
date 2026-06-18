using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    [System.Serializable]
    public struct PlayerUISlot
    {
        public GameObject contenedorSlot;
        public TextMeshProUGUI textoNombre;
        public Slider barraVida;
        public TextMeshProUGUI textoCargadores;
    }

    [Header("Configurá los 5 slots de la UI aquí")]
    public PlayerUISlot[] slotsJugadores = new PlayerUISlot[5];

    [Header("UI Central (Extracción)")]
    public TextMeshProUGUI textoExtraccion;

    // NUEVO: Variables para la Migración de Host
    [Header("UI Migración (Host)")]
    public GameObject panelFondoMigracion;
    public TextMeshProUGUI textoMigracion;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    void Start()
    {
        if (textoExtraccion != null) textoExtraccion.gameObject.SetActive(false);

        // Arrancamos con la pantalla de migración apagada
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(false);
    }

    void Update()
    {
        HealthSystem[] todosLosPersonajes = FindObjectsOfType<HealthSystem>();
        int currentIndexSlot = 0;

        for (int i = 0; i < todosLosPersonajes.Length; i++)
        {
            HealthSystem health = todosLosPersonajes[i];

            if (health.isPlayer && health.photonView != null)
            {
                if (currentIndexSlot >= slotsJugadores.Length) break;

                PlayerUISlot slot = slotsJugadores[currentIndexSlot];

                if (slot.contenedorSlot != null)
                    slot.contenedorSlot.SetActive(true);

                if (slot.textoNombre != null)
                {
                    string prefijo = health.photonView.IsMine ? "[VOS] " : "";
                    slot.textoNombre.text = prefijo + health.photonView.Owner.NickName;

                    if (health.photonView.Owner.CustomProperties.TryGetValue("color", out object indexColor))
                    {
                        int cIndex = (int)indexColor;
                        if (cIndex >= 0 && cIndex < GameColors.Palette.Length)
                        {
                            slot.textoNombre.color = GameColors.Palette[cIndex];
                        }
                    }
                }

                if (slot.barraVida != null)
                {
                    slot.barraVida.maxValue = health.maxHealth;
                    slot.barraVida.value = health.currentHealth;
                }

                TopDownWeaponController weapon = health.GetComponent<TopDownWeaponController>();
                if (slot.textoCargadores != null && weapon != null)
                {
                    slot.textoCargadores.text = "Cargadores: " + weapon.cargadoresActuales;
                }

                currentIndexSlot++;
            }
        }

        for (int i = currentIndexSlot; i < slotsJugadores.Length; i++)
        {
            if (slotsJugadores[i].contenedorSlot != null)
            {
                slotsJugadores[i].contenedorSlot.SetActive(false);
            }
        }
    }

    public void MostrarTextoExtraccion(string mensaje, Color colorMensaje)
    {
        if (textoExtraccion != null)
        {
            if (!textoExtraccion.gameObject.activeSelf) textoExtraccion.gameObject.SetActive(true);
            textoExtraccion.text = mensaje;
            textoExtraccion.color = colorMensaje;
        }
    }

    public void OcultarTextoExtraccion()
    {
        if (textoExtraccion != null && textoExtraccion.gameObject.activeSelf)
        {
            textoExtraccion.gameObject.SetActive(false);
        }
    }

    // ==========================================
    // NUEVO: MÉTODOS PARA LA MIGRACIÓN
    // ==========================================
    public void MostrarMigracion(string mensaje)
    {
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(true);
        if (textoMigracion != null) textoMigracion.text = mensaje;
    }

    public void OcultarMigracion()
    {
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(false);
    }
}