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

    // Volvemos a tu variable estática original, simple y directa.
    public static bool fuegoAmigoActivado = false;

    private bool partidaIniciada = false;
    private bool juegoTerminado = false;

    async void Awake()
    {
        // A PEDIDO TUYO: TODOS los jugadores (sin importar si son Host o Clientes) 
        // se conectan a la nube de Unity por su cuenta en el instante 0.
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        RemoteConfigService.Instance.FetchCompleted += AplicarConfiguracionRemota;

        // Cada jugador descarga su propia copia de las reglas antes de que empiece la acción
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
        // Solo el Host instancia los enemigos
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
        // CADA JUGADOR aplica los valores a sus variables locales. 
        // Como todos leen de la misma nube, todos van a tener exactamente la misma configuración.
        timeBetweenSpawns = RemoteConfigService.Instance.appConfig.GetFloat("SpawnRate", 1.0f);
        fuegoAmigoActivado = RemoteConfigService.Instance.appConfig.GetBool("fuegoAmigoActivado", false);

        Debug.Log("Live-Ops Local | Spawns: " + timeBetweenSpawns + "s | Fuego Amigo: " + fuegoAmigoActivado);
    }

    IEnumerator StartWave()
    {
        int zombiesToSpawn = currentWave * 5;

        for (int i = 0; i < zombiesToSpawn; i++)
        {
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

        // Hacemos que todos los jugadores chequeen si cambiaste algo en LiveOps entre rondas
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
        }

        // Solo el Host lanza la oleada
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