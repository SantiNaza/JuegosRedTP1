using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMouseAim : MonoBehaviourPun
{
    [Header("Aim Settings")]
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private bool instantRotation = true;

    private Camera sceneCamera;
    private Rigidbody rb;
    private Quaternion targetRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        targetRotation = transform.rotation;
    }

    private void Start()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        sceneCamera = Camera.main;

        if (sceneCamera == null)
        {
            sceneCamera = FindObjectOfType<Camera>();
        }

        if (sceneCamera == null)
        {
            Debug.LogError("No se encontró ninguna cámara en la escena.");
        }
    }

    private void Update()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        AimToMouse();
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        if (instantRotation)
        {
            rb.MoveRotation(targetRotation);
        }
        else
        {
            Quaternion smoothRotation = Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

            rb.MoveRotation(smoothRotation);
        }
    }

    private void AimToMouse()
    {
        if (sceneCamera == null)
        {
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 mouseWorldPosition = ray.GetPoint(distance);

            Vector3 direction = mouseWorldPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            targetRotation = Quaternion.LookRotation(direction);
        }
    }
}