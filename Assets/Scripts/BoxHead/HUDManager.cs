using UnityEngine;
using TMPro; // Agregamos la librería de TextMeshPro
using Photon.Pun;

public class HUDManager : MonoBehaviour
{
    [Header("Arrastrá el texto de UI acá")]
    public TextMeshProUGUI textoCargadores; // Cambiamos el tipo a TextMeshProUGUI

    private TopDownWeaponController jugadorLocal;

    void Update()
    {
        // Buscamos al jugador local si todavía no lo enganchamos
        if (jugadorLocal == null)
        {
            TopDownWeaponController[] todosLosJugadores = FindObjectsOfType<TopDownWeaponController>();

            foreach (TopDownWeaponController jugador in todosLosJugadores)
            {
                if (jugador.photonView.IsMine)
                {
                    jugadorLocal = jugador;
                    break;
                }
            }
        }

        // Si ya encontramos a nuestro jugador, actualizamos la UI
        if (jugadorLocal != null && textoCargadores != null)
        {
            textoCargadores.text = "Cargadores: " + jugadorLocal.cargadoresActuales;
        }
    }
}