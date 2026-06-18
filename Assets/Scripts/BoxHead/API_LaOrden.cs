using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;

[System.Serializable]
public class ReporteMuerte
{
    public string agente;
    public int kills;
    public float tiempo;
}

public class API_LaOrden : MonoBehaviour
{
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbzVu0Kxx6gFolsUGAUzp5slYJzxEw2xNJR0Va4F0Ztz_PhnHv6jiWwPwx9l1wLcW6uh/exec";

    public static int misKillsLocales = 0;

    // NUEVO: Candado de seguridad
    public static bool yaEnviado = false;

    void Awake()
    {
        // Al arrancar un nivel nuevo, reseteamos el candado y las kills
        yaEnviado = false;
        misKillsLocales = 0;
    }

    public void EnviarReporteMuerte(string nombreAgente, int totalKills, float tiempoPartida)
    {
        // Si ya mandamos datos en esta partida, abortamos para evitar duplicados
        if (yaEnviado) return;

        yaEnviado = true; // Cerramos el candado

        ReporteMuerte reporte = new ReporteMuerte();
        reporte.agente = nombreAgente;
        reporte.kills = totalKills;
        reporte.tiempo = tiempoPartida;

        string jsonPayload = JsonConvert.SerializeObject(reporte);
        StartCoroutine(EnviarPost(jsonPayload));
    }

    private IEnumerator EnviarPost(string json)
    {
        using (UnityWebRequest www = new UnityWebRequest(webAppUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error de transmisión a la base: " + www.error);
                yaEnviado = false; // Si hubo un error de internet, abrimos el candado para reintentar
            }
            else
            {
                Debug.Log("¡Reporte recibido con éxito! Respuesta: " + www.downloadHandler.text);
            }
        }
    }
}