using UnityEngine;
using UnityEngine.UI; // Obligatorio para usar el componente Slider (Barra de vida)
using TMPro;
using Photon.Pun;

public class HUDManager : MonoBehaviour
{
    // Creamos una estructura para organizar los componentes de cada jugador en el Inspector
    [System.Serializable]
    public struct PlayerUISlot
    {
        public GameObject contenedorSlot; // El objeto padre que prende/apaga todo este bloque
        public TextMeshProUGUI textoNombre;
        public Slider barraVida;
        public TextMeshProUGUI textoCargadores;
    }

    [Header("Configurá los 5 slots de la UI aquí")]
    public PlayerUISlot[] slotsJugadores = new PlayerUISlot[5];

    void Update()
    {
        // 1. Buscamos a todos los componentes de vida en la escena
        HealthSystem[] todosLosPersonajes = FindObjectsOfType<HealthSystem>();

        int currentIndexSlot = 0;

        // 2. Recorremos los personajes encontrados
        for (int i = 0; i < todosLosPersonajes.Length; i++)
        {
            HealthSystem health = todosLosPersonajes[i];

            // Filtramos: nos interesan solo los que son JUGADORES y tienen vista de red
            if (health.isPlayer && health.photonView != null)
            {
                // Seguridad: si por algún motivo hay más de 5, no rompemos el array
                if (currentIndexSlot >= slotsJugadores.Length) break;

                PlayerUISlot slot = slotsJugadores[currentIndexSlot];

                // Activamos el contenedor visual de este slot
                if (slot.contenedorSlot != null)
                    slot.contenedorSlot.SetActive(true);

                // A) Sincronizar Nombre
                if (slot.textoNombre != null)
                {
                    // Si soy yo, me pongo un tag para reconocerme rápido en el HUD
                    string prefijo = health.photonView.IsMine ? "[TÚ] " : "";
                    slot.textoNombre.text = prefijo + health.photonView.Owner.NickName;
                }

                // B) Sincronizar Barra de Vida (Slider)
                if (slot.barraVida != null)
                {
                    slot.barraVida.maxValue = health.maxHealth;
                    slot.barraVida.value = health.currentHealth;
                }

                // C) Sincronizar Cargadores
                TopDownWeaponController weapon = health.GetComponent<TopDownWeaponController>();
                if (slot.textoCargadores != null && weapon != null)
                {
                    slot.textoCargadores.text = "Cargadores: " + weapon.cargadoresActuales;
                }

                // Pasamos al siguiente slot de la UI
                currentIndexSlot++;
            }
        }

        // 3. Ocultamos los slots que sobran (si hay menos de 5 jugadores en la sala)
        for (int i = currentIndexSlot; i < slotsJugadores.Length; i++)
        {
            if (slotsJugadores[i].contenedorSlot != null)
            {
                slotsJugadores[i].contenedorSlot.SetActive(false);
            }
        }
    }
}