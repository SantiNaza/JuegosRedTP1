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
}

public class API_LaOrden : MonoBehaviour
{
    // ¡PEGÁ TU URL DE APPS SCRIPT ACÁ!
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbzVu0Kxx6gFolsUGAUzp5slYJzxEw2xNJR0Va4F0Ztz_PhnHv6jiWwPwx9l1wLcW6uh/exec";

    public void EnviarReporteMuerte(string nombreAgente, int totalKills)
    {
        ReporteMuerte reporte = new ReporteMuerte();
        reporte.agente = nombreAgente;
        reporte.kills = totalKills;

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
                Debug.Log("¡Reporte de baja recibido con éxito! Respuesta: " + www.downloadHandler.text);
            }
        }
    }
}