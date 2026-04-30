using UnityEngine;

public class GameplayMusicStarter : MonoBehaviour
{
    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameplayMusic();
    }
}