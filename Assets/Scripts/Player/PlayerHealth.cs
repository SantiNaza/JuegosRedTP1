using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

[RequireComponent(typeof(PhotonView))]
public class PlayerHealth : MonoBehaviourPun, IOnEventCallback
{
    public static event Action<int> OnAnyPlayerDeath;

    private const byte PlayerDeathEventCode = 1;

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerMouseAim playerMouseAim;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private Rigidbody playerRigidbody;

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerMouseAim == null)
            playerMouseAim = GetComponent<PlayerMouseAim>();

        if (playerAttack == null)
            playerAttack = GetComponent<PlayerAttack>();

        if (playerCollider == null)
            playerCollider = GetComponent<Collider>();

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start()
    {
        Debug.Log("Vida inicial: " + currentHealth);
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f)
            return;

        if (isDead)
            return;

        PhotonView view = PhotonView.Get(this);

        view.RPC("RPC_TakeDamage", RpcTarget.All, damage);
    }

    [PunRPC]
    public void RPC_TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log("Vida actual: " + currentHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Debug.Log("Player muerto: " + photonView.Owner.ActorNumber);

        DisablePlayer();

        if (photonView.IsMine)
        {
            RaisePlayerDeathEvent();

            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void DisablePlayer()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerMouseAim != null)
            playerMouseAim.enabled = false;

        if (playerAttack != null)
            playerAttack.enabled = false;

        if (playerCollider != null)
            playerCollider.enabled = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }
    }

    private void RaisePlayerDeathEvent()
    {
        object[] content = new object[]
        {
            photonView.Owner.ActorNumber
        };

        RaiseEventOptions raiseEventOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(
            PlayerDeathEventCode,
            content,
            raiseEventOptions,
            SendOptions.SendReliable
        );
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;

        if (eventCode == PlayerDeathEventCode)
        {
            object[] data = (object[])photonEvent.CustomData;

            int deadPlayerActorNumber = (int)data[0];

            Debug.Log("RaiseEvent recibido. Murió el jugador: " + deadPlayerActorNumber);

            OnAnyPlayerDeath?.Invoke(deadPlayerActorNumber);
        }
    }
}