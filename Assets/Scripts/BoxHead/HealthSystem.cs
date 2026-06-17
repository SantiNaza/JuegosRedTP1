using UnityEngine;
using Photon.Pun;

public class HealthSystem : MonoBehaviourPun
{
    public float maxHealth = 100f;
    public float currentHealth; // Antes era private
    public bool destroyOnDeath = true;

    [Header("Configuración de Jugador (Revivir)")]
    public bool isPlayer = false;
    public bool isDowned = false;

    // NUEVO: Variables para el control de tiempo
    public float maxBleedOutTime = 15f;
    private float bleedOutTimer;
    private float reviveTimer = 0f;
    public float timeRequiredToRevive = 3f;
    public float reviveRadius = 2f;

    // NUEVO: Variables para el texto 3D
    private GameObject countdownTextObj;
    private TextMesh countdownTextMesh;
    private int lastDisplayedTime = -1; // Para no saturar la red mandando el mismo número

    void Start()
    {
        currentHealth = maxHealth;

        // Creamos el objeto de texto flotante al arrancar la partida
        if (isPlayer)
        {
            photonView.RPC("RPC_CrearTextoCuentaRegresiva", RpcTarget.AllBuffered);
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (isPlayer && isDowned)
        {
            // Verificamos si alguien nos está tocando/reviviendo
            bool isBeingRevived = CheckForRevive();

            if (isBeingRevived)
            {
                // MECÁNICA DE ESTABILIZACIÓN: Mientras nos tocan, el tiempo vuelve al máximo
                bleedOutTimer = maxBleedOutTime;

                // Actualizamos el cartel para que diga que nos están curando
                ActualizarTextoRed(true, 0, true);
            }
            else
            {
                // Lógica normal de desangrado
                bleedOutTimer -= Time.deltaTime;

                // Redondeamos para arriba (ej: 29.8 se muestra como 30)
                int segundosActuales = Mathf.CeilToInt(bleedOutTimer);

                // Solo mandamos el RPC si el segundo entero cambió
                if (segundosActuales != lastDisplayedTime)
                {
                    lastDisplayedTime = segundosActuales;
                    ActualizarTextoRed(true, segundosActuales, false);
                }

                if (bleedOutTimer <= 0)
                {
                    ActualizarTextoRed(false, 0, false); // Apagamos el texto
                    Die();
                    return;
                }
            }
        }
    }

    // --- MÉTODOS DEL TEXTO 3D ---

    [PunRPC]
    private void RPC_CrearTextoCuentaRegresiva()
    {
        countdownTextObj = new GameObject("TextoDesangrado");
        countdownTextObj.transform.SetParent(this.transform);
        countdownTextObj.transform.localPosition = new Vector3(0f, 2f, 0f);

        countdownTextMesh = countdownTextObj.AddComponent<TextMesh>();
        countdownTextMesh.text = "";
        countdownTextMesh.characterSize = 0.15f;
        countdownTextMesh.fontSize = 45;
        countdownTextMesh.anchor = TextAnchor.MiddleCenter;
        countdownTextMesh.alignment = TextAlignment.Center;

        countdownTextObj.SetActive(false);
    }

    private void ActualizarTextoRed(bool mostrar, int segundos, bool estabilizando)
    {
        // El dueño le avisa a todos qué mostrar en su cartel
        photonView.RPC("RPC_ActualizarTextoCaido", RpcTarget.All, mostrar, segundos, estabilizando);
    }

    [PunRPC]
    public void RPC_ActualizarTextoCaido(bool mostrar, int segundosRestantes, bool estabilizando)
    {
        if (countdownTextObj != null)
        {
            countdownTextObj.SetActive(mostrar);
            if (mostrar)
            {
                if (estabilizando)
                {
                    countdownTextMesh.text = "¡ESTABILIZANDO!";
                    countdownTextMesh.color = Color.cyan;
                }
                else
                {
                    countdownTextMesh.text = segundosRestantes.ToString();
                    // Si quedan menos de 10 segundos, lo ponemos rojo oscuro, si no, rojo normal
                    countdownTextMesh.color = segundosRestantes <= 10 ? new Color(0.6f, 0f, 0f) : Color.red;
                }
            }
        }
    }

    private void LateUpdate()
    {
        // Evita que el número gire cuando el jugador rota en el piso
        if (countdownTextObj != null && countdownTextObj.activeSelf && Camera.main != null)
        {
            countdownTextObj.transform.rotation = Camera.main.transform.rotation;
        }
    }

    // --- LÓGICA DE DAÑO Y REVIVIR ---

    [PunRPC]
    public void RPC_TakeDamage(float damage)
    {
        if (isDowned) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            if (isPlayer)
            {
                photonView.RPC("RPC_EnterDownedState", RpcTarget.All);
            }
            else
            {
                Die();
            }
        }
    }

    [PunRPC]
    private void RPC_EnterDownedState()
    {
        isDowned = true;
        bleedOutTimer = maxBleedOutTime;
        reviveTimer = 0f;
        lastDisplayedTime = -1; // Reseteamos el control del texto

        transform.eulerAngles = new Vector3(90f, transform.eulerAngles.y, transform.eulerAngles.z);

        gameObject.tag = "Untagged";

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (photonView.IsMine)
        {
            GetComponent<PlayerMovement>().enabled = false;
            GetComponent<TopDownWeaponController>().enabled = false;
        }
    }

    private bool CheckForRevive()
    {
        bool isBeingRevived = false;

        Collider[] colliders = Physics.OverlapSphere(transform.position, reviveRadius);
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player") && col.gameObject != this.gameObject)
            {
                HealthSystem allyHealth = col.GetComponent<HealthSystem>();

                if (allyHealth != null && !allyHealth.isDowned)
                {
                    isBeingRevived = true;
                    break;
                }
            }
        }

        if (isBeingRevived)
        {
            reviveTimer += Time.deltaTime;

            if (reviveTimer >= timeRequiredToRevive)
            {
                photonView.RPC("RPC_Revive", RpcTarget.All);
            }
        }
        else
        {
            reviveTimer = 0f;
        }

        return isBeingRevived; // Devolvemos el estado para que el Update sepa si estabilizar
    }

    [PunRPC]
    private void RPC_Revive()
    {
        isDowned = false;
        currentHealth = maxHealth / 2;

        // Apagamos el texto flotante al levantarnos
        if (countdownTextObj != null) countdownTextObj.SetActive(false);

        transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, transform.eulerAngles.z);

        gameObject.tag = "Player";

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

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
            if (!isPlayer)
            {
                WaveManager waveManager = FindObjectOfType<WaveManager>();
                if (waveManager != null)
                {
                    waveManager.ZombieDied();
                }
            }
            else
            {
                // 1. Liberamos la cámara (Nos hacemos fantasmas)
                if (Camera.main != null)
                {
                    Camera.main.gameObject.AddComponent<GhostCamera>();
                }

                // 2. ENVIAMOS LA INFORMACIÓN A LA API
                API_LaOrden api = FindObjectOfType<API_LaOrden>();
                if (api != null)
                {
                    // Por ahora hardcodeamos las kills en 15 para probar que funciona.
                    // PhotonNetwork.NickName agarra el nombre que el jugador se puso en el lobby.
                    api.EnviarReporteMuerte(PhotonNetwork.NickName, 15);
                }
            }

            PhotonNetwork.Destroy(gameObject);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reviveRadius);
    }
}