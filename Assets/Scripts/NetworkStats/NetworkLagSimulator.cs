using UnityEngine;
using Photon.Pun;

public class NetworkLagSimulator : MonoBehaviour
{
    [Header("Configuración de Simulación")]
    [Tooltip("Activa o desactiva la simulación de lag")]
    public bool enableSimulation = false;

    [Tooltip("Lag artificial de ida y vuelta (RTT) en milisegundos")]
    public int incomingLagInMs = 100;
    public int outgoingLagInMs = 100;

    [Tooltip("Porcentaje de paquetes que se perderán (0 a 100)")]
    [Range(0, 100)]
    public int packetLossPercentage = 0;

    private void Update()
    {
        // Accedemos al Peer de Photon para aplicar la configuración en tiempo real
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;

        if (peer != null)
        {
            // Activamos/desactivamos la simulación según el booleano
            peer.IsSimulationEnabled = enableSimulation;

            if (enableSimulation)
            {
                // Configuramos los valores de Lag (Latencia)
                peer.NetworkSimulationSettings.IncomingLag = incomingLagInMs;
                peer.NetworkSimulationSettings.OutgoingLag = outgoingLagInMs;

                // Configuramos la pérdida de paquetes
                peer.NetworkSimulationSettings.IncomingLossPercentage = packetLossPercentage;
                peer.NetworkSimulationSettings.OutgoingLossPercentage = packetLossPercentage;
            }
        }
    }
}