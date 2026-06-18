using UnityEngine;
using Photon.Pun;

public class Bullet : MonoBehaviourPun
{
    [Header("Configuración de la Bala")]
    public float speed = 15f;
    public float lifeTime = 3f;

    private float damage;
    private int ownerViewID; 

    void Start()
    {
        if (photonView.IsMine)
        {
            Invoke(nameof(DestroyBullet), lifeTime);
        }
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

  
    public void SetDamage(float weaponDamage, int shooterID)
    {
        damage = weaponDamage;
        ownerViewID = shooterID;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!photonView.IsMine) return;

        // Si chocamos a un jugador y el Live-Ops dice que NO hay fuego amigo, la bala se rompe y no hace daño.
        if (other.CompareTag("Player") && !WaveManager.fuegoAmigoActivado)
        {
            DestroyBullet();
            return; // Cortamos la ejecución acá
        }

        HealthSystem target = other.GetComponent<HealthSystem>();
        if (target != null)
        {
            target.photonView.RPC("RPC_TakeDamage", RpcTarget.All, damage, ownerViewID);
        }

        DestroyBullet();
    }

    private void DestroyBullet()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}