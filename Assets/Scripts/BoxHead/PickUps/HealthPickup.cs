using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class HealthPickup : MonoBehaviourPun
{
    [Header("Configuración")]
    public float healAmount = 50f;
    public float respawnTime = 60f;
    public float startupGraceTime = 1f; // tiempo sin poder recogerse al spawnear

    [Header("Referencias")]
    public GameObject visual;
    public Collider triggerCollider;

    private bool isAvailable = true;

    void Awake()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
    }

    void Start()
    {
        // 1. Forzamos el estado inicial correcto (por si el prefab quedó raro)
        isAvailable = true;
        if (visual != null) visual.SetActive(true);

        // 2. Bloqueamos la recogida durante el spawneo para que NO se auto-recoja
        if (triggerCollider != null) triggerCollider.enabled = false;
        Invoke(nameof(HabilitarTrigger), startupGraceTime);

        // DEBUG: ver el estado real al spawnear
        Renderer r = visual != null ? visual.GetComponentInChildren<Renderer>(true) : null;
        Debug.Log($"[HealthPickup] Spawneado. visual activo: {(visual != null ? visual.activeSelf.ToString() : "NULL")} | " +
                  $"renderer enabled: {(r != null ? r.enabled.ToString() : "NULL")} | " +
                  $"pos: {transform.position} | scale: {transform.lossyScale}");
    }

    private void HabilitarTrigger()
    {
        if (isAvailable && triggerCollider != null)
            triggerCollider.enabled = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isAvailable) return;
        if (!other.CompareTag("Player")) return;

        PhotonView playerView = other.GetComponentInParent<PhotonView>();
        if (playerView == null || !playerView.IsMine) return;
        if (photonView.ViewID == 0) return;

        photonView.RPC(nameof(RPC_RequestPickup), RpcTarget.MasterClient, playerView.Owner.ActorNumber);
    }

    [PunRPC]
    private void RPC_RequestPickup(int requesterActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!isAvailable) return;

        isAvailable = false;
        photonView.RPC(nameof(RPC_ConfirmPickup), RpcTarget.All, requesterActorNumber);
    }

    [PunRPC]
    private void RPC_ConfirmPickup(int healedActorNumber)
    {
        isAvailable = false;
        SetVisualActive(false);

        if (PhotonNetwork.LocalPlayer.ActorNumber == healedActorNumber)
        {
            HealthSystem myHealth = GetLocalPlayerHealth();
            if (myHealth != null)
                myHealth.Heal(healAmount);
        }

        if (PhotonNetwork.IsMasterClient)
            Invoke(nameof(RespawnByMaster), respawnTime);
    }

    private void RespawnByMaster()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        isAvailable = true;
        photonView.RPC(nameof(RPC_Respawn), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_Respawn()
    {
        isAvailable = true;
        SetVisualActive(true);
    }

    private void SetVisualActive(bool active)
    {
        if (visual != null) visual.SetActive(active);
        if (triggerCollider != null) triggerCollider.enabled = active;
    }

    private HealthSystem GetLocalPlayerHealth()
    {
        foreach (HealthSystem hs in FindObjectsOfType<HealthSystem>())
        {
            if (hs.isPlayer && hs.photonView.IsMine)
                return hs;
        }
        return null;
    }
}