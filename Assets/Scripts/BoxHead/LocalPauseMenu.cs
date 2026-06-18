using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class LocalPauseMenu : MonoBehaviourPunCallbacks
{
    [Header("Referencias UI")]
    public GameObject pausePanel; // El panel gris que oscurece la pantalla
    public string mainMenuSceneName = "RoomMenu"; // El nombre de tu escena de menú

    private bool isMenuOpen = false;

    void Start()
    {
        // Nos aseguramos de que el menú empiece apagado
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    void Update()
    {
        // Al apretar ESC, abrimos o cerramos el menú
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (pausePanel != null)
        {
            pausePanel.SetActive(isMenuOpen);
        }

        // CLAVE: NO usamos Time.timeScale = 0f. 
        // Al no tocar el tiempo, el juego sigue corriendo, los zombies atacan
        // y tus compañeros te ven quieto. ¡Falsa pausa lograda!
    }

    public void LeaveMatchLocal()
    {
        // Apagamos el panel para que no cliqueen dos veces
        if (pausePanel != null) pausePanel.SetActive(false);

        // Le pedimos a Photon que nos saque de la sala actual
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            // Por si acaso no estábamos en sala, cargamos directo
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    // Este método es de Photon: se dispara automáticamente en tu máquina
    // justo en el instante en que terminás de salir de la sala.
    public override void OnLeftRoom()
    {
        // Una vez desconectados, cargamos la escena del menú
        SceneManager.LoadScene(mainMenuSceneName);
    }
}