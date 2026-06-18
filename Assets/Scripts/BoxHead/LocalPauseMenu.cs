using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class LocalPauseMenu : MonoBehaviourPunCallbacks
{
    [Header("Referencias UI")]
    public GameObject pausePanel;
    public string mainMenuSceneName = "RoomMenu";

    // CLAVE: Variable global estática para bloquear los controles
    public static bool isPaused = false;

    void Start()
    {
        isPaused = false; // Nos aseguramos de que arranque desactivada

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        // Invertimos el estado de la variable global
        isPaused = !isPaused;

        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }
    }

    public void LeaveMatchLocal()
    {
        isPaused = false; // Limpiamos la variable por las dudas antes de salir

        if (pausePanel != null) pausePanel.SetActive(false);

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}