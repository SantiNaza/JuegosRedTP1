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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        weaponController = GetComponent<TopDownWeaponController>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        currentStamina = maxStamina; // Llenamos el aire al nacer
    }

    private void Start()
    {
        // El dueño del jugador le pide a la red que cree el texto para todos
        if (photonView.IsMine)
        {
            photonView.RPC("RPC_CrearTextoSinAire", RpcTarget.AllBuffered);
        }
    }

    private void Update()
    {
        // NUEVO: Bloqueamos los inputs si el menú de pausa local está abierto
        if (!photonView.IsMine || isKnockedBack || LocalPauseMenu.isPaused)
        {
            moveInput = Vector3.zero; // Frenamos al personaje para que no siga resbalando
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        ManejarStamina();

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