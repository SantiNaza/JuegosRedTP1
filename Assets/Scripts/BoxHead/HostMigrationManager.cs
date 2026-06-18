using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;

public class HostMigrationManager : MonoBehaviourPunCallbacks
{
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log("El Host original se cayó. Transfiriendo red a: " + newMasterClient.NickName);
        StartCoroutine(RutinaPausaMigracion(newMasterClient));
    }

    private IEnumerator RutinaPausaMigracion(Player nuevoHost)
    {
        // 1. Congelamos el tiempo
        Time.timeScale = 0f;

        // 2. Le pedimos al Singleton del HUD que prenda el cartel
        if (HUDManager.Instance != null)
        {
            string nombre = string.IsNullOrEmpty(nuevoHost.NickName) ? "Agente " + nuevoHost.ActorNumber : nuevoHost.NickName;
            string mensaje = "ENLACE PRIMARIO PERDIDO.\nBIBUBUBOP BIBUBUBOP\nESTABLECIENDO REDUNDANCIA CON: " + nombre + "...\nPOR FAVOR ESPERE.";
            HUDManager.Instance.MostrarMigracion(mensaje);
        }

        // 3. Esperamos 3 segundos reales
        yield return new WaitForSecondsRealtime(3f);

        // 4. Descongelamos el tiempo y le pedimos al HUD que apague el cartel
        Time.timeScale = 1f;
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.OcultarMigracion();
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Migración exitosa. Ahora controlo la IA de La Orden.");
        }
    }
}