using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.AI;

public class WaveManager : MonoBehaviourPun
{
    // YA NO se asigna a mano: lo tomamos del LevelGenerator
    private List<Vector3> enemySpawnPoints;

    [Header("Arrastra el Prefab del Zombie aquí")]
    public GameObject zombiePrefab;

    private int currentWave = 1;
    private int zombiesAlive = 0;

    private float timeBetweenSpawns = 1f;

    public static bool fuegoAmigoActivado = false;

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
        // Leemos la velocidad de los zombis que ya tenías
        timeBetweenSpawns = RemoteConfigService.Instance.appConfig.GetFloat("SpawnRate", 1.0f);

        // NUEVO: Leemos nuestra llave de fuego amigo desde la nube
        fuegoAmigoActivado = RemoteConfigService.Instance.appConfig.GetBool("fuego_amigo_activado", false);

        Debug.Log("Live-Ops | Spawns: " + timeBetweenSpawns + "s | Fuego Amigo: " + fuegoAmigoActivado);
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

            // Pegamos la posición al NavMesh más cercano (hasta 5m de distancia)
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
                // CAMBIAR ESTO:
                // PhotonNetwork.Instantiate(zombiePrefab.name, spawnPos, Quaternion.identity);

                // POR ESTO:
                PhotonNetwork.InstantiateRoomObject(zombiePrefab.name, spawnPos, Quaternion.identity);
                zombiesAlive++;
            }
            else
            {
                Debug.LogWarning("No se encontró NavMesh cerca del spawn de enemigos: " + spawnPos);
            }

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