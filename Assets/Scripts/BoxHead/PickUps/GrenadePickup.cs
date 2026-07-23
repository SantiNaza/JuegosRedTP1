using UnityEngine;
using Photon.Pun;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;

[RequireComponent(typeof(PhotonView))]
public class GrenadePickup : MonoBehaviourPun
{
    [Header("Configuración")]
    public int granadasQueDa = 1;
    public float respawnTime = 60f;
    public float startupGraceTime = 1f; // Evita auto-recogida[cite: 20]

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
        isAvailable = true;
        if (visual != null) visual.SetActive(true);

        if (triggerCollider != null) triggerCollider.enabled = false;
        Invoke(nameof(HabilitarTrigger), startupGraceTime);
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
            TopDownWeaponController weapon = GetLocalWeaponController();
            if (weapon != null)
            {
                weapon.granadasActuales += granadasQueDa;
                Debug.Log("Granada recogida. Granadas actuales: " + weapon.granadasActuales);
            }
        }

        if (PhotonNetwork.IsMasterClient)
        {
            // LEEMOS LIVEOPS ANTES DE INICIAR EL RELOJ
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                respawnTime = RemoteConfigService.Instance.appConfig.GetFloat("respawnGrenade", respawnTime);
            }
            Invoke(nameof(RespawnByMaster), respawnTime);
        }
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

    private TopDownWeaponController GetLocalWeaponController()
    {
        foreach (HealthSystem hs in FindObjectsOfType<HealthSystem>())
        {
            if (hs.isPlayer && hs.photonView.IsMine)
                return hs.GetComponent<TopDownWeaponController>();
        }
        return null;
    }
}