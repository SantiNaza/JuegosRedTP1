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
    [SerializeField] private byte maxPlayersAmount = 5;

    [Header("Panels")]
    [SerializeField] private GameObject panelRoom;
    [SerializeField] private GameObject panelLobby;

    [Header("Lobby UI")]
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private TMP_Text lobbyText;
    [SerializeField] private TMP_Text statusText;

    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    private bool backToMainMenu;

    private void Awake()
    {
        instance = this;

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();

        ShowRoomPanel();
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

        ShowLobbyPanel();
        UpdateLobbyInfo();
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Solo el Host puede iniciar la partida.");
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        PhotonNetwork.LoadLevel(gameSceneName);
    }

    public void BackToRoomMenu()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            ShowRoomPanel();
        }
    }

    public void BackToMainMenu()
    {
        backToMainMenu = true;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public override void OnLeftRoom()
    {
        if (backToMainMenu)
        {
            backToMainMenu = false;
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        ShowRoomPanel();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateLobbyInfo();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateLobbyInfo();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateLobbyInfo();
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

    private void ShowRoomPanel()
    {
        if (panelRoom != null)
        {
            panelRoom.SetActive(true);
        }

        if (panelLobby != null)
        {
            panelLobby.SetActive(false);
        }
    }

    private void ShowLobbyPanel()
    {
        if (panelRoom != null)
        {
            panelRoom.SetActive(false);
        }

        if (panelLobby != null)
        {
            panelLobby.SetActive(true);
        }
    }

    private void UpdateLobbyInfo()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        bool isHost = PhotonNetwork.IsMasterClient;

        if (startGameButton != null)
        {
            startGameButton.SetActive(isHost);
        }

        if (lobbyText != null)
        {
            lobbyText.text =
                "Room: " + PhotonNetwork.CurrentRoom.Name +
                "\nPlayers: " + PhotonNetwork.CurrentRoom.PlayerCount + " / " + PhotonNetwork.CurrentRoom.MaxPlayers +
                "\nHost: " + PhotonNetwork.MasterClient.NickName;
        }
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