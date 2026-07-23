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
    private List<Vector3> enemySpawnPoints;

    [Header("Arrastra los Prefabs de los Zombies aquí")]
    public GameObject[] zombiePrefabs;

    private int currentWave = 1;
    private int zombiesAlive = 0;
    private float timeBetweenSpawns = 1f;

    public static bool fuegoAmigoActivado = false;

    private bool partidaIniciada = false;
    private bool juegoTerminado = false;

    async void Start()
    {
        // 1. Iniciamos la comprobación de sala normal e inmediata (Como tenías antes)
        if (PhotonNetwork.InRoom)
        {
            ComprobarYArrancar();
        }
        else if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += ComprobarYArrancar;
        }

        // 2. Iniciamos Unity Services en segundo plano sin frenar el juego
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

    void Update()
    {
        if (!PhotonNetwork.IsMasterClient || juegoTerminado) return;
        GameObject[] jugadoresVivos = GameObject.FindGameObjectsWithTag("Player");

        if (!partidaIniciada)
        {
            if (jugadoresVivos.Length > 0) partidaIniciada = true;
        }
        else
        {
            if (jugadoresVivos.Length == 0)
            {
                juegoTerminado = true;
                photonView.RPC("RPC_MostrarDerrota", RpcTarget.All);
            }
        }
    }

    void OnDestroy()
    {
        if (PhotonManager.Instance != null) PhotonManager.Instance.OnRoom -= ComprobarYArrancar;
        RemoteConfigService.Instance.FetchCompleted -= AplicarConfiguracionRemota;
    }

    private void ComprobarYArrancar()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            CargarSpawnPointsDelMapa();

            // ARRANCAMOS LA OLEADA INMEDIATAMENTE (Soluciona la falta de enemigos)
            StartCoroutine(StartWave());

            // Pedimos los datos en segundo plano
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
            }
        }
    }

    private void CargarSpawnPointsDelMapa()
    {
        LevelGenerator generator = FindObjectOfType<LevelGenerator>();
        if (generator != null)
        {
            enemySpawnPoints = generator.enemySpawns;
        }
    }

    private void AplicarConfiguracionRemota(ConfigResponse response)
    {
        timeBetweenSpawns = RemoteConfigService.Instance.appConfig.GetFloat("SpawnRate", 1.0f);
        fuegoAmigoActivado = RemoteConfigService.Instance.appConfig.GetBool("fuegoAmigoActivado", false);

        // Avisamos a todo el escuadrón
        photonView.RPC("RPC_SincronizarLiveOps", RpcTarget.AllBuffered, timeBetweenSpawns, fuegoAmigoActivado);
    }

    [PunRPC]
    private void RPC_SincronizarLiveOps(float spawnRateRed, bool fuegoAmigoRed)
    {
        timeBetweenSpawns = spawnRateRed;
        fuegoAmigoActivado = fuegoAmigoRed;
    }

    IEnumerator StartWave()
    {
        int zombiesToSpawn = currentWave * 5;

        for (int i = 0; i < zombiesToSpawn; i++)
        {
            // Seguridad: Si no hay prefabs o puntos, cortamos
            if (zombiePrefabs == null || zombiePrefabs.Length == 0) break;
            if (enemySpawnPoints == null || enemySpawnPoints.Count == 0) break;

            Vector3 spawnPos = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Count)];

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
                GameObject zombieElegido = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
                PhotonNetwork.InstantiateRoomObject(zombieElegido.name, spawnPos, Quaternion.identity);
                zombiesAlive++;
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

        // La siguiente oleada arranca SIEMPRE
        StartCoroutine(StartWave());
    }

    [PunRPC]
    private void RPC_MostrarDerrota()
    {
        if (HUDManager.Instance != null) HUDManager.Instance.MostrarDerrota();
    }

    public struct userAttributes { }
    public struct appAttributes { }
}