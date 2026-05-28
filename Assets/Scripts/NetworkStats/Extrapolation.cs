using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class Extrapolation : MonoBehaviourPun, IPunObservable 
{
    private Vector3 networkPosition;
    private Vector3 networkVelocity;
    private double lastPacketTime;
    
    private Rigidbody rb;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Importante: Si no es nuestro, apagamos la gravedad para que la física local
        // no pelee contra la posición que viene de la red.
        if (!photonView.IsMine)
        {
            rb.useGravity = false;
            rb.isKinematic = true; // Opcional, según si querés que colisione localmente o no
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(rb.velocity); // Enviamos velocidad obligatoriamente, puede ser del RB o de un script propio
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            lastPacketTime = info.SentServerTime;
        }
    }

    private void Update()
    {
        if (!photonView.IsMine)
        {
            // Calculamos cuánto tiempo pasó desde que el paquete salió del emisor
            float lag = (float)(PhotonNetwork.Time - lastPacketTime);

            // Extrapolamos la posición: Posición Recibida + (Velocidad * Tiempo de retraso)
            Vector3 extrapolatedPosition = networkPosition + (networkVelocity * lag);

            // Suavizamos hacia la posición extrapolada
            transform.position = Vector3.MoveTowards(transform.position,
                extrapolatedPosition, Time.deltaTime * 20f);
        }
    }
}
