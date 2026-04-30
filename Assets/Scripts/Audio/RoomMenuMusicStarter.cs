using UnityEngine;

public class RoomMenuMusicStarter : MonoBehaviour
{
    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayRoomMenuMusic();
    }
}