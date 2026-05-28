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
       }
       else
       {
           Destroy(this);
       }
   }

   void Start()
   {
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
        PhotonNetwork.JoinRandomOrCreateRoom(roomName: "My Room");
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
