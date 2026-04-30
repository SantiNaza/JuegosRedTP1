using UnityEngine;
using TMPro;
using Photon.Realtime;

public class RoomItem : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;

    private string roomName;

    public void Setup(RoomInfo info)
    {
        roomName = info.Name;
        roomNameText.text = info.Name;
        playerCountText.text = info.PlayerCount + " / " + info.MaxPlayers;
    }

    // A este método lo tenés que arrastrar al evento OnClick() del botón en el Inspector
    public void OnClickItem()
    {
        PhotonManager.instance.JoinSpecificRoom(roomName);
    }
}