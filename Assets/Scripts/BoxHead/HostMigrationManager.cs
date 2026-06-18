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
        Time.timeScale = 0f;

        if (HUDManager.Instance != null)
        {
            string nombre = string.IsNullOrEmpty(nuevoHost.NickName) ? "Agente " + nuevoHost.ActorNumber : nuevoHost.NickName;
            string mensaje = "ENLACE PRIMARIO PERDIDO.\nBIBUBUBOP BIBUBUBOP\nESTABLECIENDO REDUNDANCIA CON: " + nombre + "...\nPOR FAVOR ESPERE.";
            HUDManager.Instance.MostrarMigracion(mensaje);
        }

        yield return new WaitForSecondsRealtime(3f);

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
    // 2. CAÍDA DE NUESTRA PROPIA CONEXIÓN
    // ==========================================
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (cause == DisconnectCause.DisconnectByClientLogic) return;

        Debug.LogWarning("Desconexión crítica de Photon. Causa: " + cause);

        Time.timeScale = 0f;

        if (HUDManager.Instance != null)
        {
            string mensajeError = "CONEXIÓN PERDIDA.\nFALLO CRÍTICO EN EL ENLACE DE RED.\nMOTIVO: " + cause.ToString() + "\nPOR FAVOR, REINICIE EL SISTEMA.";
            HUDManager.Instance.MostrarMigracion(mensajeError);
        }
    }

    // ==========================================
    // 3. NUEVO: DESCONEXIÓN DE OTRO JUGADOR
    // ==========================================
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Si el que se fue era el Host, la migración ya se encarga de avisar.
        // Solo lanzamos la notificación chiquita si era un jugador normal.
        if (!otherPlayer.IsMasterClient)
        {
            string nombre = string.IsNullOrEmpty(otherPlayer.NickName) ? "Jugador " + otherPlayer.ActorNumber : otherPlayer.NickName;
            string mensaje = nombre + " se desconectó.";
            
            Debug.Log(mensaje);

            if (HUDManager.Instance != null)
            {
                // Le pedimos al HUD que muestre el texto por 3 segundos
                HUDManager.Instance.MostrarNotificacionTemporal(mensaje, 3f);
            }
        }
    }
}