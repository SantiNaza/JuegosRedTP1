using UnityEngine;
using Photon.Pun;

public class HealthSystem : MonoBehaviourPun
{
    public float maxHealth = 100f;
    private float currentHealth;
    public bool destroyOnDeath = true;

    [Header("Configuración de Jugador (Revivir)")]
    public bool isPlayer = false; // Marcar en TRUE solo en el Prefab del jugador
    public bool isDowned = false;
    private float bleedOutTimer = 15f;
    private float reviveTimer = 0f;
    public float timeRequiredToRevive = 3f;
    public float reviveRadius = 2f; // Distancia a la que debe estar el compañero

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        // Solo el dueño del jugador procesa sus propios contadores
        if (!photonView.IsMine) return;

        if (isPlayer && isDowned)
        {
            // 1. Lógica de desangrado (30 segundos)
            bleedOutTimer -= Time.deltaTime;
            if (bleedOutTimer <= 0)
            {
                Die(); // Pasaron 30 segundos, muere definitivamente
                return;
            }

            // 2. Lógica de ser revivido
            CheckForRevive();
        }
    }

    [PunRPC]
    public void RPC_TakeDamage(float damage)
    {
        // Si el jugador ya está en el piso, ignoramos más daño
        if (isDowned) return; 

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            if (isPlayer)
            {
                // Entramos en estado "Caído" en todas las pantallas
                photonView.RPC("RPC_EnterDownedState", RpcTarget.All);
            }
            else
            {
                // Es un zombie o caja, muere directo
                Die();
            }
        }
    }

    [PunRPC]
    private void RPC_EnterDownedState()
    {
        isDowned = true;
        bleedOutTimer = 30f;
        reviveTimer = 0f;

        // Visual: Acostamos al personaje en el piso
        transform.eulerAngles = new Vector3(90f, transform.eulerAngles.y, transform.eulerAngles.z);

        // NUEVO: Congelar al jugador y volverlo "invisible" para los zombies
        gameObject.tag = "Untagged"; // Los zombies ya no lo detectarán
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero; // Frenamos cualquier inercia de caída o movimiento
            rb.isKinematic = true;      // Apagamos las reacciones físicas para que NO ruede
        }

        // Desactivamos el movimiento y el disparo para el dueño
        if (photonView.IsMine)
        {
            GetComponent<PlayerMovement>().enabled = false;
            GetComponent<TopDownWeaponController>().enabled = false;
        }
    }

    private void CheckForRevive()
    {
        bool isBeingRevived = false;

        // Buscamos si hay otro jugador parado muy cerca
        Collider[] colliders = Physics.OverlapSphere(transform.position, reviveRadius);
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player") && col.gameObject != this.gameObject)
            {
                HealthSystem allyHealth = col.GetComponent<HealthSystem>();
                
                // Si encontramos a un aliado y NO está caído también
                if (allyHealth != null && !allyHealth.isDowned)
                {
                    isBeingRevived = true;
                    break;
                }
            }
        }

        // Si el compañero está encima, sumamos tiempo
        if (isBeingRevived)
        {
            reviveTimer += Time.deltaTime;
            
            if (reviveTimer >= timeRequiredToRevive)
            {
                // ¡Llegó a los 5 segundos! Revivimos a través de la red
                photonView.RPC("RPC_Revive", RpcTarget.All);
            }
        }
        else
        {
            // Si el compañero se aleja para disparar o esquivar, el progreso se pierde
            reviveTimer = 0f;
        }
    }

    [PunRPC]
    private void RPC_Revive()
    {
        isDowned = false;
        currentHealth = maxHealth / 2; // Revive con el 50% de la vida

        // Visual: Lo volvemos a poner de pie
        transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, transform.eulerAngles.z);

        // NUEVO: Restaurar las físicas y volver a ser objetivo de los zombies
        gameObject.tag = "Player"; // Los zombies volverán a perseguirlo
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Volvemos a encender las físicas y la gravedad
        }

        // Reactivamos sus controles
        if (photonView.IsMine)
        {
            GetComponent<PlayerMovement>().enabled = true;
            GetComponent<TopDownWeaponController>().enabled = true;
        }
    }

    private void Die()
    {
        if (destroyOnDeath && photonView.IsMine)
        {
            // Solo avisamos al WaveManager si NO es un jugador
            if (!isPlayer)
            {
                WaveManager waveManager = FindObjectOfType<WaveManager>();
                if (waveManager != null)
                {
                    waveManager.ZombieDied();
                }
            }

            PhotonNetwork.Destroy(gameObject);
        }
    }

    // Dibuja el radio de revivir en el editor para ajustarlo fácil
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reviveRadius);
    }
}