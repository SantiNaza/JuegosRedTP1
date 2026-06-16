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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        Vector3 velocity = moveInput * moveSpeed;

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
}