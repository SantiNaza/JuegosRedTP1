using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

[System.Serializable]
public class ReporteMuerte
{
    public string agente;
    public int kills;
    public float tiempo;
}

// NUEVO: La estructura para decodificar lo que bajamos
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

    void Awake()
    {
        yaEnviado = false;
        misKillsLocales = 0;
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
        // 1. SUBIMOS NUESTROS DATOS
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

        // 2. Esperamos 2 segundos para darle tiempo a Google de guardar los datos de todos los amigos muertos
        yield return new WaitForSeconds(2f);

        // 3. DESCARGAMOS LOS ÚLTIMOS REGISTROS
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

                // Armamos el texto estético de terminal
                string textoTerminal = "ARCHIVOS ANALÓGICOS RECUPERADOS:\n----------------------------------\n";

                foreach (ReporteDescargado rep in ultimosReportes)
                {
                    textoTerminal += $"> Agente {rep.agente} | Bajas: {rep.kills} | Extracción: {rep.tiempo}\n";
                }

                textoTerminal += "----------------------------------\nFIN DE TRANSMISIÓN.";

                // Imprimimos los resultados en la pantalla negra de redundancia
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.MostrarMigracion(textoTerminal);
                }
            }
        }
    }
}