using UnityEngine;
using Photon.Pun;

public class HealthSystem : MonoBehaviourPun
{
    public float maxHealth = 100f;
    public float currentHealth;
    public bool destroyOnDeath = true;

    [Header("Configuración de Jugador (Revivir)")]
    public bool isPlayer = false;
    public bool isDowned = false;

    // NUEVO: Variable sincronizada por red para saber si aprieta la E
    public bool isPressingE = false;

    public float maxBleedOutTime = 15f;
    private float bleedOutTimer;
    private float reviveTimer = 0f;
    public float timeRequiredToRevive = 3f;
    public float reviveRadius = 2f;

    private GameObject countdownTextObj;
    private TextMesh countdownTextMesh;
    private string lastDisplayedText = ""; // Para no saturar la red mandando el mismo texto

    void Start()
    {
        currentHealth = maxHealth;

        if (isPlayer)
        {
            photonView.RPC("RPC_CrearTextoCuentaRegresiva", RpcTarget.AllBuffered);
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // 1. Si estamos VIVOS, leemos el teclado para que el dato viaje por red
        if (isPlayer && !isDowned)
        {
            isPressingE = Input.GetKey(KeyCode.E);
        }

        // 2. Si estamos CAÍDOS, evaluamos nuestro entorno
        if (isPlayer && isDowned)
        {
            ManejarEstadoCaido();
        }
    }

    private void ManejarEstadoCaido()
    {
        bool isSomeoneNear = false;
        bool isSomeonePressingE = false;

        Collider[] colliders = Physics.OverlapSphere(transform.position, reviveRadius);
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player") && col.gameObject != this.gameObject)
            {
                HealthSystem allyHealth = col.GetComponent<HealthSystem>();

                if (allyHealth != null && !allyHealth.isDowned)
                {
                    isSomeoneNear = true;

                    // Acá verificamos la variable de red del compañero
                    if (allyHealth.isPressingE)
                    {
                        isSomeonePressingE = true;
                        break;
                    }
                }
            }
        }

        string currentText = "";
        int colorState = 0; // 0=Rojo, 1=RojoOscuro, 2=Amarillo, 3=Cyan

        if (isSomeonePressingE)
        {
            // MECÁNICA DE ESTABILIZACIÓN: Mientras mantienen la E
            bleedOutTimer = maxBleedOutTime;
            reviveTimer += Time.deltaTime;

            if (reviveTimer >= timeRequiredToRevive)
            {
                photonView.RPC("RPC_Revive", RpcTarget.All);
                return;
            }

            int reviveSegundos = Mathf.CeilToInt(timeRequiredToRevive - reviveTimer);
            currentText = $"¡ESTABILIZANDO!\nReviviendo en {reviveSegundos}s";
            colorState = 3; // Cyan
        }
        else
        {
            reviveTimer = 0f; // Reiniciamos si sueltan la E
            bleedOutTimer -= Time.deltaTime;

            if (bleedOutTimer <= 0)
            {
                photonView.RPC("RPC_ActualizarTextoCaido", RpcTarget.All, false, "", 0);
                Die();
                return;
            }

            int bleedSegundos = Mathf.CeilToInt(bleedOutTimer);

            if (isSomeoneNear)
            {
                // Aparece la E cuando están cerca
                currentText = $"[E] Para Revivir\nDesangrado: {bleedSegundos}s";
                colorState = 2; // Amarillo
            }
            else
            {
                // Lógica normal si nadie ayuda
                currentText = bleedSegundos.ToString();
                colorState = bleedSegundos <= 10 ? 1 : 0;
            }
        }

        // AHORRO DE RED: Solo mandamos la orden si el texto visual cambió
        if (currentText != lastDisplayedText)
        {
            lastDisplayedText = currentText;
            photonView.RPC("RPC_ActualizarTextoCaido", RpcTarget.All, true, currentText, colorState);
        }
    }

    // --- MÉTODOS DEL TEXTO 3D ---

    [PunRPC]
    private void RPC_CrearTextoCuentaRegresiva()
    {
        countdownTextObj = new GameObject("TextoDesangrado");
        countdownTextObj.transform.SetParent(this.transform);

        // Lo subimos un poquito en Y (a 3f) porque el texto ahora usa dos renglones (\n)
        countdownTextObj.transform.localPosition = new Vector3(0f, 3f, 0f);

        countdownTextMesh = countdownTextObj.AddComponent<TextMesh>();
        countdownTextMesh.text = "";
        countdownTextMesh.characterSize = 0.15f;
        countdownTextMesh.fontSize = 45;
        countdownTextMesh.anchor = TextAnchor.MiddleCenter;
        countdownTextMesh.alignment = TextAlignment.Center;

        countdownTextObj.SetActive(false);
    }

    [PunRPC]
    public void RPC_ActualizarTextoCaido(bool mostrar, string textoCambiado, int colorState)
    {
        if (countdownTextObj != null)
        {
            countdownTextObj.SetActive(mostrar);
            if (mostrar)
            {
                countdownTextMesh.text = textoCambiado;

                // Mapeamos los colores optimizados
                switch (colorState)
                {
                    case 0: countdownTextMesh.color = Color.red; break;
                    case 1: countdownTextMesh.color = new Color(0.6f, 0f, 0f); break;
                    case 2: countdownTextMesh.color = Color.yellow; break;
                    case 3: countdownTextMesh.color = Color.cyan; break;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (countdownTextObj != null && countdownTextObj.activeSelf && Camera.main != null)
        {
            countdownTextObj.transform.rotation = Camera.main.transform.rotation;
        }
    }

    // --- LÓGICA DE DAÑO Y REVIVIR ---

    [PunRPC]
    public void RPC_TakeDamage(float damage, int shooterViewID)
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
                // ¡EL ZOMBI MURIÓ!
                // Buscamos quién fue el tirador
                PhotonView shooter = PhotonView.Find(shooterViewID);

                // Si el tirador existe, y es MI jugador en MI computadora, me sumo un punto
                if (shooter != null && shooter.IsMine)
                {
                    API_LaOrden.misKillsLocales++;
                    Debug.Log("¡Zombi eliminado! Kills actuales: " + API_LaOrden.misKillsLocales);
                }

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
        lastDisplayedText = "";

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

    [PunRPC]
    private void RPC_Revive()
    {
        isDowned = false;
        currentHealth = maxHealth / 2;

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
                // Lógica si el que muere definitivamente es un jugador
                if (Camera.main != null)
                {
                    Camera.main.gameObject.AddComponent<GhostCamera>();
                }

                API_LaOrden api = FindObjectOfType<API_LaOrden>();
                if (api != null)
                {
                    // Mandamos nuestro nombre, nuestras kills, y el tiempo que duramos vivos
                    api.EnviarReporteMuerte(PhotonNetwork.NickName, API_LaOrden.misKillsLocales, Time.timeSinceLevelLoad);
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