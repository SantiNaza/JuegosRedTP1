using UnityEngine;
using Photon.Pun;
using System; // Obligatorio para usar el BitConverter

public class PlayerNetworkSync : MonoBehaviourPun, IPunObservable
{
    private HealthSystem healthSystem;
    private TopDownWeaponController weaponController;

    // Nuestro paquete a medida.
    // float (Salud) = 4 bytes
    // int (Cargadores) = 4 bytes
    // Total = 8 bytes de puro rendimiento.
    private byte[] syncBuffer = new byte[8];

    void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        weaponController = GetComponent<TopDownWeaponController>();
    }

    // Este método es el corazón de la sincronización continua
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 1. EMPAQUETAR (El dueño de la compu)
            // Pasamos la salud a bytes y la ponemos al principio del paquete (posición 0)
            BitConverter.GetBytes(healthSystem.currentHealth).CopyTo(syncBuffer, 0);

            // Pasamos los cargadores a bytes y los ponemos a la mitad (posición 4)
            BitConverter.GetBytes(weaponController.cargadoresActuales).CopyTo(syncBuffer, 4);

            // Mandamos los 8 bytes de una sola vez por la red
            stream.SendNext(syncBuffer);
        }
        else
        {
            // 2. DESEMPAQUETAR (Las compus de los compañeros)
            // Recibimos el arreglo de bytes crudos
            byte[] receivedBuffer = (byte[])stream.ReceiveNext();

            // Traducimos los primeros 4 bytes de vuelta a un float (Salud)
            healthSystem.currentHealth = BitConverter.ToSingle(receivedBuffer, 0);

            // Traducimos los últimos 4 bytes de vuelta a un int (Cargadores)
            weaponController.cargadoresActuales = BitConverter.ToInt32(receivedBuffer, 4);
        }
    }
}