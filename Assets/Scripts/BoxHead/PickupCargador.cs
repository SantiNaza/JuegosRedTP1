using UnityEngine;
using Photon.Pun;

public class PickupCargador : MonoBehaviourPun
{
    void OnTriggerEnter(Collider other)
    {
        // Solo el jugador que toca el cargador ejecuta esta lógica en su máquina
        if (other.CompareTag("Player"))
        {
            PhotonView playerView = other.GetComponent<PhotonView>();

            // Verificamos si somos los dueños de ese jugador
            if (playerView != null && playerView.IsMine)
            {
                TopDownWeaponController weapon = other.GetComponent<TopDownWeaponController>();
                if (weapon != null)
                {
                    weapon.RecibirCargador(); // Le sumamos el cargador al jugador

                    // Solo el MasterClient puede destruir objetos instanciados en red correctamente
                    // o el dueño del objeto. Le pedimos a Photon que lo destruya en todos lados.
                    PhotonNetwork.Destroy(gameObject);
                }
            }
        }
    }
}