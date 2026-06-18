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
        Debug.Log("Algo entró: " + other.name + " | tag: " + other.tag);
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

            // LLAMAMOS AL HUD MANAGER
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

            // LLAMAMOS AL HUD MANAGER PARA OCULTAR EL TEXTO SI SALEN DE LA ZONA
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.OcultarTextoExtraccion();
            }
        }
    }

    private void EjecutarVictoria()
    {
        this.enabled = false;

        // CONGELAMOS EL TIEMPO AL INSTANTE
        Time.timeScale = 0f;

        // MOSTRAMOS EL MENSAJE FINAL
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.MostrarTextoExtraccion("¡EXTRACCIÓN EXITOSA!", Color.green);
        }

        API_LaOrden api = FindObjectOfType<API_LaOrden>();
        if (api != null)
        {
            api.EnviarReporteMuerte(PhotonNetwork.NickName, API_LaOrden.misKillsLocales, Time.timeSinceLevelLoad);
        }
    }
}