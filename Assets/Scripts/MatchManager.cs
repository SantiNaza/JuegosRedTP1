using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

[RequireComponent(typeof(PhotonView))]
public class MatchManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte PlayerDeathEventCode = 1;
    private const byte MatchEndEventCode = 2;

    private const int WinnerResult = 0;
    private const int DrawResult = 1;

    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string roomMenuSceneName = "RoomMenu";

    [Header("Match Settings")]
    [SerializeField] private float simultaneousDeathWindow = 0.35f;

    [Header("Result UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;

    [Header("Notifications UI")]
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private float notificationDuration = 3f;

    [SerializeField]
    private Color[] playerColors = new Color[5]
    {
        Color.green,
        Color.red,
        Color.blue,
        Color.yellow,
        Color.black
    };

    private Coroutine notificationCoroutine;

    private HashSet<int> alivePlayers = new HashSet<int>();
    private HashSet<int> deadPlayers = new HashSet<int>();

    private List<int> finalDeathWindowPlayers = new List<int>();

    private bool isResolvingMatch;
    private bool matchEnded;
    private bool resultShown;

    private bool returningToRoomMenu;
    private bool alreadyReturningToRoomMenu;
    private bool alreadyRestarting;

    public override void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        RegisterInitialPlayers();
    }

    private void RegisterInitialPlayers()
    {
        alivePlayers.Clear();
        deadPlayers.Clear();

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            alivePlayers.Add(player.ActorNumber);
        }

        Debug.Log("Jugadores vivos al iniciar: " + alivePlayers.Count);
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;

        if (eventCode == PlayerDeathEventCode)
        {
            object[] data = (object[])photonEvent.CustomData;

            int deadPlayerActorNumber = (int)data[0];

            Debug.Log("Evento recibido en MatchManager. Murió: " + deadPlayerActorNumber);

            RegisterPlayerDeath(deadPlayerActorNumber);
        }

        if (eventCode == MatchEndEventCode)
        {
            object[] data = (object[])photonEvent.CustomData;

            int resultType = (int)data[0];
            int[] resultPlayers = (int[])data[1];

            Debug.Log("Evento de final de partida recibido.");

            ShowMatchResult(resultType, resultPlayers);
        }
    }

    private void RegisterPlayerDeath(int deadPlayerActorNumber)
    {
        if (matchEnded)
        {
            return;
        }

        if (deadPlayers.Contains(deadPlayerActorNumber))
        {
            return;
        }

        deadPlayers.Add(deadPlayerActorNumber);
        alivePlayers.Remove(deadPlayerActorNumber);

        string playerName = GetPlayerName(deadPlayerActorNumber);
        string colorHex = GetPlayerColorHex(deadPlayerActorNumber);
        ShowNotification($"<color=#{colorHex}>{playerName}</color> fue eliminado.");

        Debug.Log("Murió jugador: " + deadPlayerActorNumber);
        Debug.Log("Jugadores vivos restantes: " + alivePlayers.Count);

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (isResolvingMatch)
        {
            if (!finalDeathWindowPlayers.Contains(deadPlayerActorNumber))
            {
                finalDeathWindowPlayers.Add(deadPlayerActorNumber);
            }

            return;
        }

        if (alivePlayers.Count <= 1)
        {
            finalDeathWindowPlayers.Clear();
            finalDeathWindowPlayers.Add(deadPlayerActorNumber);

            StartCoroutine(ResolveMatchAfterDelay());
        }
    }

    private IEnumerator ResolveMatchAfterDelay()
    {
        isResolvingMatch = true;

        yield return new WaitForSeconds(simultaneousDeathWindow);

        if (matchEnded)
        {
            yield break;
        }

        if (alivePlayers.Count == 1)
        {
            int winnerActorNumber = GetOnlyAlivePlayer();

            SendMatchResult(
                WinnerResult,
                new int[] { winnerActorNumber }
            );
        }
        else if (alivePlayers.Count <= 0)
        {
            SendMatchResult(
                DrawResult,
                finalDeathWindowPlayers.ToArray()
            );
        }

        isResolvingMatch = false;
    }

    private int GetOnlyAlivePlayer()
    {
        foreach (int actorNumber in alivePlayers)
        {
            return actorNumber;
        }

        return -1;
    }

    private void SendMatchResult(int resultType, int[] resultPlayers)
    {
        if (matchEnded)
        {
            return;
        }

        matchEnded = true;

        object[] content = new object[]
        {
            resultType,
            resultPlayers
        };

        RaiseEventOptions raiseEventOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(
            MatchEndEventCode,
            content,
            raiseEventOptions,
            SendOptions.SendReliable
        );

        Debug.Log("Final de partida enviado a todos los jugadores de la sala.");
    }

    private void ShowMatchResult(int resultType, int[] resultPlayers)
    {
        if (resultShown)
        {
            return;
        }

        resultShown = true;
        matchEnded = true;

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultText == null)
        {
            return;
        }

        if (resultType == WinnerResult)
        {
            int winnerActorNumber = resultPlayers[0];

            resultText.text = "Ganó " + GetPlayerName(winnerActorNumber);
        }
        else if (resultType == DrawResult)
        {
            resultText.text = "Empate entre:\n";

            for (int i = 0; i < resultPlayers.Length; i++)
            {
                resultText.text += GetPlayerName(resultPlayers[i]);

                if (i < resultPlayers.Length - 1)
                {
                    resultText.text += "\n";
                }
            }
        }
    }

    public void RestartMatch()
    {
        if (alreadyRestarting) return;

        Time.timeScale = 1f;

        if (!PhotonNetwork.InRoom)
        {
            alreadyRestarting = true;
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            alreadyRestarting = true;
            PhotonNetwork.LoadLevel(gameplaySceneName);
        }
        else
        {
            photonView.RPC(nameof(RPC_RequestRestartMatch), RpcTarget.MasterClient);
        }
    }

    [PunRPC]
    private void RPC_RequestRestartMatch()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (alreadyRestarting) return;

        alreadyRestarting = true;
        Time.timeScale = 1f;
        PhotonNetwork.LoadLevel(gameplaySceneName);
    }

    private void SendRestartMatchToAll()
    {
        photonView.RPC(nameof(RPC_RestartMatchForAll), RpcTarget.All);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    [PunRPC]
    private void RPC_RestartMatchForAll()
    {
        if (alreadyRestarting)
        {
            return;
        }

        alreadyRestarting = true;

        Time.timeScale = 1f;

        Debug.Log("Reiniciando partida en cliente: " + PhotonNetwork.LocalPlayer.ActorNumber);

        SceneManager.LoadScene(gameplaySceneName);
    }

    public void ReturnToRoomMenu()
    {
        if (!PhotonNetwork.InRoom)
        {
            SceneManager.LoadScene(roomMenuSceneName);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            SendReturnToRoomMenuToAll();
        }
        else
        {
            photonView.RPC(nameof(RPC_RequestReturnToRoomMenu), RpcTarget.MasterClient);
        }
    }

    [PunRPC]
    private void RPC_RequestReturnToRoomMenu()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        SendReturnToRoomMenuToAll();
    }

    private void SendReturnToRoomMenuToAll()
    {
        photonView.RPC(nameof(RPC_ReturnToRoomMenuForAll), RpcTarget.All);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    [PunRPC]
    private void RPC_ReturnToRoomMenuForAll()
    {
        if (alreadyReturningToRoomMenu)
        {
            return;
        }

        alreadyReturningToRoomMenu = true;

        Debug.Log("Volviendo al RoomMenu en cliente: " + PhotonNetwork.LocalPlayer.ActorNumber);

        StartCoroutine(LeaveRoomAndLoadRoomMenu());
    }

    private IEnumerator LeaveRoomAndLoadRoomMenu()
    {
        Time.timeScale = 1f;

        returningToRoomMenu = true;

        yield return new WaitForSeconds(0.15f);

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            SceneManager.LoadScene(roomMenuSceneName);
        }
    }

    public override void OnLeftRoom()
    {
        if (returningToRoomMenu)
        {
            SceneManager.LoadScene(roomMenuSceneName);
        }
    }

    private string GetPlayerName(int actorNumber)
    {
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);

        if (player == null)
        {
            return "Jugador " + actorNumber;
        }

        if (string.IsNullOrEmpty(player.NickName))
        {
            return "Jugador " + actorNumber;
        }

        return player.NickName;
    }

    private string GetPlayerColorHex(int actorNumber)
    {
        int playerIndex = actorNumber - 1;

        if (playerIndex < 0) playerIndex = 0;
        if (playerIndex >= playerColors.Length) playerIndex = playerColors.Length - 1;

        return ColorUtility.ToHtmlStringRGB(playerColors[playerIndex]);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        string playerName = string.IsNullOrEmpty(otherPlayer.NickName) ? "Jugador " + otherPlayer.ActorNumber : otherPlayer.NickName;
        string colorHex = GetPlayerColorHex(otherPlayer.ActorNumber);
        ShowNotification($"<color=#{colorHex}>{playerName}</color> se desconectó de la partida.");

        if (matchEnded)
        {
            return;
        }

        alivePlayers.Remove(otherPlayer.ActorNumber);

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (alivePlayers.Count <= 1 && !isResolvingMatch)
        {
            StartCoroutine(ResolveMatchAfterDelay());
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (matchEnded)
        {
            return;
        }

        if (alivePlayers.Count <= 1 && !isResolvingMatch)
        {
            StartCoroutine(ResolveMatchAfterDelay());
        }
    }

    private void ShowNotification(string message)
    {
        if (notificationText == null) return;

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }
        notificationCoroutine = StartCoroutine(NotificationRoutine(message));
    }

    private IEnumerator NotificationRoutine(string message)
    {
        notificationText.text = message;
        notificationText.gameObject.SetActive(true);

        yield return new WaitForSeconds(notificationDuration);

        notificationText.gameObject.SetActive(false);
    }
}