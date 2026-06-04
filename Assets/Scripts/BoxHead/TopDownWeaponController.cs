using UnityEngine;
using Photon.Pun;

public class TopDownWeaponController : MonoBehaviourPun
{
    [Header("Configuración de Pistola")]
    public string bulletPrefabName = "Bullet";
    public float gunDamage = 25f;
    public float fireRate = 0.5f; // Más rápido para un juego tipo Boxhead
    private float nextFireTime = 0f;
    public Transform firePoint; // Desde dónde sale la bala (ej. la punta del arma)

    [Header("Configuración de Melee")]
    public float meleeDamage = 50f;
    public float meleeRange = 2f;
    public float meleeCooldown = 1f;
    private float nextMeleeTime = 0f;

    [Header("Cámara")]
    public Camera mainCamera;

    void Start()
    {
        if (!photonView.IsMine)
        {
            enabled = false; // Desactivar si no es nuestro jugador
            return;
        }

        // Si no asignaste la cámara, la busca automáticamente (debe tener el tag MainCamera)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Orientar al jugador hacia el mouse constantemente (Opcional, pero ideal para Top-Down)
        ApuntarHaciaElMouse();

        // Disparo con clic izquierdo
        if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            DispararPistola();
        }

        // Golpe melee con clic derecho
        if (Input.GetMouseButtonDown(1) && Time.time >= nextMeleeTime)
        {
            nextMeleeTime = Time.time + meleeCooldown;
            AtaqueMelee();
        }
    }

    private void ApuntarHaciaElMouse()
    {
        // 1. Creamos un plano imaginario a la altura del jugador (normal hacia arriba)
        Plane planoSuelo = new Plane(Vector3.up, transform.position);

        // 2. Lanzamos un rayo desde la cámara pasando por el cursor del mouse
        Ray rayoMouse = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 3. Calculamos dónde choca el rayo con el plano
        float distanciaAlPlano;
        if (planoSuelo.Raycast(rayoMouse, out distanciaAlPlano))
        {
            Vector3 puntoDeApunto = rayoMouse.GetPoint(distanciaAlPlano);

            // 4. Hacemos que el personaje rote para mirar a ese punto
            Vector3 direccionMira = new Vector3(puntoDeApunto.x, transform.position.y, puntoDeApunto.z);
            transform.LookAt(direccionMira);
        }
    }

        private void DispararPistola()
    {
        // Instanciamos la bala en red usando el nombre del Prefab. 
        // Aparecerá en la posición y con la misma rotación que tiene el "firePoint".
        GameObject bulletObj = PhotonNetwork.Instantiate(bulletPrefabName, firePoint.position, firePoint.rotation);

        // Buscamos el script de la bala que acaba de nacer y le configuramos el daño
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDamage(gunDamage);
        }
    }

    private void AtaqueMelee()
    {
        // Creamos una zona de daño frente al jugador
        Vector3 centroDelGolpe = transform.position + (transform.forward * 1f);
        Collider[] impactados = Physics.OverlapSphere(centroDelGolpe, meleeRange);

        foreach (Collider collider in impactados)
        {
            if (collider.CompareTag("Zombie") || collider.CompareTag("Obstacle"))
            {
                HealthSystem target = collider.GetComponent<HealthSystem>();
                if (target != null)
                {
                    target.photonView.RPC("RPC_TakeDamage", RpcTarget.All, meleeDamage);
                }
            }
        }
    }

    // Dibuja la esfera del melee en el editor para que puedas ajustar el tamaño visualmente
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + (transform.forward * 1f), meleeRange);
    }
}