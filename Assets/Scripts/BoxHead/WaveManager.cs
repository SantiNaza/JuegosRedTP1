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

    [Header("ORDEN OBLIGATORIO: 0=Normal, 1=Fast, 2=Tank")]
    public GameObject[] zombiePrefabs;

    private int currentWave = 1;
    private int zombiesAlive = 0;

    // Variables de LiveOps
    private float timeBetweenSpawns = 1f;
    private int pesoNormal = 70;
    private int pesoFast = 20;
    private int pesoTank = 10;

    public static bool fuegoAmigoActivado = false;

    private bool partidaIniciada = false;
    private bool juegoTerminado = false;

    async void Awake()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        RemoteConfigService.Instance.FetchCompleted += AplicarConfiguracionRemota;

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
        }
    }

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            ComprobarYArrancar();
        }
        else if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += ComprobarYArrancar;
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
            StartCoroutine(StartWave());
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
        fuegoAmigoActivado = RemoteConfigService.Instance.appConfig.GetBool("fuegoAmigoActivado", false);
        timeBetweenSpawns = RemoteConfigService.Instance.appConfig.GetFloat("SpawnRate", 1.0f);

        // NUEVO: Leemos los pesos desde la nube (si no existen, usan los valores por defecto que ponemos acá)
        pesoNormal = RemoteConfigService.Instance.appConfig.GetInt("pesoNormal", 70);
        pesoFast = RemoteConfigService.Instance.appConfig.GetInt("pesoFast", 20);
        pesoTank = RemoteConfigService.Instance.appConfig.GetInt("pesoTank", 10);

        Debug.Log($"LiveOps | Rate: {timeBetweenSpawns}s | Pesos: N:{pesoNormal} F:{pesoFast} T:{pesoTank}");
    }

    IEnumerator StartWave()
    {
        // Más zombis a medida que avanzan las oleadas
        int zombiesToSpawn = currentWave * 5;

        for (int i = 0; i < zombiesToSpawn; i++)
        {
            if (zombiePrefabs == null || zombiePrefabs.Length < 3)
            {
                Debug.LogError("Faltan asignar los 3 tipos de zombis en el Inspector.");
                break;
            }
            if (enemySpawnPoints == null || enemySpawnPoints.Count == 0) break;

            Vector3 spawnPos = enemySpawnPoints[Random.Range(0, enemySpawnPoints.Count)];

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;

                // --- EL SISTEMA DE PESOS (PROBABILIDADES) ---
                GameObject zombieElegido = null;
                int totalWeight = pesoNormal + pesoFast + pesoTank;
                int randomValue = Random.Range(0, totalWeight);

                if (randomValue < pesoNormal)
                {
                    zombieElegido = zombiePrefabs[0]; // Element 0: Normal
                }
                else if (randomValue < pesoNormal + pesoFast)
                {
                    zombieElegido = zombiePrefabs[1]; // Element 1: Fast
                }
                else
                {
                    zombieElegido = zombiePrefabs[2]; // Element 2: Tank
                }
                // ---------------------------------------------

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

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(StartWave());
        }
    }

    [PunRPC]
    private void RPC_MostrarDerrota()
    {
        if (HUDManager.Instance != null) HUDManager.Instance.MostrarDerrota();
    }

    public struct userAttributes { }
    public struct appAttributes { }
}