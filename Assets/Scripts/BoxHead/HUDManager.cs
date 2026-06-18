using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections;
using UnityEngine.SceneManagement; // Para poder cargar el menú

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

    [Header("UI Migración (Host)")]
    public GameObject panelFondoMigracion;
    public TextMeshProUGUI textoMigracion;

    [Header("UI Notificaciones")]
    public TextMeshProUGUI textoNotificaciones;

 
    [Header("UI Derrota")]
    public GameObject panelDerrota;

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
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(false);
        if (textoNotificaciones != null) textoNotificaciones.gameObject.SetActive(false);
        
        // Apagamos el panel de derrota al empezar
        if (panelDerrota != null) panelDerrota.SetActive(false);
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
                    slot.textoNombre.text = health.photonView.Owner.NickName;

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

    public void MostrarMigracion(string mensaje)
    {
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(true);
        if (textoMigracion != null) textoMigracion.text = mensaje;
    }

    public void OcultarMigracion()
    {
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(false);
    }

    public void MostrarNotificacionTemporal(string mensaje, float tiempo)
    {
        if (textoNotificaciones != null)
        {
            StopCoroutine("RutinaNotificacion"); 
            StartCoroutine(RutinaNotificacion(mensaje, tiempo));
        }
    }

    private IEnumerator RutinaNotificacion(string mensaje, float tiempo)
    {
        textoNotificaciones.gameObject.SetActive(true);
        textoNotificaciones.text = mensaje;
        yield return new WaitForSeconds(tiempo);
        textoNotificaciones.gameObject.SetActive(false);
    }

    
    public void MostrarDerrota()
    {
        if (panelDerrota != null) panelDerrota.SetActive(true);

        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    
    public void VolverAlMenuPrincipal()
    {
        StartCoroutine(RutinaSalir());
    }

    private IEnumerator RutinaSalir()
    {

        Time.timeScale = 1f; 

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            // Esperamos a que Photon nos desconecte de la sala antes de cargar la escena
            while (PhotonNetwork.InRoom) yield return null; 
        }

        // Carga la escena del menú principal
        SceneManager.LoadScene("Menu"); 
    }
}