using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class EstacionResurreccion : MonoBehaviourPun
{
    [Header("Configuración")]
    public string nombrePrefabJugador = "Player";

    private bool yaUsada = false;

    void OnTriggerStay(Collider other)
    {
        if (yaUsada) return;

        if (other.CompareTag("Player"))
        {
            HealthSystem hs = other.GetComponent<HealthSystem>();

            if (hs != null && hs.photonView.IsMine)
            {
                if (hs.chapasRecogidas.Count > 0)
                {
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.MostrarTextoExtraccion("Pulsa [E] para revivir agente", Color.cyan);
                    }

                    if (Input.GetKey(KeyCode.E))
                    {
                        yaUsada = true;
                        if (HUDManager.Instance != null) HUDManager.Instance.OcultarTextoExtraccion();

                        // Rescatamos SOLO AL PRIMERO de la lista (Índice 0)
                        int actorARevivir = hs.chapasRecogidas[0];
                        photonView.RPC("RPC_ActivarEstacion", RpcTarget.MasterClient, hs.photonView.ViewID, actorARevivir);
                    }
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.GetComponent<PhotonView>().IsMine)
        {
            if (HUDManager.Instance != null) HUDManager.Instance.OcultarTextoExtraccion();
        }
    }

    [PunRPC]
    public void RPC_ActivarEstacion(int viewIdSalvador, int actorARevivir)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Player agenteCaido = PhotonNetwork.CurrentRoom.GetPlayer(actorARevivir);

        // Si el jugador sigue conectado en la sala, lo revivimos
        if (agenteCaido != null)
        {
            photonView.RPC("RPC_RespawnAgente", agenteCaido, transform.position, nombrePrefabJugador);
        }

        // Le quitamos esa chapa de la mochila al salvador
        PhotonView salvador = PhotonView.Find(viewIdSalvador);
        if (salvador != null)
        {
            salvador.RPC("RPC_RemoverPrimeraChapa", RpcTarget.All);
        }

        // La estación se funde (1 solo uso)
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    public void RPC_RespawnAgente(Vector3 posicionRescate, string prefabName)
    {
        GhostCamera gc = FindObjectOfType<GhostCamera>();
        if (gc != null) Destroy(gc.gameObject);

        PhotonNetwork.Instantiate(prefabName, posicionRescate + (Vector3.up * 1f), Quaternion.identity);
    }
}