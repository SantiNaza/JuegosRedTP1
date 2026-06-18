using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PressureDoor : MonoBehaviourPun
{
    [Header("Placas necesarias (arrastrá las 2)")]
    public PressurePlate[] plates;

    [Header("Movimiento de la puerta")]
    public Transform doorTransform;
    public Vector3 openOffset = new Vector3(0f, 4f, 0f);
    public float moveSpeed = 3f;

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool isOpen = false;
    private bool alreadyOpened = false; // una vez abierta, queda así para siempre

    void Awake()
    {
        if (doorTransform == null) doorTransform = transform;
        closedPos = doorTransform.position;
        openPos = closedPos + openOffset;
    }

    // Lo llaman las placas (corre solo en el MasterClient)
    public void EvaluatePlates()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Si ya se abrió alguna vez, no volvemos a evaluar nunca más
        if (alreadyOpened) return;

        bool allPressed = true;
        foreach (PressurePlate plate in plates)
        {
            if (plate == null || !plate.IsPressed)
            {
                allPressed = false;
                break;
            }
        }

        if (allPressed)
        {
            alreadyOpened = true;
            // Buffered: el que entre tarde también la ve abierta
            photonView.RPC(nameof(RPC_OpenForever), RpcTarget.AllBuffered);
        }
    }

    [PunRPC]
    private void RPC_OpenForever()
    {
        isOpen = true;
        alreadyOpened = true;
    }

    void Update()
    {
        Vector3 target = isOpen ? openPos : closedPos;
        doorTransform.position = Vector3.MoveTowards(
            doorTransform.position, target, moveSpeed * Time.deltaTime);
    }
}