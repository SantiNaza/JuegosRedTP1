using UnityEngine;
using Photon.Pun;

public class PickupCargador : MonoBehaviourPun
{
    // Usamos esta bandera para evitar que dos jugadores lo agarren en el mismo frame exacto
    private bool yaRecogido = false;

    void OnTriggerEnter(Collider other)
    {
        if (yaRecogido) return;

        // Solo el jugador que toca el cargador ejecuta esta lógica en su máquina
        if (other.CompareTag("Player"))
        {
            PhotonView playerView = other.GetComponent<PhotonView>();

            // Verificamos si somos los dueños de ese jugador que acaba de chocar
            if (playerView != null && playerView.IsMine)
            {
                TopDownWeaponController weapon = other.GetComponent<TopDownWeaponController>();
                if (weapon != null)
                {
                    weapon.RecibirCargador(); // Le sumamos el cargador al jugador

                    yaRecogido = true; // Lo marcamos como recogido localmente

                    // Magia de red: Le mandamos la orden DIRECTAMENTE al dueño del cargador
                    photonView.RPC("RPC_DestruirCargador", photonView.Owner);
                }
            }
        }
    }

    [PunRPC]
    public void RPC_DestruirCargador()
    {
        // Esta función solo la va a ejecutar el dueño del objeto (el que lo dropeó).
        // Como él sí tiene los permisos, Photon lo destruye para todos sin chistar.
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}