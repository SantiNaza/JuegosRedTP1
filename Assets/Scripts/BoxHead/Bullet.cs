using UnityEngine;
using Photon.Pun;

public class Bullet : MonoBehaviourPun
{
    [Header("Configuración de la Bala")]
    public float speed = 15f;
    public float lifeTime = 3f;

    private float damage;
    private int ownerViewID; // NUEVO: La credencial del jugador que disparó

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

    // NUEVO: Ahora recibe también el ID del tirador
    public void SetDamage(float weaponDamage, int shooterID)
    {
        damage = weaponDamage;
        ownerViewID = shooterID;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!photonView.IsMine) return;
        if (other.CompareTag("Player")) return;

        HealthSystem target = other.GetComponent<HealthSystem>();
        if (target != null)
        {
            // NUEVO: La bala despacha el daño utilizando el ID del jugador original
            target.photonView.RPC("RPC_TakeDamage", RpcTarget.All, damage, ownerViewID);
        }

        DestroyBullet();
    }

    private void DestroyBullet()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}