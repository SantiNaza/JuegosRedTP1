using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PhotonView))]
public class PressurePlate : MonoBehaviourPun
{
    public string playerTag = "Player";

    [Tooltip("La puerta que controla esta placa")]
    public PressureDoor door;

    [Header("Visual (opcional, se hunde al pisarla)")]
    public Transform plateVisual;
    public float pressedDepth = 0.1f;

    private int playersOnPlate = 0;   // solo se cuenta en el MasterClient
    private Vector3 initialVisualPos;

    public bool IsPressed => playersOnPlate > 0;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (plateVisual != null) initialVisualPos = plateVisual.localPosition;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!other.CompareTag(playerTag)) return;

        playersOnPlate++;
        if (playersOnPlate == 1)
        {
            photonView.RPC(nameof(RPC_SetPressed), RpcTarget.All, true);
            if (door != null) door.EvaluatePlates();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!other.CompareTag(playerTag)) return;

        playersOnPlate = Mathf.Max(0, playersOnPlate - 1);
        if (playersOnPlate == 0)
        {
            photonView.RPC(nameof(RPC_SetPressed), RpcTarget.All, false);
            if (door != null) door.EvaluatePlates();
        }
    }

    // Sincroniza el hundimiento de la placa en TODOS los clientes
    [PunRPC]
    private void RPC_SetPressed(bool pressed)
    {
        if (plateVisual == null) return;
        plateVisual.localPosition = pressed
            ? initialVisualPos - Vector3.up * pressedDepth
            : initialVisualPos;
    }
}