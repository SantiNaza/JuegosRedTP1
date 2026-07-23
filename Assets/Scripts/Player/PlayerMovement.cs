using UnityEngine;
using Photon.Pun;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class PlayerMovement : MonoBehaviourPun
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Sprint y Stamina")]
    public float sprintMultiplier = 1.5f;
    public float maxStamina = 4f; // Cuántos segundos puede correr sin cansarse
    private float currentStamina;
    private bool isExhausted = false; // Bandera para saber si se quedó sin aire
    private GameObject sinAireTextObj;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.1f;
    public LayerMask groundLayer;

    [Header("Configuración de Cámara")]
    private Vector3 cameraOffset = new Vector3(0f, 6f, -3f);
    private Vector3 anguloCamara = new Vector3(60f, 0f, 0f);

    private Rigidbody rb;
    private Vector3 moveInput;
    private bool isKnockedBack = false;
    private TopDownWeaponController weaponController;

    [Header("Animacion")]
    private Animator animator;
    private bool lastWalking;
    private bool lastRunning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        weaponController = GetComponent<TopDownWeaponController>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        currentStamina = maxStamina; // Llenamos el aire al nacer
    }

    private void Start()
    {
        // El Animator del modelo activo (PlayerModel ya lo activo en Awake)
        animator = GetComponentInChildren<Animator>();

        // El dueño del jugador le pide a la red que cree el texto para todos
        if (photonView.IsMine)
        {
            photonView.RPC("RPC_CrearTextoSinAire", RpcTarget.AllBuffered);
        }
    }

    private void Update()
    {
        // NUEVO: Bloqueamos los inputs si el menú de pausa local está abierto
        if (!photonView.IsMine) return; // los remotos animan por RPC, no por input

        if (isKnockedBack || LocalPauseMenu.isPaused)
        {
            moveInput = Vector3.zero; // Frenamos al personaje para que no siga resbalando
            ActualizarAnimacion();    // queda en idle mientras esta quieto/pausado
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        ManejarStamina();
        ActualizarAnimacion();

        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            Jump();
        }
    }

    private void ManejarStamina()
    {
        // ¿Nos estamos moviendo y apretando Shift?
        bool isTryingToSprint = Input.GetKey(KeyCode.LeftShift) && moveInput.magnitude > 0;

        // No podemos correr si el arma está en plena recarga
        bool isReloading = weaponController != null && weaponController.isReloading;

        if (isTryingToSprint && !isExhausted && !isReloading)
        {
            // Gastamos aire
            currentStamina -= Time.deltaTime;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                isExhausted = true; // ¡Nos quedamos sin aire!
                photonView.RPC("RPC_MostrarTextoSinAire", RpcTarget.All, true);
            }
        }
        else
        {
            // Si no estamos corriendo, recuperamos aire
            if (currentStamina < maxStamina)
            {
                currentStamina += Time.deltaTime;

                // Si nos cansamos, hay que esperar a que el pulmón se llene al 100% para volver a correr
                if (isExhausted && currentStamina >= maxStamina)
                {
                    isExhausted = false;
                    photonView.RPC("RPC_MostrarTextoSinAire", RpcTarget.All, false);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        // NUEVO: Bloqueamos las físicas de movimiento voluntario si está pausado
        if (!photonView.IsMine || isKnockedBack || LocalPauseMenu.isPaused) return;

        Move();
    }

    private void Move()
    {
        float currentSpeed = moveSpeed;
        bool isReloading = weaponController != null && weaponController.isReloading;

        // Jerarquía de velocidades: Recargar frena el sprint
        if (isReloading)
        {
            currentSpeed = moveSpeed * 0.75f; // Penalización ajustada al 75%
        }
        else if (Input.GetKey(KeyCode.LeftShift) && !isExhausted && moveInput.magnitude > 0)
        {
            currentSpeed = moveSpeed * sprintMultiplier; // Aumento al 150%
        }

        Vector3 velocity = moveInput * currentSpeed;

        rb.velocity = new Vector3(
            velocity.x,
            rb.velocity.y,
            velocity.z
        );
    }

    private void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
    }

    // ==========================================
    // ANIMACION (IsWalking / IsRunning)
    // ==========================================
    private void ActualizarAnimacion()
    {
        bool isMoving = moveInput.magnitude > 0.01f;
        bool isReloading = weaponController != null && weaponController.isReloading;

        // Running solo si: se mueve + Shift + tiene aire + no esta recargando
        bool isSprinting = isMoving && Input.GetKey(KeyCode.LeftShift) && !isExhausted && !isReloading;

        // IsWalking queda activo SIEMPRE que te movas (asi nunca vuelve a idle
        // en pleno sprint). IsRunning se suma cuando esprintas.
        bool walking = isMoving;      // moviendose (normal o sprint)
        bool running = isSprinting;   // ademas, corriendo

        // Solo avisamos por red cuando el estado CAMBIA (no cada frame)
        if (walking != lastWalking || running != lastRunning)
        {
            lastWalking = walking;
            lastRunning = running;

            AplicarAnim(walking, running);                                        // local inmediato
            photonView.RPC(nameof(RPC_SetAnim), RpcTarget.Others, walking, running); // resto de clientes
        }
    }

    private void AplicarAnim(bool walking, bool running)
    {
        if (animator == null)
        {
            Debug.LogWarning("[Anim] animator NULL: no se encontro Animator en el modelo activo");
            return;
        }
        Debug.Log($"[Anim] IsWalking={walking} IsRunning={running}");
        animator.SetBool("IsWalking", walking);
        animator.SetBool("IsRunning", running);
    }

    // Aplica los bools del Animator en los clientes remotos (cada uno en su modelo activo)
    [PunRPC]
    private void RPC_SetAnim(bool walking, bool running)
    {
        AplicarAnim(walking, running);
    }

    // ==========================================
    // CÁMARA Y TEXTOS 3D
    // ==========================================
    private void LateUpdate()
    {
        // 1. La cámara solo la mueve el dueño local de este personaje
        if (photonView.IsMine && Camera.main != null)
        {
            Camera.main.transform.position = transform.position + cameraOffset;
            Camera.main.transform.rotation = Quaternion.Euler(anguloCamara);
        }

        // 2. El texto flotante debe rotar hacia la cámara en LAS PANTALLAS DE TODOS
        if (sinAireTextObj != null && sinAireTextObj.activeSelf && Camera.main != null)
        {
            sinAireTextObj.transform.rotation = Camera.main.transform.rotation;
        }
    }

    // ==========================================
    // SISTEMA DE EMPUJE
    // ==========================================
    [PunRPC]
    public void RPC_ApplyKnockback(Vector3 knockbackForce)
    {
        if (!photonView.IsMine) return;
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector3 force)
    {
        isKnockedBack = true;
        rb.velocity = Vector3.zero;
        rb.AddForce(force, ForceMode.Impulse);
        yield return new WaitForSeconds(0.2f);
        isKnockedBack = false;
    }

    // ==========================================
    // RPCS PARA EL TEXTO DE STAMINA
    // ==========================================
    [PunRPC]
    private void RPC_CrearTextoSinAire()
    {
        sinAireTextObj = new GameObject("TextoSinAire");
        sinAireTextObj.transform.SetParent(this.transform);
        // Lo ponemos un poquito más alto (3f) para que no se superponga con el "¡RECARGANDO!" (2.5f)
        sinAireTextObj.transform.localPosition = new Vector3(0f, 3f, 0f);

        TextMesh tm = sinAireTextObj.AddComponent<TextMesh>();
        tm.text = "¡SIN AIRE!";
        tm.characterSize = 0.15f;
        tm.fontSize = 40;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.cyan;

        sinAireTextObj.SetActive(false);
    }

    [PunRPC]
    public void RPC_MostrarTextoSinAire(bool mostrar)
    {
        if (sinAireTextObj != null)
        {
            sinAireTextObj.SetActive(mostrar);
        }
    }

    public void AplicarMejoraVelocidad(float velocidadExtra)
    {
        moveSpeed += velocidadExtra;
    }
}