using UnityEngine;
using Photon.Pun;

public class Bullet : MonoBehaviourPun
{
    [Header("Configuración de la Bala")]
    public float speed = 15f;
    public float lifeTime = 3f; // Cuánto dura antes de desaparecer en el aire
    private float damage; // El daño se lo pasará el jugador al disparar

    void Start()
    {
        // Solo el dueño de la bala inicia el temporizador de destrucción
        if (photonView.IsMine)
        {
            Invoke(nameof(DestroyBullet), lifeTime);
        }
    }

    void Update()
    {
        // La bala siempre avanza hacia "adelante" en su propio eje Z local
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    // Método público para que el arma le diga cuánto daño hace al nacer
    public void SetDamage(float weaponDamage)
    {
        damage = weaponDamage;
    }

    void OnTriggerEnter(Collider other)
    {
        // Regla de Oro: Solo el dueño de la bala calcula el daño. 
        // Si no hacemos esto, el Master y el Cliente enviarían el daño al mismo tiempo y restaría el doble.
        if (!photonView.IsMine) return;

        // Evitar que la bala choque con el jugador que la disparó
        if (other.CompareTag("Player")) return;

        // Verificamos si chocó contra algo que tenga vida (Zombie o Caja)
        HealthSystem target = other.GetComponent<HealthSystem>();
        if (target != null)
        {
            target.photonView.RPC("RPC_TakeDamage", RpcTarget.All, damage);
        }

        // Al chocar contra CUALQUIER cosa (pared, caja, zombie), la bala se destruye
        DestroyBullet();
    }

    private void DestroyBullet()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}