using UnityEngine;
using Photon.Pun;

public class PickupCargador : MonoBehaviourPun
{
    private bool yaRecogido = false;

    // NUEVO: Agregamos el tiempo de gracia
    private float delayRecogida = 1.5f;

    void Update()
    {
        // Restamos el tiempo en cada frame
        if (delayRecogida > 0) delayRecogida -= Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        // Si ya lo agarraron o si el tiempo de gracia no terminó, cortamos acá
        if (yaRecogido || delayRecogida > 0) return;

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