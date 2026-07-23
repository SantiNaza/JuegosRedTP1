using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Photon.Pun;

[System.Serializable]
public class ReporteMuerte
{
    public string agente;
    public int kills;
    public float tiempo;
}

[System.Serializable]
public class ReporteDescargado
{
    public string agente;
    public int kills;
    public string tiempo;
}

public class API_LaOrden : MonoBehaviour
{
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbzVu0Kxx6gFolsUGAUzp5slYJzxEw2xNJR0Va4F0Ztz_PhnHv6jiWwPwx9l1wLcW6uh/exec";

    public static int misKillsLocales = 0;
    public static bool yaEnviado = false;

    public static int aliadosRescatadosLocales = 0;
    public static int aliadosRevividosLocales = 0;

    [Header("Reporte Final")]
    [Tooltip("Tope de segundos que esperamos a que termine la partida antes de mostrar el reporte igual.")]
    [SerializeField] private float esperaMaximaFinPartida = 240f;

    // Esta función calcula toda la matemática y la manda al encriptador
    public static int GuardarExperienciaLocal(bool sobrevivio)
    {
        int xpGanada = 0;
        xpGanada += misKillsLocales * 5;
        xpGanada += aliadosRescatadosLocales * 5;
        xpGanada += aliadosRevividosLocales * 10;

        if (sobrevivio) xpGanada += 15;

        int xpActual = PlayerStatsConfig.GetXP();
        PlayerStatsConfig.SetXP(xpActual + xpGanada);

        return xpGanada; // Devolvemos el número para mostrarlo en pantalla
    }

    void Awake()
    {
        yaEnviado = false;
        misKillsLocales = 0;
        aliadosRescatadosLocales = 0;
        aliadosRevividosLocales = 0;
    }

    public void EnviarReporteMuerte(string nombreAgente, int totalKills, float tiempoPartida)
    {
        if (yaEnviado) return;
        yaEnviado = true;

        ReporteMuerte reporte = new ReporteMuerte();
        reporte.agente = nombreAgente;
        reporte.kills = totalKills;
        reporte.tiempo = tiempoPartida;

        string jsonPayload = JsonConvert.SerializeObject(reporte);
        StartCoroutine(EnviarPostYDescargar(jsonPayload));
    }

    private IEnumerator EnviarPostYDescargar(string json)
    {
        // ---------- 1) SUBIR el reporte (esto sí se hace apenas morís) ----------
        using (UnityWebRequest www = new UnityWebRequest(webAppUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error de transmisión: " + www.error);
                yaEnviado = false;
                yield break; // Cortamos acá si falló
            }
        }

        // ---------- 2) ESPERAR a que la partida termine de verdad ----------
        // Mientras quede aunque sea un jugador en juego (vivo o derribado), NO mostramos
        // el reporte. Si a vos te reviven, esto sigue esperando y el cartel no aparece.
        yield return StartCoroutine(EsperarFinDePartida());

        // ---------- 3) DESCARGAR y MOSTRAR ----------
        // Le damos tiempo a Google de registrar los datos de todos los jugadores
        yield return new WaitForSeconds(2f);

        using (UnityWebRequest wwwGet = UnityWebRequest.Get(webAppUrl))
        {
            yield return wwwGet.SendWebRequest();

            if (wwwGet.result == UnityWebRequest.Result.ConnectionError || wwwGet.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error descargando archivos: " + wwwGet.error);
            }
            else
            {
                string jsonRespuesta = wwwGet.downloadHandler.text;

                // Traducimos el JSON a una lista de C#
                List<ReporteDescargado> ultimosReportes = JsonConvert.DeserializeObject<List<ReporteDescargado>>(jsonRespuesta);

                string textoTerminal = "ARCHIVOS ANALÓGICOS RECUPERADOS:\n----------------------------------\n";

                int jugadoresEnPartida = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;

                int startIndex = Mathf.Max(0, ultimosReportes.Count - jugadoresEnPartida);

                for (int i = startIndex; i < ultimosReportes.Count; i++)
                {
                    ReporteDescargado rep = ultimosReportes[i];
                    textoTerminal += $"> Agente {rep.agente} | Bajas: {rep.kills} | Extracción: {rep.tiempo}\n";
                }

                textoTerminal += "----------------------------------\nFIN DE TRANSMISIÓN.";

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.MostrarReporteFinal(textoTerminal);
                }
            }
        }
    }

    // Espera hasta que no quede NINGÚN jugador en juego, o hasta que salgamos de la sala.
    // Tiene un tope de tiempo por si algo queda colgado.
    private IEnumerator EsperarFinDePartida()
    {
        float tiempoEsperado = 0f;

        while (tiempoEsperado < esperaMaximaFinPartida)
        {
            // si ya no estamos en la sala, no tiene sentido seguir esperando
            if (!PhotonNetwork.InRoom) yield break;

            if (HUDManager.Instance == null) yield break;

            int enJuego = HUDManager.Instance.ContarJugadoresEnJuego();
            if (enJuego <= 0) yield break;   // partida terminada

            yield return new WaitForSeconds(0.5f);
            tiempoEsperado += 0.5f;
        }

        Debug.LogWarning("[API] Se alcanzó la espera máxima; se muestra el reporte igual.");
    }
}