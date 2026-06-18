using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;

public class HostMigrationManager : MonoBehaviourPunCallbacks
{
    // ==========================================
    // 1. CAÍDA DEL HOST (MIGRACIÓN)
    // ==========================================
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
    // ==========================================
    // 2. NUEVO: CAÍDA DE NUESTRA PROPIA CONEXIÓN
    // ==========================================
    public override void OnDisconnected(DisconnectCause cause)
    {
        // Si nos desconectamos a propósito (ej: saliendo al menú principal con un botón), no hacemos nada
        if (cause == DisconnectCause.DisconnectByClientLogic) return;

        Debug.LogWarning("Desconexión crítica de Photon. Causa: " + cause);

        // Congelamos el mundo localmente para que los zombis no nos coman en la pantalla de error
        Time.timeScale = 0f;

        if (HUDManager.Instance != null)
        {
            // Reutilizamos la pantalla negra pasándole un mensaje de error crítico
            string mensajeError = "CONEXIÓN PERDIDA.\nFALLO CRÍTICO EN EL ENLACE DE RED.\nMOTIVO: " + cause.ToString() + "\nPOR FAVOR, REINICIE EL SISTEMA.";
            HUDManager.Instance.MostrarMigracion(mensajeError);
        }
    }
}