using UnityEngine;
using Photon.Pun;
using System.Collections;

public class WaveManager : MonoBehaviourPun
{
    public Transform[] spawnPoints; 
    
    [Header("Arrastra el Prefab del Zombie aquí")]
    public GameObject zombiePrefab;
    
    private int currentWave = 1;
    private int zombiesAlive = 0;

    void Start()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom += ComprobarYArrancar;
        }
    }

    void OnDestroy()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnRoom -= ComprobarYArrancar;
        }
    }

    private void ComprobarYArrancar()
    {
        if (PhotonNetwork.IsMasterClient)
        {
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
            
            yield return new WaitForSeconds(1f); // Tiempo entre que sale un zombie y el siguiente
        }
    }

    // El HealthSystem del zombie debe llamar a este método justo antes de destruirse
    public void ZombieDied()
    {
        zombiesAlive--;
        
        // Si eliminamos a toda la oleada...
        if (zombiesAlive <= 0)
        {
            // ...iniciamos el contador de 3 segundos para la siguiente
            StartCoroutine(WaitAndStartNextWave());
        }
    }

    // NUEVA CORRUTINA: Maneja la pausa entre oleadas
    private IEnumerator WaitAndStartNextWave()
    {
        // Esperamos exactamente 3 segundos
        yield return new WaitForSeconds(3f);

        // Subimos el nivel de la oleada y la disparamos
        currentWave++;
        StartCoroutine(StartWave());
    }
}