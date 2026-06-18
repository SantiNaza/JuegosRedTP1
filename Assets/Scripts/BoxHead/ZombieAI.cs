using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviourPun
{
    private NavMeshAgent agent;
    private Transform closestPlayer;

    [Header("Configuración de Ataque")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f; // Cuánto daño hace por golpe
    public float attackCooldown = 1f; // Espera 1 segundo entre cada golpe
    private float nextAttackTime = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Solo el Master Client prende el agente y calcula la IA
        if (PhotonNetwork.IsMasterClient)
        {
            agent.enabled = true;
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
                // El zombie llegó al jugador
                agent.isStopped = true;
                
                // Hacemos que mire al jugador mientras lo ataca
                Vector3 lookPos = new Vector3(closestPlayer.position.x, transform.position.y, closestPlayer.position.z);
                transform.LookAt(lookPos);

                // Atacamos si el tiempo de enfriamiento (cooldown) ya pasó
                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    AtacarJugador(closestPlayer);
                }
            }
            else
            {
                // El zombie sigue persiguiendo
                agent.isStopped = false;
                agent.SetDestination(closestPlayer.position);
            }
        }
    }

    private void AtacarJugador(Transform playerTransform)
    {
        // Buscamos el componente de vida del jugador
        HealthSystem playerHealth = playerTransform.GetComponent<HealthSystem>();
        
        if (playerHealth != null)
        {
            // Le enviamos el daño a través de la red
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

    // ==========================================
    // SISTEMA DE EMPUJE (KNOCKBACK)
    // ==========================================
    [PunRPC]
    public void RPC_ApplyKnockback(Vector3 knockbackForce)
    {
        // Solo el Master Client controla los movimientos físicos de los zombies
        if (!PhotonNetwork.IsMasterClient) return;
        
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector3 force)
    {
        float duration = 0.2f;
        float time = 0;

        // Pausamos su IA momentáneamente para que no se resista al empuje
        if (agent.isOnNavMesh) agent.isStopped = true;

        while (time < duration)
        {
            // Usamos agent.Move() para que el NavMesh respete las paredes y no lo empujemos a través de ellas
            if (agent.isOnNavMesh)
            {
                agent.Move(force * (Time.deltaTime / duration));
            }
            time += Time.deltaTime;
            yield return null;
        }

        // Reanudamos la persecución
        if (agent.isOnNavMesh) agent.isStopped = false;
    }
}