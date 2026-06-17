using UnityEngine;
using Photon.Pun;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class PlayerMovement : MonoBehaviourPun
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private Vector3 moveInput;
    
    // NUEVO: Bandera para saber si estamos siendo empujados
    private bool isKnockedBack = false;

    // Agregamos esta variable al principio de tu script PlayerMovement
    private TopDownWeaponController weaponController;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        weaponController = GetComponent<TopDownWeaponController>();

        // SOLUCIÓN A LOS TROPEZONES: Congelamos la rotación para que no se caiga
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        // Si no es nuestro jugador O si estamos siendo empujados, no procesamos inputs
        if (!photonView.IsMine || isKnockedBack)
        {
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal"); // A / D
        float vertical = Input.GetAxisRaw("Vertical");     // W / S

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        // No forzamos el movimiento normal si estamos en medio de un empuje
        if (!photonView.IsMine || isKnockedBack)
        {
            return;
        }

        Move();
    }

    private void Move()
    {
        // Revisamos si estamos recargando para reducir la velocidad a la mitad
        float currentSpeed = moveSpeed;
        if (weaponController != null && weaponController.isReloading)
        {
            currentSpeed = moveSpeed / 2f;
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
        rb.velocity = new Vector3(
            rb.velocity.x,
            0f,
            rb.velocity.z
        );

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(
            transform.position,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );
    }

    // ==========================================
    // SISTEMA DE EMPUJE (ADAPTADO PARA RIGIDBODY)
    // ==========================================
    [PunRPC]
    public void RPC_ApplyKnockback(Vector3 knockbackForce)
    {
        // Solo el dueño del personaje calcula su propio empuje físico
        if (!photonView.IsMine) return;
        
        StartCoroutine(KnockbackRoutine(knockbackForce));
    }

    private IEnumerator KnockbackRoutine(Vector3 force)
    {
        isKnockedBack = true; // Pausamos el control del jugador temporalmente

        // Frenamos en seco al personaje para que el impacto no se sume a la inercia anterior
        rb.velocity = Vector3.zero; 

        // Aplicamos la fuerza de golpe instantánea (Impulse)
        rb.AddForce(force, ForceMode.Impulse);

        // Esperamos el tiempo de "aturdimiento" / duración del empuje
        yield return new WaitForSeconds(0.2f);

        isKnockedBack = false; // Devolvemos el control total al jugador
    }

    [Header("Configuración de Cámara")]
    private Vector3 cameraOffset = new Vector3(0f, 12f, -6f); // Ajustá la altura y distancia acá
    private Vector3 anguloCamara = new Vector3(60f, 0f, 0f);  // Inclinación mirando hacia abajo

    // Mové esta función al final de tu script PlayerMovement.cs
    private void LateUpdate()
    {
        // Solo movemos la cámara si este es nuestro jugador
        if (!photonView.IsMine) return;

        if (Camera.main != null)
        {
            // 1. La cámara copia tu posición más el offset, pero NO es hija tuya
            Camera.main.transform.position = transform.position + cameraOffset;

            // 2. Clavamos la rotación para que siempre mire igual sin importar a dónde apuntes
            Camera.main.transform.rotation = Quaternion.Euler(anguloCamara);
        }
    }
}