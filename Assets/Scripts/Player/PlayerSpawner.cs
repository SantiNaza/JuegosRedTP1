using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    // Ya no se asigna a mano: lo tomamos del LevelGenerator
    private List<Vector3> playerSpawns;

    private bool hasSpawned;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += SpawnPlayer;
        }
    }

    private void SpawnPlayer()
    {
        if (hasSpawned)
        {
            return;
        }

        // Tomamos los spawnpoints azules que generó el mapa
        CargarSpawnPointsDelMapa();

        hasSpawned = true;

        Vector3 spawnPos = GetSpawnPosition();

        PhotonNetwork.Instantiate(
            playerPrefab.name,
            spawnPos,
            Quaternion.identity,
            0
        );
    }

    private void CargarSpawnPointsDelMapa()
    {
        LevelGenerator generator = FindObjectOfType<LevelGenerator>();

        if (generator == null)
        {
            Debug.LogError("PlayerSpawner no encontró un LevelGenerator en la escena.");
            return;
        }

        playerSpawns = generator.playerSpawns;

        if (playerSpawns == null || playerSpawns.Count == 0)
        {
            Debug.LogError("El LevelGenerator no tiene playerSpawns (pixeles azules). " +
                "Asegurate de que genere el mapa ANTES que el PlayerSpawner (Script Execution Order).");
        }
    }

    private Vector3 GetSpawnPosition()
    {
        // Fallback: si no hay spawns en el mapa, nace en la posición de este objeto
        if (playerSpawns == null || playerSpawns.Count == 0)
        {
            return transform.position;
        }

        // Mismo indexado que los colores del MatchManager: ActorNumber - 1
        int playerIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;

        if (playerIndex < 0)
        {
            playerIndex = 0;
        }

        // Si hay más jugadores que spawns, hacemos wrap para no salir del rango
        playerIndex = playerIndex % playerSpawns.Count;

        return playerSpawns[playerIndex];
    }

    private void OnDestroy()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom -= SpawnPlayer;
        }
    }
}