using UnityEngine;
using Photon.Pun;

public class ChapaAgente : MonoBehaviourPun
{
    private int actorNumberDueno = -1;
    private bool yaRecogida = false;

    [PunRPC]
    public void RPC_ConfigurarChapa(int actorNum)
    {
        actorNumberDueno = actorNum;
    }

    void OnTriggerEnter(Collider other)
    {
        if (yaRecogida) return;

        if (other.CompareTag("Player"))
        {
            PhotonView playerView = other.GetComponent<PhotonView>();

            // Solo el jugador que la pisa ejecuta esto en su máquina
            if (playerView != null && playerView.IsMine)
            {
                // Seguridad: No podés agarrar tu propia chapa
                if (actorNumberDueno == PhotonNetwork.LocalPlayer.ActorNumber) return;

                yaRecogida = true;

                // 1. Nos guardamos la chapa en la mochila
                playerView.RPC("RPC_RecogerChapa", RpcTarget.All, actorNumberDueno);

                // 2. MAGIA DE RED: Le enviamos la orden de destrucción SOLO AL DUEÑO de la chapa
                photonView.RPC("RPC_DestruirChapa", photonView.Owner);
            }
        }
    }

    [PunRPC]
    public void RPC_DestruirChapa()
    {
        // Como este mensaje lo recibe el dueño original, Photon lo deja destruirlo sin tirar errores
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}