using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

public class HealthSystem : MonoBehaviourPun
{
    public float maxHealth = 100f;
    public float currentHealth;
    public bool destroyOnDeath = true;

    [Header("Configuración de Jugador (Revivir)")]
    public bool isPlayer = false;
    public bool isDowned = false;
    public bool isPressingE = false;

    public float maxBleedOutTime = 15f;
    private float bleedOutTimer;
    private float reviveTimer = 0f;
    public float timeRequiredToRevive = 3f;
    public float reviveRadius = 2f;

    // NUEVO: El inventario ordenado de chapas recogidas
    public List<int> chapasRecogidas = new List<int>();

    private GameObject countdownTextObj;
    private TextMesh countdownTextMesh;
    private string lastDisplayedText = "";

    void Start()
    {
        currentHealth = maxHealth;
        if (isPlayer) photonView.RPC("RPC_CrearTextoCuentaRegresiva", RpcTarget.AllBuffered);
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (isPlayer && !isDowned) isPressingE = Input.GetKey(KeyCode.E);
        if (isPlayer && isDowned) ManejarEstadoCaido();
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
                    if (allyHealth.isPressingE)
                    {
                        isSomeonePressingE = true;
                        break;
                    }
                }
            }
        }

        string currentText = "";
        int colorState = 0;

        if (isSomeonePressingE)
        {
            bleedOutTimer = maxBleedOutTime;
            reviveTimer += Time.deltaTime;

            if (reviveTimer >= timeRequiredToRevive)
            {
                photonView.RPC("RPC_Revive", RpcTarget.All);
                return;
            }

            int reviveSegundos = Mathf.CeilToInt(timeRequiredToRevive - reviveTimer);
            currentText = $"¡ESTABILIZANDO!\nReviviendo en {reviveSegundos}s";
            colorState = 3;
        }
        else
        {
            reviveTimer = 0f;
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
                currentText = $"[E] Para Revivir\nDesangrado: {bleedSegundos}s";
                colorState = 2;
            }
            else
            {
                currentText = bleedSegundos.ToString();
                colorState = bleedSegundos <= 10 ? 1 : 0;
            }
        }

        if (currentText != lastDisplayedText)
        {
            lastDisplayedText = currentText;
            photonView.RPC("RPC_ActualizarTextoCaido", RpcTarget.All, true, currentText, colorState);
        }
    }

    [PunRPC]
    private void RPC_CrearTextoCuentaRegresiva()
    {
        countdownTextObj = new GameObject("TextoDesangrado");
        countdownTextObj.transform.SetParent(this.transform);
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
                PhotonView shooter = PhotonView.Find(shooterViewID);
                if (shooter != null && shooter.IsMine)
                {
                    API_LaOrden.misKillsLocales++;
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
        if (rb != null) rb.isKinematic = false;

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
                if (waveManager != null) waveManager.ZombieDied();
            }
            else
            {
                // 1. SOLTAMOS NUESTRA PROPIA CHAPA
                SoltarChapaAlPiso(PhotonNetwork.LocalPlayer.ActorNumber);

                // 2. SOLTAMOS TODAS LAS CHAPAS QUE LLEVÁBAMOS
                foreach (int actorCaido in chapasRecogidas)
                {
                    SoltarChapaAlPiso(actorCaido);
                }

                // Vaciamos la mochila (ya están todas en el piso)
                photonView.RPC("RPC_LimpiarChapas", RpcTarget.All);

                if (Camera.main != null) Camera.main.gameObject.AddComponent<GhostCamera>();

                API_LaOrden api = FindObjectOfType<API_LaOrden>();
                if (api != null)
                {
                    string miNombre = string.IsNullOrEmpty(PhotonNetwork.NickName) ? "Agente " + PhotonNetwork.LocalPlayer.ActorNumber : PhotonNetwork.NickName;
                    api.EnviarReporteMuerte(miNombre, API_LaOrden.misKillsLocales, Time.timeSinceLevelLoad);
                }
            }

            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void SoltarChapaAlPiso(int actorNum)
    {
        // Le damos un pequeño empujoncito aleatorio para que si caen 3 juntas, no queden en el mismo pixel
        Vector3 offset = new Vector3(Random.Range(-0.8f, 0.8f), 1f, Random.Range(-0.8f, 0.8f));
        GameObject chapa = PhotonNetwork.Instantiate("ChapaPrefab", transform.position + offset, Quaternion.identity);
        chapa.GetComponent<PhotonView>().RPC("RPC_ConfigurarChapa", RpcTarget.AllBuffered, actorNum);
    }

    // --- MANEJO DE MOCHILA DE CHAPAS ---
    [PunRPC]
    public void RPC_RecogerChapa(int actorNumber)
    {
        // Agregamos al final de la lista para respetar el orden de recogida
        if (photonView.IsMine && !chapasRecogidas.Contains(actorNumber))
        {
            chapasRecogidas.Add(actorNumber);
        }
    }

    [PunRPC]
    public void RPC_RemoverPrimeraChapa()
    {
        if (photonView.IsMine && chapasRecogidas.Count > 0)
        {
            chapasRecogidas.RemoveAt(0);
        }
    }

    [PunRPC]
    public void RPC_LimpiarChapas()
    {
        if (photonView.IsMine) chapasRecogidas.Clear();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        // Un jugador caído no se cura con el item (se revive con la E)
        if (isDowned)
            return;

        photonView.RPC(nameof(RPC_Heal), RpcTarget.All, amount);
    }

    [PunRPC]
    public void RPC_Heal(float amount)
    {
        if (isDowned)
            return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        Debug.Log("Curado +" + amount + " | Vida actual: " + currentHealth);
    }
}