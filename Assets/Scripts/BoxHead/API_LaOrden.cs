using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;

// El "molde" de nuestro paquete de datos
[System.Serializable]
public class ReporteMuerte
{
    public string agente;
    public int kills;
    public float tiempo; // Agregamos el tiempo de partida
}

public class API_LaOrden : MonoBehaviour
{
    // Tu URL ya está pegada acá
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbzVu0Kxx6gFolsUGAUzp5slYJzxEw2xNJR0Va4F0Ztz_PhnHv6jiWwPwx9l1wLcW6uh/exec";

    // Variable global para contar los zombis que matamos NOSOTROS
    public static int misKillsLocales = 0;

    // Actualizamos el método para que pida el tiempo
    public void EnviarReporteMuerte(string nombreAgente, int totalKills, float tiempoPartida)
    {
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
            }
            else
            {
                Debug.Log("¡Reporte recibido con éxito! Respuesta: " + www.downloadHandler.text);
            }
        }
    }
}