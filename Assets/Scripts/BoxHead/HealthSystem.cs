using UnityEngine;
using Photon.Pun;

public class HealthSystem : MonoBehaviourPun
{
    public float maxHealth = 100f;
    private float currentHealth;
    
    // Si es true, el objeto se destruye al llegar a 0 (ej. Cajas, Zombies)
    public bool destroyOnDeath = true; 

    void Start()
    {
        currentHealth = maxHealth;
    }

    // Este método lo llamará el jugador al disparar o golpear
    [PunRPC]
    public void RPC_TakeDamage(float damage)
    {
        currentHealth -= damage;
        
        // Aquí podrías disparar un evento de Wwise 2021 si necesitas un sonido de impacto
        // AkSoundEngine.PostEvent("Play_Impact", gameObject);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (destroyOnDeath && photonView.IsMine) 
        {
            // 1. Buscamos al WaveManager en la escena
            WaveManager waveManager = FindObjectOfType<WaveManager>();
            
            // 2. Le avisamos que acabamos de morir
            if (waveManager != null)
            {
                waveManager.ZombieDied();
            }

            // 3. Nos destruimos de la red
            PhotonNetwork.Destroy(gameObject);
        }
    }
}