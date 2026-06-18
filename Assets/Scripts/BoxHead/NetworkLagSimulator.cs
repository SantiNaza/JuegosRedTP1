using UnityEngine;
using Photon.Pun;

public class NetworkLagSimulator : MonoBehaviour
{
    [Header("Configuración de Simulación")]
    public bool enableSimulation = false;

    public int lagInMs = 0;
    [Range(0, 100)] public int packetLossPercentage = 0;

    void Update()
    {
        // El botón mágico para prender y apagar
        if (Input.GetKeyDown(KeyCode.F9))
        {
            enableSimulation = !enableSimulation;

            // Si lo prendés y estaba todo en cero, le damos un lag inicial para que se note
            if (enableSimulation && lagInMs == 0 && packetLossPercentage == 0)
            {
                lagInMs = 200;
                packetLossPercentage = 5;
            }
        }

        // Si la simulación está prendida, escuchamos las flechas del teclado
        if (enableSimulation)
        {
            // Flechas Arriba/Abajo modifican el Lag (de a 50ms)
            if (Input.GetKeyDown(KeyCode.UpArrow)) lagInMs += 50;
            if (Input.GetKeyDown(KeyCode.DownArrow)) lagInMs -= 50;

            // Flechas Derecha/Izquierda modifican la pérdida de paquetes (de a 5%)
            if (Input.GetKeyDown(KeyCode.RightArrow)) packetLossPercentage += 5;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) packetLossPercentage -= 5;

            // Bloqueamos los números para que no se rompa la matemática de Photon (no podés tener lag negativo)
            lagInMs = Mathf.Clamp(lagInMs, 0, 2000);
            packetLossPercentage = Mathf.Clamp(packetLossPercentage, 0, 100);
        }

        // Aplicamos siempre por si tocaste alguna flecha
        AplicarConfiguracion();
    }

    private void AplicarConfiguracion()
    {
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        if (peer != null)
        {
            peer.IsSimulationEnabled = enableSimulation;

            if (enableSimulation)
            {
                peer.NetworkSimulationSettings.IncomingLag = lagInMs;
                peer.NetworkSimulationSettings.OutgoingLag = lagInMs;
                peer.NetworkSimulationSettings.IncomingLossPercentage = packetLossPercentage;
                peer.NetworkSimulationSettings.OutgoingLossPercentage = packetLossPercentage;
            }
        }
    }

    // Dibujamos el Panel de Debug directamente en la pantalla
    void OnGUI()
    {
        if (enableSimulation)
        {
            // 1. Dibujamos la caja de fondo oscura
            GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
            Rect panelRect = new Rect(20, 20, 320, 140);
            GUI.Box(panelRect, "");
            GUI.Box(panelRect, ""); // Lo dibujamos dos veces para que sea más opaco

            // 2. Estilos de texto
            GUIStyle tituloStyle = new GUIStyle();
            tituloStyle.fontSize = 18;
            tituloStyle.fontStyle = FontStyle.Bold;
            tituloStyle.normal.textColor = Color.red;

            GUIStyle infoStyle = new GUIStyle();
            infoStyle.fontSize = 16;
            infoStyle.fontStyle = FontStyle.Bold;
            infoStyle.normal.textColor = Color.cyan;

            GUIStyle atajosStyle = new GUIStyle();
            atajosStyle.fontSize = 12;
            atajosStyle.normal.textColor = Color.gray;

            // 3. Escribimos la info adentro de la caja
            GUI.Label(new Rect(30, 30, 300, 30), "SISTEMA DE REDUNDANCIA", tituloStyle);

            // Los valores se actualizan al instante cuando tocás las flechas
            GUI.Label(new Rect(30, 60, 300, 25), $"Lag (ARRIBA / ABAJO): {lagInMs} ms", infoStyle);
            GUI.Label(new Rect(30, 90, 300, 25), $"Pérdida (DERECHA / IZQUIERDA): {packetLossPercentage} %", infoStyle);

            GUI.Label(new Rect(30, 130, 300, 20), "Presione [F9] para desactivar", atajosStyle);
        }
    }
}