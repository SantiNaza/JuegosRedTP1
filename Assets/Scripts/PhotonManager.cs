using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager instance;

    public Action OnRoom;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "Menu";
    [SerializeField] private string gameSceneName = "Gameplay";

    [Header("Room Settings")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private byte maxPlayersAmount = 4;

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;

    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    private void Awake()
    {
        instance = this;

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        SetStatus("Conectado. Podés crear o unirte a una room.");
    }

    public void CreateRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Todavía no estás conectado.");
            return;
        }

        string roomName = GetRoomName();

        if (string.IsNullOrEmpty(roomName))
        {
            SetStatus("Escribí un nombre para crear la room.");
            return;
        }

        RoomOptions roomOptions = new RoomOptions();

        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;
        roomOptions.MaxPlayers = maxPlayersAmount;

        roomOptions.PlayerTtl = 3000;
        roomOptions.EmptyRoomTtl = 60000;

        roomOptions.BroadcastPropsChangeToAll = true;

        SetStatus("Creando room: " + roomName);

        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Todavía no estás conectado.");
            return;
        }

        string roomName = GetRoomName();

        if (string.IsNullOrEmpty(roomName))
        {
            SetStatus("Escribí el nombre de la room.");
            return;
        }

        SetStatus("Entrando a room: " + roomName);

        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnJoinedRoom()
    {
        string roomName = PhotonNetwork.CurrentRoom.Name;
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        Debug.Log("Joined Room: " + roomName);
        Debug.Log("Player Count: " + playerCount);
        Debug.Log("Is Master Client: " + PhotonNetwork.IsMasterClient);

        SetStatus("Entraste a la room: " + roomName);

        OnRoom?.Invoke();

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateCachedRoomList(roomList);
    }

    private void UpdateCachedRoomList(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info.RemovedFromList)
            {
                cachedRoomList.Remove(info.Name);
            }
            else
            {
                cachedRoomList[info.Name] = info;
            }
        }
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus("No se pudo crear la room: " + message);
        Debug.LogWarning("Create Room Failed: " + message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetStatus("No se pudo entrar a la room: " + message);
        Debug.LogWarning("Join Room Failed: " + message);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus("Desconectado de Photon: " + cause);
        Debug.LogWarning("Disconnected: " + cause);
    }

    private string GetRoomName()
    {
        if (roomNameInput == null)
        {
            Debug.LogWarning("Room Name Input no está asignado en el Inspector.");
            return "";
        }

        return roomNameInput.text.Trim();
    }

    private void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}