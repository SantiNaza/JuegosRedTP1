using UnityEngine;
using TMPro;
using Photon.Realtime; 

public class RoomItem : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    private string roomName;

   
    public void Setup(RoomInfo info)
    {
        roomName = info.Name;
        roomNameText.text = $"{info.Name} ({info.PlayerCount}/{info.MaxPlayers})";
    }

    
    public void OnClickItem()
    {
        RoomPhotonManager.instance.JoinSpecificRoom(roomName);
    }
}