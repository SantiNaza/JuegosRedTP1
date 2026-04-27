using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string playSceneName = "RoomMenu";

    public void Play()
    {
        SceneManager.LoadScene(playSceneName);
    }

    public void Quit()
    {
        Application.Quit();

        Debug.Log("Quit Game");
    }
}