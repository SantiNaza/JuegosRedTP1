using UnityEngine;
using Photon.Pun;

public class TargetController : MonoBehaviourPun, IPunObservable
{
    public float speed = 5f;
    private bool isStopped = false;

    // Variables de red
    private Vector3 networkPosition;
    private Vector3 networkDirection;
    private float distance;
    
    [Header("Network Sync Mode")]
    public bool useExtrapolation = false;

    void Start()
    {
        networkPosition = transform.position;
    }

    void Update()
    {
        if (isStopped) return;

        if (PhotonNetwork.IsMasterClient)
        {
            // Movimiento local en el Master Client (Ejemplo: Ping-pong en el eje X)
            float xPos = Mathf.PingPong(Time.time * speed, 10f) - 5f;
            Vector3 newPos = new Vector3(xPos, transform.position.y, transform.position.z);
            networkDirection = (newPos - transform.position).normalized;
            transform.position = newPos;
        }
        else
        {
            // Compensación de lag en los Clientes
            if (useExtrapolation)
            {
                // EXTRAPOLACIÓN: Predecir la posición basada en la última dirección conocida y el tiempo de retraso
                transform.position = Vector3.MoveTowards(transform.position, networkPosition, distance * (1.0f / PhotonNetwork.SerializationRate));
            }
            else
            {
                // INTERPOLACIÓN: Suavizar el movimiento hacia la última posición recibida
                transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * speed);
            }
        }
    }

    public void StopTarget()
    {
        isStopped = true;
    }

    // Sincronización de datos
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) // Master Client envía
        {
            stream.SendNext(transform.position);
            stream.SendNext(networkDirection);
            stream.SendNext(isStopped);
        }
        else // Clientes reciben
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkDirection = (Vector3)stream.ReceiveNext();
            isStopped = (bool)stream.ReceiveNext();

            if (useExtrapolation)
            {
                float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
                networkPosition += networkDirection * speed * lag;
                distance = Vector3.Distance(transform.position, networkPosition);
            }
        }
    }
}