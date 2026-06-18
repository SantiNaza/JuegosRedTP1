using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class AmmoPickupSpawner : MonoBehaviour
{
    [Header("Prefab del item (DEBE estar en Resources)")]
    public GameObject ammoPickupPrefab;

    [Header("Altura sobre el piso")]
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
        if (!PhotonNetwork.IsMasterClient) return;

        LevelGenerator generator = FindObjectOfType<LevelGenerator>();
        if (generator == null)
        {
            Debug.LogError("AmmoPickupSpawner no encontró un LevelGenerator.");
            return;
        }

        List<Vector3> ammoSpawns = generator.ammoSpawns;
        if (ammoSpawns == null || ammoSpawns.Count == 0)
        {
            Debug.LogWarning("No hay spawns de munición (pixeles cyan) en el mapa.");
            return;
        }

        hasSpawned = true;

        foreach (Vector3 pos in ammoSpawns)
        {
            Vector3 spawnPos = pos + Vector3.up * spawnHeightOffset;
            PhotonNetwork.Instantiate(ammoPickupPrefab.name, spawnPos, Quaternion.identity);
        }
    }

    private void OnDestroy()
    {
        if (PhotonManager.Instance != null)
            PhotonManager.Instance.OnRoom -= SpawnPickups;
    }
}