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
                        HUDManager.Instance.MostrarTextoExtraccion("Pulsa [E] para pedir rescate", Color.cyan);
                    }

                    if (Input.GetKey(KeyCode.E))
                    {
                        yaUsada = true;
                        if (HUDManager.Instance != null) HUDManager.Instance.OcultarTextoExtraccion();

                        int actorARevivir = hs.chapasRecogidas[0];

                        // 1. Apagamos la estación AL INSTANTE para que nadie más la toque
                        photonView.RPC("RPC_ApagarEstacion", RpcTarget.AllBuffered);

                        // 2. Le pedimos al Master que ejecute la revivición
                        photonView.RPC("RPC_ProcesarRescate", RpcTarget.MasterClient, hs.photonView.ViewID, actorARevivir);
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
    public void RPC_ApagarEstacion()
    {
        yaUsada = true;

        // Apagamos las físicas para que no se pueda chocar
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Apagamos los gráficos para que desaparezca visualmente
        MeshRenderer mesh = GetComponent<MeshRenderer>();
        if (mesh != null) mesh.enabled = false;
    }

    [PunRPC]
    public void RPC_ProcesarRescate(int viewIdSalvador, int actorARevivir)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Buscamos al jugador fantasma
        Player agenteCaido = PhotonNetwork.CurrentRoom.GetPlayer(actorARevivir);

        if (agenteCaido != null)
        {
            // Le mandamos la orden DIRECTO a la computadora del fantasma
            photonView.RPC("RPC_RespawnAgente", agenteCaido, transform.position, nombrePrefabJugador);
        }

        // Le quitamos la chapa al salvador
        PhotonView salvador = PhotonView.Find(viewIdSalvador);
        if (salvador != null)
        {
            salvador.RPC("RPC_RemoverPrimeraChapa", RpcTarget.All);
        }
    }

    [PunRPC]
    public void RPC_RespawnAgente(Vector3 posicionRescate, string prefabName)
    {
        // ¡ESTO LO EJECUTA LA COMPUTADORA DEL JUGADOR MUERTO!
        GhostCamera gc = FindObjectOfType<GhostCamera>();
        if (gc != null) Destroy(gc.gameObject);

        // Creamos nuestro nuevo cuerpo
        PhotonNetwork.Instantiate(prefabName, posicionRescate + (Vector3.up * 1f), Quaternion.identity);
    }
}