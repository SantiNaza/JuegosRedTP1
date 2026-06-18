using UnityEngine;
using Photon.Pun;

public class ZombieNetworkSync : MonoBehaviourPun, IPunObservable
{
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    [Header("Configuración calcada del TransformViewClassic")]
    public float positionLerpSpeed = 15f;

    [Tooltip("Distancia máxima antes de teletransportarse de golpe (Estaba en 3)")]
    public float teleportDistance = 3f;

    [Tooltip("Velocidad de rotación constante (Estaba en 180)")]
    public float rotateTowardsSpeed = 180f;

    void Start()
    {
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    void Update()
    {
        if (photonView.IsMine) return;

        // 1. CHEQUEO DE TELETRANSPORTE (Igual a tu "Enable teleport for great distances")
        if (Vector3.Distance(transform.position, networkPosition) > teleportDistance)
        {
            transform.position = networkPosition;
        }
        else
        {
            // 2. INTERPOLACIÓN SUAVE
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
        }

        // 3. ROTACIÓN (Igual a tu "Rotate Towards" con velocidad 180)
        transform.rotation = Quaternion.RotateTowards(transform.rotation, networkRotation, rotateTowardsSpeed * Time.deltaTime);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            // La escala no se envía. ¡Ahorramos datos!
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}