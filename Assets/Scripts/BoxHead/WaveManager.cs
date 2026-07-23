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

    // NUEVO: Ahora es un Array para aceptar varios tipos de zombies
    [Header("Arrastra los Prefabs de los Zombies aquí")]
    public GameObject[] zombiePrefabs; 

    private int currentWave = 1;
    private int zombiesAlive = 0;

    private float timeBetweenSpawns = 1f;

    public static bool fuegoAmigoActivado = false;

    // Variables para detectar la derrota
    private bool partidaIniciada = false;
    private bool juegoTerminado = false;

    async void Start()
    {
        // 1. PRIMERO inicializamos Unity Services y nos conectamos a la nube (solo el Host)
        if (PhotonNetwork.IsMasterClient)
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            RemoteConfigService.Instance.FetchCompleted += AplicarConfiguracionRemota;
        }

        // 2. DESPUÉS comprobamos la sala
        if (PhotonNetwork.InRoom)
        {
            ComprobarYArrancar();
        }
        else if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += ComprobarYArrancar;
        }
    }

    // Método Update para vigilar la vida del equipo
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
        timeBetweenSpawns = RemoteConfigService.Instance.appConfig.GetFloat("SpawnRate", 1.0f);
        fuegoAmigoActivado = RemoteConfigService.Instance.appConfig.GetBool("fuegoAmigoActivado", false);

        Debug.Log("Live-Ops | Spawns: " + timeBetweenSpawns + "s | Fuego Amigo: " + fuegoAmigoActivado);

        // NUEVO: El MasterClient le envía los valores oficiales a TODOS los jugadores conectados.
        photonView.RPC("RPC_SincronizarLiveOps", RpcTarget.AllBuffered, timeBetweenSpawns, fuegoAmigoActivado);
    }

    private void ComprobarYArrancar()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            CargarSpawnPointsDelMapa();

            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                // Pedimos los datos. YA NO iniciamos la oleada acá.
                // Esperamos pacientemente a que Unity responda.
                RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
            }
            else
            {
                // Failsafe: Si no hay internet, arrancamos con los valores base
                StartCoroutine(StartWave());
            }
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
            // NUEVO: Verificamos que el array tenga prefabs asignados
            if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            {
                Debug.LogError("¡Falta asignar los Prefabs de Zombies en el WaveManager!");
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
                
                // NUEVO: Elegimos un zombie al azar del array
                GameObject zombieElegido = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];

                // Instanciamos el objeto de sala para que persista con el Host Migration
                PhotonNetwork.InstantiateRoomObject(zombieElegido.name, spawnPos, Quaternion.identity);
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

    // NUEVO MÉTODO RPC
    [PunRPC]
    private void RPC_SincronizarLiveOps(float spawnRateRed, bool fuegoAmigoRed)
    {
        // Todos los clientes actualizan sus variables locales
        timeBetweenSpawns = spawnRateRed;
        fuegoAmigoActivado = fuegoAmigoRed;

        // Ahora que TODOS tienen la misma información, el Host arranca los zombis
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(StartWave());
        }
    }

    private IEnumerator WaitAndStartNextWave()
    {
        yield return new WaitForSeconds(3f);
        currentWave++;

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            // Pedimos los datos actualizados. Esto volverá a llamar a AplicarConfiguracionRemota, 
            // que sincronizará la red y llamará a StartWave() automáticamente.
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
        }
        else
        {
            StartCoroutine(StartWave());
        }
    }

    // Método RPC para decirle al HUD que muestre la derrota
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