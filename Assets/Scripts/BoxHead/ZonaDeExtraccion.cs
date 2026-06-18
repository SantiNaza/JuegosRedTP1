using UnityEngine;
using Photon.Pun;
using TMPro; // Necesario para el texto gigante de la UI
using System.Collections.Generic;

public class ZonaDeExtraccion : MonoBehaviourPun
{
    [Header("Configuración")]
    public float tiempoParaGanar = 5f;
    private float tiempoRestante;

    [Header("UI")]
    public TextMeshProUGUI textoCuentaRegresiva;

    // Guardamos los colisionadores de los jugadores que están adentro
    private HashSet<Collider> jugadoresAdentro = new HashSet<Collider>();

    void Start()
    {
        tiempoRestante = tiempoParaGanar;
        if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);
    }

    // 1. Detectar quién entra
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Algo entró: " + other.name + " | tag: " + other.tag);
        if (other.CompareTag("Player"))
        {
            jugadoresAdentro.Add(other);
        }
    }

    // 2. Detectar quién sale
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jugadoresAdentro.Remove(other);
        }
    }

    void Update()
    {
        // Limpiamos la lista por si algún jugador se desconectó mientras estaba adentro
        jugadoresAdentro.RemoveWhere(col => col == null || !col.gameObject.activeInHierarchy || !col.CompareTag("Player"));

        // Buscamos cuántos jugadores VIVOS hay en todo el mapa
        GameObject[] jugadoresVivos = GameObject.FindGameObjectsWithTag("Player");

        // Condición de victoria: Hay al menos 1 vivo, y TODOS los vivos están en la zona
        if (jugadoresVivos.Length > 0 && jugadoresAdentro.Count == jugadoresVivos.Length)
        {
            tiempoRestante -= Time.deltaTime;

            if (textoCuentaRegresiva != null)
            {
                textoCuentaRegresiva.gameObject.SetActive(true);
                // Usamos CeilToInt para que muestre 5, 4, 3, 2, 1 sin decimales
                textoCuentaRegresiva.text = Mathf.CeilToInt(tiempoRestante).ToString();
            }

            // ¡Llegamos a cero!
            if (tiempoRestante <= 0)
            {
                EjecutarVictoria();
            }
        }
        else
        {
            // Si falta alguien o alguien sale de la zona por pánico, se resetea todo
            tiempoRestante = tiempoParaGanar;
            if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);
        }
    }

    private void EjecutarVictoria()
    {
        // Apagamos este script para que no se envíen datos repetidos a la API
        this.enabled = false;

        if (textoCuentaRegresiva != null)
        {
            textoCuentaRegresiva.text = "¡EXTRACCIÓN EXITOSA!";
            textoCuentaRegresiva.color = Color.green;
        }

        // ¡EL MOMENTO CLAVE! Cada cliente envía sus propios datos a la base.
        API_LaOrden api = FindObjectOfType<API_LaOrden>();
        if (api != null)
        {
            // Mandamos: Nickname, Las kills que sumamos, y los segundos que duró la partida.
            api.EnviarReporteMuerte(PhotonNetwork.NickName, API_LaOrden.misKillsLocales, Time.timeSinceLevelLoad);
        }
    }
}