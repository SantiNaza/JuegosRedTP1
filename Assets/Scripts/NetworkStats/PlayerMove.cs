using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviourPun
{
    [Header("Configuración de Movimiento")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    private CharacterController characterController;
    private Vector3 velocity;

    [Header("Configuración de Cámara (Mouse)")]
    public Transform playerCameraTransform;
    public float mouseSensitivity = 2f; // Sensibilidad ajustada para el sistema clásico
    private float verticalRotation = 0f;

    void Start()
    {
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        characterController = GetComponent<CharacterController>();

        // Bloquear el cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        ManejarRotacionMouse();
        ManejarMovimientoWASD();
    }

    private void ManejarRotacionMouse()
    {
        // Lectura clásica del ratón
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotación Horizontal (Cuerpo)
        transform.Rotate(Vector3.up * mouseX);

        // Rotación Vertical (Cámara)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f); 
        
        playerCameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void ManejarMovimientoWASD()
    {
        // Lectura clásica de WASD / Flechas
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 moveDirection = (transform.forward * moveZ) + (transform.right * moveX);
        
        characterController.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);

        // Gravedad
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }
}