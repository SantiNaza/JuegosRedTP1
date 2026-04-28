using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class MatchManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte PlayerDeathEventCode = 1;
    private const byte MatchEndEventCode = 2;

    private const byte RestartMatchRequestEventCode = 3;
    private const byte ReturnToRoomMenuEventCode = 4;

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

    private HashSet<int> alivePlayers = new HashSet<int>();
    private HashSet<int> deadPlayers = new HashSet<int>();

    private List<int> finalDeathWindowPlayers = new List<int>();

    private bool isResolvingMatch;
    private bool matchEnded;
    private bool resultShown;
    private bool loadingRoomMenu;

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

        if (eventCode == RestartMatchRequestEventCode)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                RestartMatchAsMaster();
            }
        }

        if (eventCode == ReturnToRoomMenuEventCode)
        {
            StartCoroutine(LeaveRoomAndLoadRoomMenu());
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
            Receivers = ReceiverGroup.All,
            CachingOption = EventCaching.AddToRoomCache
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
        if (PhotonNetwork.IsMasterClient)
        {
            RestartMatchAsMaster();
            return;
        }

        RaiseEventOptions raiseEventOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.MasterClient
        };

        PhotonNetwork.RaiseEvent(
            RestartMatchRequestEventCode,
            null,
            raiseEventOptions,
            SendOptions.SendReliable
        );
    }

    private void RestartMatchAsMaster()
    {
        Debug.Log("Reiniciando partida para todos los jugadores.");

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
        }

        PhotonNetwork.LoadLevel(gameplaySceneName);
    }

    public void ReturnToRoomMenu()
    {
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(
            ReturnToRoomMenuEventCode,
            null,
            raiseEventOptions,
            SendOptions.SendReliable
        );
    }

    private IEnumerator LeaveRoomAndLoadRoomMenu()
    {
        if (loadingRoomMenu)
        {
            yield break;
        }

        loadingRoomMenu = true;

        Debug.Log("Volviendo al RoomMenu para todos los jugadores.");

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();

            while (PhotonNetwork.InRoom)
            {
                yield return null;
            }
        }

        SceneManager.LoadScene(roomMenuSceneName);
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

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
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
}