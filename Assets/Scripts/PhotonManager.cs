using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance;

    public Action OnRoom;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 1. Hacemos que este objeto sobreviva al cambiar del Men� a la Escena de Juego
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            // 2. Destruimos el GameObject entero si ya existe una copia
            Destroy(this.gameObject);
        }
    }

    void Start()
    {
        // 3. Solo nos conectamos si venimos desconectados (Play desde la escena directa)
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        // Nota mental para despu�s: Est� hardcodeado a "My Room", lo cual para el TP est� perfecto.
        // Auto-join DESACTIVADO: crear/unirse a una room lo maneja el RoomPhotonManager (UI).
        // Con esto activo, al volver a la RoomMenu te metia solo a "My Room" sin tocar nada.
        // PhotonNetwork.JoinRandomOrCreateRoom(roomName: "My Room");
    }

    public override void OnJoinedRoom()
    {
        string roomName = PhotonNetwork.CurrentRoom.Name;
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        OnRoom?.Invoke();

        bool isMaster = PhotonNetwork.IsMasterClient;

        Debug.Log("Joined Room: " + roomName + ", PlayerCount:" + playerCount + ", IsMasterClient: " + isMaster);
    }
}