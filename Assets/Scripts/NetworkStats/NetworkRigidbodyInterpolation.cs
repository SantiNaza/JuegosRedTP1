using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class NetworkRigidbodyInterpolation : MonoBehaviourPun, IPunObservable
{
    private Rigidbody rb;

    // Variables para almacenar los datos que llegan de la red
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    [Tooltip("Velocidad de suavizado para la interpolación")]
    public float interpolationSpeed = 15f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Importante: Si no es nuestro, apagamos la gravedad para que la física local
        // no pelee contra la posición que viene de la red.
        if (!photonView.IsMine)
        {
            rb.useGravity = false;
            rb.isKinematic = true; // Opcional, según si querés que colisione localmente o no
        }
    }

    // 1. EL INTERCAMBIO DE DATOS (Ocurre según el SerializationRate de Photon)
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // El dueño del objeto envía los datos reales de su Rigidbody
            stream.SendNext(rb.position);
            stream.SendNext(rb.rotation);
        }
        else
        {
            // Los clientes remotos reciben la posición del dueño
            targetPosition = (Vector3)stream.ReceiveNext();
            targetRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    // 2. LA INTERPOLACIÓN (Ocurre en el FixedUpdate para respetar el ciclo de físicas)
    private void FixedUpdate()
    {
        // Si el objeto me pertenece, me muevo con mis scripts de Input normales
        if (photonView.IsMine) return;

        // Si es un objeto remoto, interpolamos usando métodos específicos de Rigidbody:
        
        // Interpolación de Posición
        Vector3 nextPosition = Vector3.Lerp(rb.position, targetPosition, Time.fixedDeltaTime * interpolationSpeed);
        rb.MovePosition(nextPosition);

        // Interpolación de Rotación
        Quaternion nextRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * interpolationSpeed);
        rb.MoveRotation(nextRotation);
    }
}