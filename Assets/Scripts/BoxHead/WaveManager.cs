using UnityEngine;
using Photon.Pun;
using System.Collections;
using Unity.Services.RemoteConfig;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

public class WaveManager : MonoBehaviourPun
{
    public Transform[] spawnPoints;

    [Header("Arrastra el Prefab del Zombie aquí")]
    public GameObject zombiePrefab;

    private int currentWave = 1;
    private int zombiesAlive = 0;

    private float timeBetweenSpawns = 1f;

    async void Start()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += ComprobarYArrancar;
        }

        // 1. Inicializamos los servicios SIN IMPORTAR si somos Master Client todavía.
        // Esto toma unos milisegundos, así que lo hacemos apenas arranca la escena.
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // 2. Le decimos a Unity qué método ejecutar cuando termine de descargar datos
        RemoteConfigService.Instance.FetchCompleted += AplicarConfiguracionRemota;
    }

    void OnDestroy()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom -= ComprobarYArrancar;
        }

        // Es buena práctica desuscribirse de los eventos al destruir el objeto
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
            // 3. PRIMER PULL: Cuando el Master Client arranca la partida, pedimos los datos a la nube
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
            }

            StartCoroutine(StartWave());
        }
    }

    IEnumerator StartWave()
    {
        int zombiesToSpawn = currentWave * 5;

        for (int i = 0; i < zombiesToSpawn; i++)
        {
            if (zombiePrefab != null)
            {
                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                PhotonNetwork.Instantiate(zombiePrefab.name, spawnPoint.position, Quaternion.identity);
                zombiesAlive++;
            }
            else
            {
                Debug.LogError("¡Falta asignar el Prefab del Zombie en el WaveManager!");
                break;
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

        // 4. SEGUNDO PULL: Justo antes de la siguiente oleada, volvemos a consultar a la nube.
        // ¡Acá es donde se aplica tu cambio en vivo desde el Dashboard!
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
        }

        StartCoroutine(StartWave());
    }

    public struct userAttributes { }
    public struct appAttributes { }
}