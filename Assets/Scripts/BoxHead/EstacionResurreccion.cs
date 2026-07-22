using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;

public class EstacionResurreccion : MonoBehaviourPun
{
    [Header("Configuración")]
    public string nombrePrefabJugador = "Player";
    private bool yaUsada = false;
    private bool rescateEjecutado = false; // Candado de red

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
                    int actorARevivir = hs.chapasRecogidas[0];

                    // NUEVO: Buscamos el nombre del agente muerto
                    string nombreCaido = "Agente " + actorARevivir;
                    Player agenteMuerto = PhotonNetwork.CurrentRoom.GetPlayer(actorARevivir);
                    if (agenteMuerto != null && !string.IsNullOrEmpty(agenteMuerto.NickName))
                    {
                        nombreCaido = agenteMuerto.NickName;
                    }

                    if (HUDManager.Instance != null)
                    {
                        // MOSTRAMOS EL NOMBRE EN LA UI CENTRAL
                        HUDManager.Instance.MostrarTextoExtraccion("Pulsa [E] para revivir a " + nombreCaido, Color.cyan);
                    }

                    if (Input.GetKey(KeyCode.E))
                    {
                        yaUsada = true;
                        if (HUDManager.Instance != null) HUDManager.Instance.OcultarTextoExtraccion();

                        photonView.RPC("RPC_ApagarEstacion", RpcTarget.AllBuffered);
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
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        MeshRenderer mesh = GetComponent<MeshRenderer>();
        if (mesh != null) mesh.enabled = false;
    }

    [PunRPC]
    public void RPC_ProcesarRescate(int viewIdSalvador, int actorARevivir)
    {
        // Si no somos el host, o si el totem ya procesó un cuerpo, cortamos acá
        if (!PhotonNetwork.IsMasterClient || rescateEjecutado) return;

        rescateEjecutado = true; // Cerramos el candado para que no haya clones

        Player agenteCaido = PhotonNetwork.CurrentRoom.GetPlayer(actorARevivir);

        if (agenteCaido != null)
        {
            photonView.RPC("RPC_RespawnAgente", agenteCaido, transform.position, nombrePrefabJugador);
        }

        PhotonView salvador = PhotonView.Find(viewIdSalvador);
        if (salvador != null)
        {
            salvador.RPC("RPC_RemoverPrimeraChapa", RpcTarget.All);
        }
    }

    [PunRPC]
    public void RPC_RespawnAgente(Vector3 posicionRescate, string prefabName)
    {
        GhostCamera gc = FindObjectOfType<GhostCamera>();

        // CORRECCIÓN MAGISTRAL: Borramos el componente, NO el GameObject entero.
        if (gc != null) Destroy(gc);

        PhotonNetwork.Instantiate(prefabName, posicionRescate + (Vector3.up * 1f), Quaternion.identity);
    }
}