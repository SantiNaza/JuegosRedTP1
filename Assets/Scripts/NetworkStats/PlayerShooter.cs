using UnityEngine;
using Photon.Pun;
using TMPro; // Asegúrate de tener configurado TextMeshPro para la UI

public class PlayerShooter : MonoBehaviourPun
{
    [Header("Configuración de Disparo")]
    public float fireRate = 2f; // Fire rate lento
    private float nextFireTime = 0f;
    public Camera playerCamera;

    [Header("UI")]
    public TextMeshProUGUI coordinatesUI; // Asignar un Text de UI en la escena

    [Header("Modo de Autoridad")]
    [Tooltip("True = Cliente decide / False = Master decide")]
    public bool clientAuthorityMode = true; 

    void Update()
    {
        if (!photonView.IsMine) return;

        // Lectura clásica: GetMouseButtonDown(0) es el clic izquierdo
        if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;

            if (clientAuthorityMode)
            {
                ShootClientAuthority();
            }
            else
            {
                ShootMasterAuthority();
            }
        }
    }

    // ==========================================
    // CAMINO 1: EL CLIENTE DECIDE (Client Authority)
    // ==========================================
    private void ShootClientAuthority()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // El cliente hace el Raycast localmente
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Target")) // Asegúrate de que la Diana tenga el tag "Target"
            {
                // El cliente decide que le pegó y le avisa al Master Client
                photonView.RPC("RPC_NotifyMasterOfHit", RpcTarget.MasterClient, hit.point, hit.collider.gameObject.GetPhotonView().ViewID);
            }
        }
    }

    [PunRPC]
    private void RPC_NotifyMasterOfHit(Vector3 hitPoint, int targetViewID)
    {
        // Solo el Master ejecuta esto
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            targetView.GetComponent<TargetController>().StopTarget();
            
            // El master informa a todos para actualizar la UI
            photonView.RPC("RPC_UpdateUI", RpcTarget.All, hitPoint);
        }
    }

    // ==========================================
    // CAMINO 2: EL MASTER DECIDE (Server/Master Authority)
    // ==========================================
    private void ShootMasterAuthority()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // El cliente NO hace Raycast. Solo envía su posición y dirección al Master.
        photonView.RPC("RPC_RequestMasterToShoot", RpcTarget.MasterClient, ray.origin, ray.direction);
    }

    [PunRPC]
    private void RPC_RequestMasterToShoot(Vector3 origin, Vector3 direction)
    {
        // El Master Client realiza el Raycast real
        if (Physics.Raycast(origin, direction, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Target"))
            {
                hit.collider.GetComponent<TargetController>().StopTarget();
                
                // Si el Master confirma el hit, actualiza la UI de todos
                photonView.RPC("RPC_UpdateUI", RpcTarget.All, hit.point);
            }
        }
    }

    // ==========================================
    // ACTUALIZACIÓN DE UI (Común para ambos)
    // ==========================================
    [PunRPC]
    private void RPC_UpdateUI(Vector3 hitCoordinates)
    {
        if (coordinatesUI != null)
        {
            coordinatesUI.text = $"¡Impacto!\nCoordenadas: X:{hitCoordinates.x:F2}, Y:{hitCoordinates.y:F2}, Z:{hitCoordinates.z:F2}";
        }
    }
}