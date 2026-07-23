using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
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
    public GameObject botonExtraccion;

    [Header("UI Migración (Host)")]
    public GameObject panelFondoMigracion;
    public TextMeshProUGUI textoMigracion;

    [Header("UI Notificaciones")]
    public TextMeshProUGUI textoNotificaciones;

    [Header("UI Derrota")]
    public GameObject panelDerrota;

    [Header("UI Reporte Final (opcional)")]
    [Tooltip("Si los dejás vacíos, el reporte se muestra en el panel de Migración como hasta ahora.")]
    public GameObject panelReporteFinal;
    public TextMeshProUGUI textoReporteFinal;

    // ---------- Paleta del kit ----------
    public static readonly Color AMBER = new Color32(0xF5, 0xA8, 0x28, 0xFF); // ámbar principal
    public static readonly Color CREAM = new Color32(0xEB, 0xE1, 0xCD, 0xFF); // texto normal
    public static readonly Color DANGER = new Color32(0xD6, 0x3A, 0x3A, 0xFF); // vida crítica

    [Header("Colores del HUD")]
    [Tooltip("Si está activo, la barra de vida se tiñe con el color del jugador.")]
    [SerializeField] private bool tenirBarraConColorJugador = true;
    [Tooltip("Debajo de este porcentaje la barra parpadea en rojo.")]
    [Range(0f, 0.5f)][SerializeField] private float umbralVidaCritica = 0.25f;

    [Header("Rendimiento")]
    [Tooltip("Cada cuántos segundos se vuelve a buscar jugadores en la escena.")]
    [SerializeField] private float intervaloRefrescoJugadores = 0.5f;

    [Header("Detección de Derrota")]
    [Tooltip("Segundos de gracia al iniciar la escena antes de chequear la derrota. " +
             "Evita falsos positivos mientras los jugadores todavía están spawneando.")]
    [SerializeField] private float retrasoInicialDerrota = 4f;

    // cache para no llamar a FindObjectsOfType todos los frames
    private readonly List<HealthSystem> jugadoresCache = new List<HealthSystem>();
    private float proximoRefresco;

    // estado de la derrota
    private bool huboJugadores;      // ya se detectó al menos un jugador vivo alguna vez
    private bool derrotaMostrada;    // para no dispararla más de una vez
    private bool mostrandoReporteFinal; // el reporte está en pantalla, no lo pisemos

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
        if (botonExtraccion != null) botonExtraccion.SetActive(false);
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(false);
        if (textoNotificaciones != null) textoNotificaciones.gameObject.SetActive(false);

        // Apagamos el panel de derrota al empezar
        if (panelDerrota != null) panelDerrota.SetActive(false);
        if (panelReporteFinal != null) panelReporteFinal.SetActive(false);

        AplicarPaletaInicial();
        RefrescarListaJugadores();
    }

    // Pinta de una sola vez los textos fijos con la paleta del kit
    void AplicarPaletaInicial()
    {
        if (textoExtraccion != null) textoExtraccion.color = AMBER;
        if (textoMigracion != null) textoMigracion.color = AMBER;
        if (textoNotificaciones != null) textoNotificaciones.color = AMBER;

        foreach (var slot in slotsJugadores)
        {
            if (slot.textoCargadores != null) slot.textoCargadores.color = CREAM;
        }
    }

    // Busca los jugadores y los ORDENA por ActorNumber.
    // Sin esto, FindObjectsOfType devuelve un orden arbitrario y los jugadores
    // saltan de slot entre frame y frame.
    void RefrescarListaJugadores()
    {
        jugadoresCache.Clear();

        HealthSystem[] todos = FindObjectsOfType<HealthSystem>();
        foreach (var h in todos)
        {
            if (h != null && h.isPlayer && h.photonView != null && h.photonView.Owner != null)
                jugadoresCache.Add(h);
        }

        jugadoresCache.Sort((a, b) =>
            a.photonView.Owner.ActorNumber.CompareTo(b.photonView.Owner.ActorNumber));
    }

    void Update()
    {
        // refresco espaciado en vez de cada frame
        if (Time.time >= proximoRefresco)
        {
            proximoRefresco = Time.time + intervaloRefrescoJugadores;
            RefrescarListaJugadores();
        }

        int currentIndexSlot = 0;
        int jugadoresVivos = 0;

        for (int i = 0; i < jugadoresCache.Count; i++)
        {
            HealthSystem health = jugadoresCache[i];
            if (health == null) continue;                       // se desconectó
            if (currentIndexSlot >= slotsJugadores.Length) break;

            // Conteo para la derrota.
            // OJO: un jugador DERRIBADO (isDowned) sigue contando como "en juego",
            // porque todavía lo pueden revivir durante el bleedOutTime.
            // La muerte real es cuando HealthSystem.Die() hace PhotonNetwork.Destroy,
            // y ahí el objeto directamente desaparece de esta lista.
            if (health.currentHealth > 0 || health.isDowned) jugadoresVivos++;

            PlayerUISlot slot = slotsJugadores[currentIndexSlot];

            if (slot.contenedorSlot != null)
                slot.contenedorSlot.SetActive(true);

            // color elegido por el jugador en SelectCharacter
            Color colorJugador = CREAM;
            if (health.photonView.Owner.CustomProperties.TryGetValue("color", out object indexColor))
            {
                int cIndex = (int)indexColor;
                if (cIndex >= 0 && cIndex < GameColors.Palette.Length)
                    colorJugador = GameColors.Palette[cIndex];
            }

            if (slot.textoNombre != null)
            {
                slot.textoNombre.text = health.photonView.Owner.NickName;
                slot.textoNombre.color = colorJugador;
            }

            if (slot.barraVida != null)
            {
                slot.barraVida.maxValue = health.maxHealth;
                slot.barraVida.value = health.currentHealth;

                PintarBarra(slot.barraVida, colorJugador,
                            health.maxHealth > 0 ? (float)health.currentHealth / health.maxHealth : 0f);
            }

            TopDownWeaponController weapon = health.GetComponent<TopDownWeaponController>();
            if (slot.textoCargadores != null && weapon != null)
            {
                // ámbar para el número, crema para la etiqueta
                slot.textoCargadores.text =
                    $"<color=#EBE1CD>CARGADORES</color>  <color=#F5A828><b>{weapon.cargadoresActuales}</b></color>";
            }

            currentIndexSlot++;
        }

        for (int i = currentIndexSlot; i < slotsJugadores.Length; i++)
        {
            if (slotsJugadores[i].contenedorSlot != null)
            {
                slotsJugadores[i].contenedorSlot.SetActive(false);
            }
        }

        ChequearDerrota(jugadoresVivos);
    }

    // Derrota = no queda NINGÚN jugador en juego.
    // "En juego" incluye a los derribados, que todavía pueden ser revividos.
    // Cuando a un derribado se le acaba el desangrado, HealthSystem.Die() suelta
    // las chapas y destruye el GameObject, así que deja de aparecer en la lista.
    // Recién cuando la lista queda sin nadie en juego se muestra la derrota.
    void ChequearDerrota(int jugadoresVivos)
    {
        if (derrotaMostrada) return;

        // margen de gracia al cargar la escena: los players todavía están spawneando
        // y algunos scripts setean currentHealth recién en Start().
        if (Time.timeSinceLevelLoad < retrasoInicialDerrota) return;

        // recién empezamos a vigilar una vez que vimos al menos un jugador vivo
        if (jugadoresVivos > 0)
        {
            huboJugadores = true;
            return;
        }

        if (huboJugadores)
        {
            MostrarDerrota();
        }
    }

    // Tiñe el Fill del Slider y avisa cuando la vida está crítica
    void PintarBarra(Slider barra, Color colorJugador, float porcentaje)
    {
        if (barra.fillRect == null) return;

        Image fill = barra.fillRect.GetComponent<Image>();
        if (fill == null) return;

        if (porcentaje <= umbralVidaCritica)
        {
            // parpadeo suave en rojo
            float t = Mathf.PingPong(Time.time * 3f, 1f);
            fill.color = Color.Lerp(DANGER, Color.white, t * 0.35f);
        }
        else
        {
            fill.color = tenirBarraConColorJugador ? colorJugador : AMBER;
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

        if (botonExtraccion != null)
        {
            botonExtraccion.SetActive(true);
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Versión corta: usa el ámbar del kit sin tener que pasar color
    public void MostrarTextoExtraccion(string mensaje)
    {
        MostrarTextoExtraccion(mensaje, AMBER);
    }

    public void OcultarTextoExtraccion()
    {
        if (textoExtraccion != null && textoExtraccion.gameObject.activeSelf)
        {
            textoExtraccion.gameObject.SetActive(false);
        }

        if (botonExtraccion != null)
        {
            botonExtraccion.SetActive(false);
        }
        
       
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void MostrarMigracion(string mensaje)
    {
        if (panelFondoMigracion != null) panelFondoMigracion.SetActive(true);
        if (textoMigracion != null)
        {
            textoMigracion.text = mensaje;
            textoMigracion.color = AMBER;
        }
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
        textoNotificaciones.color = AMBER;
        textoNotificaciones.text = mensaje;
        yield return new WaitForSeconds(tiempo);
        textoNotificaciones.gameObject.SetActive(false);
    }

    // Cuenta los jugadores que siguen EN JUEGO en este momento.
    // "En juego" = vivo, o derribado (todavía revivible).
    // Un jugador realmente muerto ya no existe: Die() hace PhotonNetwork.Destroy.
    public int ContarJugadoresEnJuego()
    {
        int n = 0;
        HealthSystem[] todos = FindObjectsOfType<HealthSystem>();
        foreach (var h in todos)
        {
            if (h == null || !h.isPlayer) continue;
            if (h.currentHealth > 0 || h.isDowned) n++;
        }
        return n;
    }

    // GUARDA: aunque otro script la llame, la derrota NO se muestra
    // mientras quede al menos un jugador en juego.
    public void MostrarDerrota()
    {
        if (derrotaMostrada) return;

        int enJuego = ContarJugadoresEnJuego();
        if (enJuego > 0)
        {
            Debug.Log($"[HUD] Se pidió la derrota pero todavía quedan {enJuego} jugador(es) en juego. Ignorado.");
            return;
        }

        ForzarDerrota();
    }

    // Muestra el reporte de fin de partida ("ARCHIVOS ANALÓGICOS RECUPERADOS").
    // Si no asignaste panelReporteFinal, cae al panel de Migración como antes.
    public void MostrarReporteFinal(string texto)
    {
        mostrandoReporteFinal = true;

        if (panelReporteFinal != null)
        {
            panelReporteFinal.SetActive(true);
            if (textoReporteFinal != null)
            {
                textoReporteFinal.text = texto;
                textoReporteFinal.color = AMBER;
            }
        }
        else
        {
            MostrarMigracion(texto);
        }
    }

    // Muestra la derrota sin chequear nada. Usar solo si de verdad hace falta.
    public void ForzarDerrota()
    {
        if (derrotaMostrada) return;   // que no se dispare dos veces
        derrotaMostrada = true;

        if (panelDerrota != null) panelDerrota.SetActive(true);

        OcultarTextoExtraccion();

        // OJO: no apagamos la migración si ahí se está mostrando el reporte final
        if (!mostrandoReporteFinal) OcultarMigracion();

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