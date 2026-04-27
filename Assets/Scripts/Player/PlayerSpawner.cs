using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints = new Transform[5];

    private bool hasSpawned;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            PhotonManager.instance.OnRoom += SpawnPlayer;
        }
    }

    private void SpawnPlayer()
    {
        if (hasSpawned)
        {
            return;
        }

        hasSpawned = true;

        Transform selectedSpawn = GetSpawnPoint();

        PhotonNetwork.Instantiate(
            playerPrefab.name,
            selectedSpawn.position,
            selectedSpawn.rotation,
            0
        );
    }

    private Transform GetSpawnPoint()
    {
        int playerIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;

        if (playerIndex < 0)
        {
            playerIndex = 0;
        }

        if (playerIndex >= spawnPoints.Length)
        {
            playerIndex = spawnPoints.Length - 1;
        }

        if (spawnPoints[playerIndex] != null)
        {
            return spawnPoints[playerIndex];
        }

        return transform;
    }

    private void OnDestroy()
    {
        if (PhotonManager.instance != null)
        {
            PhotonManager.instance.OnRoom -= SpawnPlayer;
        }
    }
}