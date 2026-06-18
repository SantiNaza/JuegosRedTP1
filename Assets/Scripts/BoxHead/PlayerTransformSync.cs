using UnityEngine;
using Photon.Pun;

public class PlayerTransformSync : MonoBehaviourPun, IPunObservable
{
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    [Header("Compensación de Lag")]
    public float lerpSpeed = 15f;

    void Start()
    {
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    void Update()
    {
        if (photonView.IsMine) return;

        // INTERPOLACIÓN: Suaviza la posición remota frame a frame para evitar teletransportes
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}