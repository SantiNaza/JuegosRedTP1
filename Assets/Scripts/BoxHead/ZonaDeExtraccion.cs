using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class ZonaDeExtraccion : MonoBehaviourPun
{
    [Header("Configuración")]
    public float tiempoParaGanar = 5f;
    private float tiempoRestante;

    private HashSet<Collider> jugadoresAdentro = new HashSet<Collider>();

    void Start()
    {
        tiempoRestante = tiempoParaGanar;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jugadoresAdentro.Add(other);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jugadoresAdentro.Remove(other);
        }
    }

    void Update()
    {
        jugadoresAdentro.RemoveWhere(col => col == null || !col.gameObject.activeInHierarchy || !col.CompareTag("Player"));
        GameObject[] jugadoresVivos = GameObject.FindGameObjectsWithTag("Player");

        if (jugadoresVivos.Length > 0 && jugadoresAdentro.Count == jugadoresVivos.Length)
        {
            tiempoRestante -= Time.deltaTime;

            if (HUDManager.Instance != null)
            {
                string mensaje = "Extracción: " + Mathf.CeilToInt(tiempoRestante).ToString() + "...";
                HUDManager.Instance.MostrarTextoExtraccion(mensaje, Color.yellow);
            }

            if (tiempoRestante <= 0)
            {
                EjecutarVictoria();
            }
        }
        else
        {
            tiempoRestante = tiempoParaGanar;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.OcultarTextoExtraccion();
            }
        }
    }

    private void EjecutarVictoria()
    {
        this.enabled = false;

        int xpGanada = API_LaOrden.GuardarExperienciaLocal(true);
        int nivelActual = PlayerStatsConfig.GetLevel();

        if (HUDManager.Instance != null)
        {
            // Agregamos un aviso visual para que el jugador no piense que el juego se tildó
            HUDManager.Instance.MostrarTextoExtraccion(
                $"¡EXTRACCIÓN EXITOSA!\n+{xpGanada} XP | Nivel {nivelActual}\nDescargando reportes analógicos...", Color.green);
        }

        API_LaOrden api = FindObjectOfType<API_LaOrden>();
        if (api != null)
        {
            api.EnviarReporteMuerte(PhotonNetwork.NickName, API_LaOrden.misKillsLocales, Time.timeSinceLevelLoad);
        }

        // LA CORRECCIÓN: Le damos 12 segundos en lugar de 3.
        // Esto permite que la API de Google responda (tarda unos 3-4 seg) y que 
        // el jugador tenga unos 8 segundos de sobra para leer toda la pantalla negra con los puntajes.
        Invoke(nameof(SalirDePartida), 12f);
    }

    private void SalirDePartida()
    {
        LocalPauseMenu.isPaused = false;
        PhotonNetwork.LeaveRoom();
    }
}