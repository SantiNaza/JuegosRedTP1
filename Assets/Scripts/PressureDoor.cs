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

    [Header("Tolerancia de Reflejos (Segundos)")]
    [Tooltip("Cuánto tiempo de diferencia puede haber entre que A y B presionan la tecla")]
    public double simultaneityTolerance = 0.5;

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool isOpen = false;
    private bool alreadyOpened = false;

    void Awake()
    {
        if (doorTransform == null) doorTransform = transform;
        closedPos = doorTransform.position;
        openPos = closedPos + openOffset;
    }

    public void EvaluatePlates()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (alreadyOpened) return;

        double currentTime = PhotonNetwork.Time;

        // Verificamos si TODAS las placas fueron presionadas recientemente
        foreach (PressurePlate plate in plates)
        {
            if (plate == null) return;

            // Si la placa nunca se presionó, o si se presionó hace MÁS del tiempo tolerado, abortamos
            if (currentTime - plate.PressedTime > simultaneityTolerance)
            {
                return;
            }
        }

        // ¡Si el código llega hasta acá, significa que ambas placas pasaron la prueba!
        alreadyOpened = true;
        photonView.RPC(nameof(RPC_OpenForever), RpcTarget.AllBuffered);
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