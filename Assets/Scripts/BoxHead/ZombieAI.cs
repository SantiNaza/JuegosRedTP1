using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using System.Collections;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviourPun
{
    private NavMeshAgent agent;
    private Transform closestPlayer;

    [Header("Configuración LiveOps")]
    [Tooltip("Escribí 'Normal', 'Fast' o 'Tank' para que busque sus variables en la nube")]
    public string prefijoLiveOps = "Normal";

    [Header("Configuración de Ataque")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f; // Se sobreescribirá con LiveOps
    public float attackCooldown = 1f;
    private float nextAttackTime = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // 1. TODOS los clientes actualizan los stats visuales y de vida del zombi
        AplicarAtributosDeLiveOps();

        // 2. Lógica exclusiva de movimiento (Solo Host)
        if (!PhotonNetwork.IsMasterClient)
        {
            if (agent != null) agent.enabled = false;
            return;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    private void AplicarAtributosDeLiveOps()
    {
        // Si no hay internet o servicios, se queda con los valores fijos de tu Inspector
        if (UnityServices.State != ServicesInitializationState.Initialized) return;

        // Armamos el nombre de la llave combinando el prefijo (Ej: "Normal" + "_HP" = "Normal_HP")
        float liveHp = RemoteConfigService.Instance.appConfig.GetFloat(prefijoLiveOps + "_HP", GetComponent<HealthSystem>().maxHealth);
        float liveDamage = RemoteConfigService.Instance.appConfig.GetFloat(prefijoLiveOps + "_Damage", attackDamage);

        // Buscamos la velocidad. Si falla, usa la que tenga el NavMeshAgent actualmente
        float currentSpeed = agent != null ? agent.speed : 1.5f;
        float liveSpeed = RemoteConfigService.Instance.appConfig.GetFloat(prefijoLiveOps + "_Speed", currentSpeed);

        // --- APLICAMOS LOS CAMBIOS ---

        // 1. Daño
        attackDamage = liveDamage;

        // 2. Vida
        HealthSystem healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.maxHealth = liveHp;
            healthSystem.currentHealth = liveHp;
        }

        // 3. Velocidad de Movimiento (Solo importa para el Master Client que lo mueve)
        if (agent != null && PhotonNetwork.IsMasterClient)
        {
            agent.speed = liveSpeed;
        }
    }

    void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        FindClosestPlayer();

        if (closestPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, closestPlayer.position);

            if (distance <= attackRange)
            {
                agent.isStopped = true;

                Vector3 lookPos = new Vector3(closestPlayer.position.x, transform.position.y, closestPlayer.position.z);
                transform.LookAt(lookPos);

                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    AtacarJugador(closestPlayer);
                }
            }
            else
            {
                agent.isStopped = false;
                agent.SetDestination(closestPlayer.position);
            }
        }
    }

    private void AtacarJugador(Transform playerTransform)
    {
        HealthSystem playerHealth = playerTransform.GetComponent<HealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.photonView.RPC("RPC_TakeDamage", RpcTarget.All, attackDamage, 0);
        }
    }

    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float minDistance = Mathf.Infinity;
        closestPlayer = null;

        foreach (GameObject player in players)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestPlayer = player.transform;
            }
        }
    }

    [PunRPC]
    public void RPC_ApplyKnockback(Vector3 knockbackForce)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector3 force)
    {
        float duration = 0.2f;
        float time = 0;

        if (agent.isOnNavMesh) agent.isStopped = true;

        while (time < duration)
        {
            if (agent.isOnNavMesh)
            {
                agent.Move(force * (Time.deltaTime / duration));
            }
            time += Time.deltaTime;
            yield return null;
        }

        if (agent.isOnNavMesh) agent.isStopped = false;
    }
}