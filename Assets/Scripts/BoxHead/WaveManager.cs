using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.RemoteConfig;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

public class WaveManager : MonoBehaviourPun
{
    // YA NO se asigna a mano: lo tomamos del LevelGenerator
    private List<Vector3> enemySpawnPoints;

    [Header("Arrastra el Prefab del Zombie aquí")]
    public GameObject zombiePrefab;

    private int currentWave = 1;
    private int zombiesAlive = 0;

    private float timeBetweenSpawns = 1f;

    async void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            ComprobarYArrancar();
        }
        else if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += ComprobarYArrancar;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            RemoteConfigService.Instance.FetchCompleted += AplicarConfiguracionRemota;
        }
    }

    void OnDestroy()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom -= ComprobarYArrancar;
        }

        RemoteConfigService.Instance.FetchCompleted -= AplicarConfiguracionRemota;
    }

    private void AplicarConfiguracionRemota(ConfigResponse response)
    {
        timeBetweenSpawns = RemoteConfigService.Instance.appConfig.GetFloat("SpawnRate", 1.0f);
        Debug.Log("Live-Ops: Tiempo entre spawns actualizado a: " + timeBetweenSpawns + " segundos");
    }

    private void ComprobarYArrancar()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Tomamos los spawnpoints rojos que generó el mapa
            CargarSpawnPointsDelMapa();

            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
            }

            StartCoroutine(StartWave());
        }
    }

    private void CargarSpawnPointsDelMapa()
    {
        LevelGenerator generator = FindObjectOfType<LevelGenerator>();

        if (generator == null)
        {
            Debug.LogError("WaveManager no encontró un LevelGenerator en la escena.");
            return;
        }

        enemySpawnPoints = generator.enemySpawns;

        if (enemySpawnPoints == null || enemySpawnPoints.Count == 0)
        {
            Debug.LogError("El LevelGenerator no tiene enemySpawns. " +
                "Asegurate de que genere el mapa ANTES que el WaveManager (Script Execution Order).");
        }
    }

    IEnumerator StartWave()
    {
        int zombiesToSpawn = currentWave * 5;

        for (int i = 0; i < zombiesToSpawn; i++)
        {
            if (zombiePrefab == null)
            {
                Debug.LogError("¡Falta asignar el Prefab del Zombie en el WaveManager!");
                break;
            }

            if (enemySpawnPoints == null || enemySpawnPoints.Count == 0)
            {
                Debug.LogError("No hay spawnpoints de enemigos en el mapa (pixeles rojos).");
                break;
            }

            Vector3 spawnPos = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Count)];
            PhotonNetwork.Instantiate(zombiePrefab.name, spawnPos, Quaternion.identity);
            zombiesAlive++;

            yield return new WaitForSeconds(timeBetweenSpawns);
        }
    }

    public void ZombieDied()
    {
        zombiesAlive--;

        if (zombiesAlive <= 0)
        {
            StartCoroutine(WaitAndStartNextWave());
        }
    }

    private IEnumerator WaitAndStartNextWave()
    {
        yield return new WaitForSeconds(3f);
        currentWave++;

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
        }

        StartCoroutine(StartWave());
    }

    public struct userAttributes { }
    public struct appAttributes { }
}