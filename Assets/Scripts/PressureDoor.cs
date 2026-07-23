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

    [Header("Simultaneidad")]
    public float simultaneityTolerance = 0.5f;

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

        float minTime = float.MaxValue;
        float maxTime = float.MinValue;

        foreach (PressurePlate plate in plates)
        {
            if (plate == null || !plate.IsPressed)
                return; 

            minTime = Mathf.Min(minTime, plate.PressedTime);
            maxTime = Mathf.Max(maxTime, plate.PressedTime);
        }

        if (maxTime - minTime <= simultaneityTolerance)
        {
            alreadyOpened = true;
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