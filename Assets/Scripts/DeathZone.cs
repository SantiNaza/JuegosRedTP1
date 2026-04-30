using UnityEngine;
using Photon.Pun;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Buscamos si lo que colisionó tiene el script PlayerHealth
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            PhotonView pv = playerHealth.GetComponent<PhotonView>();

            // MUY IMPORTANTE EN RED: 
            // Solo el "dueño" del jugador debe mandar la orden de hacerse daño.
            // Esto evita que si hay 4 jugadores en la partida, los 4 intenten 
            // mandarle la orden de muerte al mismo jugador al mismo tiempo.
            if (pv != null && pv.IsMine)
            {
                // Le pasamos un daño igual o mayor a su vida máxima para asegurar que muera
                playerHealth.TakeDamage(playerHealth.MaxHealth * 2f);
            }
        }
    }
}