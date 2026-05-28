using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class LatenyaSerializeView : MonoBehaviourPunCallbacks, IPunObservable
{

    private Vector3 networkPosition;
    
    private void Update()
    {
        if (photonView.IsMine)
        {
            Vector3 v = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            v *= Time.deltaTime;
            transform.position += v;
        }
        else
        {
            // Suaviza el movimiento hacia la última posición de red recibida
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Enviamos datos normalmente
            stream.SendNext(transform.position);
        }
        else
        {
            // Recibimos datos
            networkPosition = (Vector3)stream.ReceiveNext();
        
            // info.SentServerTime nos dice CUÁNDO se envió el paquete según el reloj de Photon
            double latency = PhotonNetwork.Time - info.SentServerTime; 
        
            // Convertimos a milisegundos para mostrar en consola o UI
            float latencyInMs = (float)latency * 1000f;
        
            Debug.Log($"El paquete tardó {latencyInMs}ms en llegar desde el emisor.");
        }
    }
}
