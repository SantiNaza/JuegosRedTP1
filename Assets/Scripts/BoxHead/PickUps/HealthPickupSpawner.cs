using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class HealthPickupSpawner : MonoBehaviour
{
    [Header("Prefab del item (DEBE estar en una carpeta Resources)")]
    public GameObject healthPickupPrefab;

    [Header("Altura sobre el piso al spawnear")]
    public float spawnHeightOffset = 0.5f;

    private bool hasSpawned;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnPickups();
        }
        else if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += SpawnPickups;
        }
    }

    private void SpawnPickups()
    {
        if (hasSpawned) return;

        // Solo el MasterClient instancia los objetos en red
        if (!PhotonNetwork.IsMasterClient) return;

        LevelGenerator generator = FindObjectOfType<LevelGenerator>();
        if (generator == null)
        {
            Debug.LogError("HealthPickupSpawner no encontró un LevelGenerator.");
            return;
        }

        List<Vector3> healSpawns = generator.healSpawns;
        if (healSpawns == null || healSpawns.Count == 0)
        {
            Debug.LogWarning("No hay spawns de curación (pixeles magenta) en el mapa.");
            return;
        }

        hasSpawned = true;

        foreach (Vector3 pos in healSpawns)
        {
            Vector3 spawnPos = pos + Vector3.up * spawnHeightOffset;
            PhotonNetwork.Instantiate(healthPickupPrefab.name, spawnPos, Quaternion.identity);
        }
    }

    private void OnDestroy()
    {
        if (PhotonManager.Instance != null)
            PhotonManager.Instance.OnRoom -= SpawnPickups;
    }
}