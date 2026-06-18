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

    // NUEVO: Variables para detectar la derrota
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

    // NUEVO: Método Update para vigilar la vida del equipo
    void Update()
    {
        // Solo el Host vigila el estado de la partida
        if (!PhotonNetwork.IsMasterClient || juegoTerminado) return;

        // Buscamos cuántos objetos tienen el Tag "Player" (los caídos pierden este Tag)
        GameObject[] jugadoresVivos = GameObject.FindGameObjectsWithTag("Player");

        // Fase 1: Esperar a que alguien spawnee para no tirar derrota prematura
        if (!partidaIniciada)
        {
            if (jugadoresVivos.Length > 0)
            {
                partidaIniciada = true;
            }
        }
        // Fase 2: El juego ya empezó, vigilamos si el contador llega a cero
        else 
        {
            if (jugadoresVivos.Length == 0)
            {
                juegoTerminado = true;
                // Avisamos a todos los clientes que muestren su pantalla
                photonView.RPC("RPC_MostrarDerrota", RpcTarget.All);
            }
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
                
                // Instanciamos el objeto de sala para que persista con el Host Migration
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

    // NUEVO: Método RPC para decirle al HUD que muestre la derrota
    [PunRPC]
    private void RPC_MostrarDerrota()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.MostrarDerrota();
        }
    }

    public struct userAttributes { }
    public struct appAttributes { }
}