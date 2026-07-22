using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class GrenadePickupSpawner : MonoBehaviour
{
    [Header("Prefab del item (DEBE estar en Resources)")]
    public GameObject grenadePickupPrefab;

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
            Debug.LogError("GrenadePickupSpawner no encontró un LevelGenerator.");
            return;
        }

        // ATENCIÓN ACÁ: Necesitás agregar esta lista a tu LevelGenerator
        List<Vector3> grenadeSpawns = generator.grenadeSpawns;

        if (grenadeSpawns == null || grenadeSpawns.Count == 0)
        {
            Debug.LogWarning("No hay spawns de granadas en el mapa.");
            return;
        }

        hasSpawned = true;

        foreach (Vector3 pos in grenadeSpawns)
        {
            Vector3 spawnPos = pos + Vector3.up * spawnHeightOffset;
            PhotonNetwork.Instantiate(grenadePickupPrefab.name, spawnPos, Quaternion.identity);
        }
    }

    private void OnDestroy()
    {
        if (PhotonManager.Instance != null)
            PhotonManager.Instance.OnRoom -= SpawnPickups;
    }
}