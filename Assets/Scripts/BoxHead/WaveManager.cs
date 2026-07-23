using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.AI;
using Hashtable = ExitGames.Client.Photon.Hashtable; // NUEVO: Importante para la pizarra de Photon

public class WaveManager : MonoBehaviourPun
{
    private List<Vector3> enemySpawnPoints;

    [Header("Arrastra los Prefabs de los Zombies aquí")]
    public GameObject[] zombiePrefabs;

    private int currentWave = 1;
    private int zombiesAlive = 0;
    private float timeBetweenSpawns = 1f;

    // ¡LA REGLA ABSOLUTA! Ahora cada vez que una bala pregunte por esta variable, 
    // el código va a ir a leer directamente la configuración global de la sala.
    public static bool fuegoAmigoActivado
    {
        get
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("FuegoAmigo"))
            {
                return (bool)PhotonNetwork.CurrentRoom.CustomProperties["FuegoAmigo"];
            }
            return false; // Valor seguro por si todavía no se descargó la regla
        }
    }

    private bool partidaIniciada = false;
    private bool juegoTerminado = false;

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
        bool fuegoAmigoNube = RemoteConfigService.Instance.appConfig.GetBool("fuegoAmigoActivado", false);

        // REEMPLAZAMOS EL RPC: El Host escribe la regla en la "pizarra" de la sala para todos
        Hashtable props = new Hashtable();
        props.Add("FuegoAmigo", fuegoAmigoNube);
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        Debug.Log("Live-Ops | Spawns: " + timeBetweenSpawns + "s | Fuego Amigo guardado en la Sala.");
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

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
        }

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