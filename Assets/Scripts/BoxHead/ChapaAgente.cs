using UnityEngine;
using Photon.Pun;

public class ChapaAgente : MonoBehaviourPun
{
    private int actorNumberDueño;
    private bool yaRecogida = false;

    [PunRPC]
    public void RPC_ConfigurarChapa(int actorNum)
    {
        actorNumberDueño = actorNum;
    }

    void OnTriggerEnter(Collider other)
    {
        if (yaRecogida) return;

        PhotonView playerView = other.GetComponent<PhotonView>();

        // El que la agarra tiene que ser el jugador local, y obviamente no puede agarrar su propia chapa
        if (playerView != null && playerView.IsMine && other.CompareTag("Player") && actorNumberDueño != PhotonNetwork.LocalPlayer.ActorNumber)
        {
            yaRecogida = true;

            playerView.RPC("RPC_RecogerChapa", RpcTarget.All, actorNumberDueño);
            photonView.RPC("RPC_DestruirChapa", RpcTarget.MasterClient);
        }
    }

    [PunRPC]
    public void RPC_DestruirChapa()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}