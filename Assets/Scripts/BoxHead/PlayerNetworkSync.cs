using UnityEngine;
using Photon.Pun;
using System;

public class PlayerNetworkSync : MonoBehaviourPun, IPunObservable
{
    private HealthSystem healthSystem;
    private TopDownWeaponController weaponController;

    // ¡Modificamos a 9 bytes!
    // float (Salud) = 4 bytes
    // int (Cargadores) = 4 bytes
    // bool (isPressingE) = 1 byte
    // Total = 9 bytes de puro rendimiento.
    private byte[] syncBuffer = new byte[9];

    void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        weaponController = GetComponent<TopDownWeaponController>();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            BitConverter.GetBytes(healthSystem.currentHealth).CopyTo(syncBuffer, 0);
            BitConverter.GetBytes(weaponController.cargadoresActuales).CopyTo(syncBuffer, 4);

            // Agregamos el input de la tecla E al final del paquete (posición 8)
            BitConverter.GetBytes(healthSystem.isPressingE).CopyTo(syncBuffer, 8);

            stream.SendNext(syncBuffer);
        }
        else
        {
            byte[] receivedBuffer = (byte[])stream.ReceiveNext();

            healthSystem.currentHealth = BitConverter.ToSingle(receivedBuffer, 0);
            weaponController.cargadoresActuales = BitConverter.ToInt32(receivedBuffer, 4);

            // Leemos si el compañero está apretando la E
            healthSystem.isPressingE = BitConverter.ToBoolean(receivedBuffer, 8);
        }
    }
}