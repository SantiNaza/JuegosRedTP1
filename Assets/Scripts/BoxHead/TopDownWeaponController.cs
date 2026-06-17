using UnityEngine;
using Photon.Pun;
using System.Collections;

public class TopDownWeaponController : MonoBehaviourPun
{
    [Header("Configuración de Pistola (Modificable por Stats)")]
    public string bulletPrefabName = "Bullet";
    public float gunDamage = 25f;
    public float fireRate = 0.5f;
    public int magCapacity = 15; // Capacidad del cargador
    public float reloadSpeed = 2f; // Tiempo de recarga

    [Header("Configuración de Drop de Cargadores")]
    public string droppedMagPrefabName = "CargadorSuelto"; // Nombre del prefab en la carpeta Resources

    [Header("Estado del Arma")]
    private int currentAmmo;
    public int cargadoresActuales = 3;
    public bool isReloading { get; private set; } = false;
    private float nextFireTime = 0f;
    public Transform firePoint;
    private GameObject reloadTextObj;

    [Header("Configuración de Melee")]
    public float meleeDamage = 50f;
    public float meleeRange = 2f;
    public float meleeCooldown = 1f;
    private float nextMeleeTime = 0f;

    [Header("Configuración de Patada")]
    public float kickDamage = 10f;
    public float kickForce = 8f;

    public Camera mainCamera;

    void Start()
    {
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // Llenamos el cargador al inicio
        currentAmmo = magCapacity;

        // Creamos el texto de recarga sin tocar el editor de Unity
        photonView.RPC("RPC_CrearTextoRecarga", RpcTarget.AllBuffered);
    }

    public void AplicarMejoras(float dañoExtra, float fireRateMejora, int extraMag, float reloadMejora)
    {
        gunDamage += dañoExtra;
        fireRate -= fireRateMejora;
        magCapacity += extraMag;
        reloadSpeed -= reloadMejora;

        currentAmmo = magCapacity;
    }

    void Update()
    {
        if (!photonView.IsMine || isReloading) return;

        ApuntarHaciaElMouse();

        // Recarga manual
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magCapacity && cargadoresActuales > 0)
        {
            StartCoroutine(Recargar());
            return;
        }

        // Soltar Cargador para un compañero (Tecla G)
        if (Input.GetKeyDown(KeyCode.G) && cargadoresActuales > 0)
        {
            SoltarCargador();
        }

        // Disparo
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                nextFireTime = Time.time + fireRate;
                DispararPistola();
            }
            else if (cargadoresActuales > 0)
            {
                // Si no hay balas pero hay cargadores, forzamos recarga
                StartCoroutine(Recargar());
            }
        }

        // Melee
        if (Input.GetMouseButtonDown(1) && Time.time >= nextMeleeTime)
        {
            nextMeleeTime = Time.time + meleeCooldown;
            AtaqueMelee();
        }
    }

    private IEnumerator Recargar()
    {
        isReloading = true;

        photonView.RPC("RPC_MostrarTextoRecarga", RpcTarget.All, true);

        yield return new WaitForSeconds(reloadSpeed);

        // AHORA SÍ: Consumimos un cargador de la reserva
        cargadoresActuales--;
        currentAmmo = magCapacity;
        isReloading = false;

        photonView.RPC("RPC_MostrarTextoRecarga", RpcTarget.All, false);
    }

    // --- NUEVOS MÉTODOS PARA SOLTAR Y RECIBIR CARGADORES ---
    private void SoltarCargador()
    {
        cargadoresActuales--; // Restamos uno de nuestra reserva

        // Instanciamos el cargador en el piso usando Photon para que todos lo vean
        PhotonNetwork.Instantiate(droppedMagPrefabName, firePoint.position, Quaternion.identity);
    }

    public void RecibirCargador()
    {
        cargadoresActuales++; // Sumamos uno a la reserva
    }
    // --------------------------------------------------------

    [PunRPC]
    private void RPC_CrearTextoRecarga()
    {
        reloadTextObj = new GameObject("TextoRecarga");
        reloadTextObj.transform.SetParent(this.transform);
        reloadTextObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);

        TextMesh tm = reloadTextObj.AddComponent<TextMesh>();
        tm.text = "¡RECARGANDO!";
        tm.characterSize = 0.15f;
        tm.fontSize = 40;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.yellow;

        reloadTextObj.SetActive(false);
    }

    [PunRPC]
    public void RPC_MostrarTextoRecarga(bool mostrar)
    {
        if (reloadTextObj != null)
        {
            reloadTextObj.SetActive(mostrar);
        }
    }

    private void LateUpdate()
    {
        if (reloadTextObj != null && reloadTextObj.activeSelf && mainCamera != null)
        {
            reloadTextObj.transform.rotation = mainCamera.transform.rotation;
        }
    }

    private void ApuntarHaciaElMouse()
    {
        Plane planoSuelo = new Plane(Vector3.up, transform.position);
        Ray rayoMouse = mainCamera.ScreenPointToRay(Input.mousePosition);
        float distanciaAlPlano;

        if (planoSuelo.Raycast(rayoMouse, out distanciaAlPlano))
        {
            Vector3 puntoDeApunto = rayoMouse.GetPoint(distanciaAlPlano);
            Vector3 direccionMira = new Vector3(puntoDeApunto.x, transform.position.y, puntoDeApunto.z);
            transform.LookAt(direccionMira);
        }
    }

    private void DispararPistola()
    {
        currentAmmo--;

        GameObject bulletObj = PhotonNetwork.Instantiate(bulletPrefabName, firePoint.position, firePoint.rotation);
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDamage(gunDamage);
        }
    }

    private void AtaqueMelee()
    {
        Vector3 centroDelGolpe = transform.position + (transform.forward * 1f);
        Collider[] impactados = Physics.OverlapSphere(centroDelGolpe, meleeRange);

        foreach (Collider col in impactados)
        {
            if (col.gameObject == this.gameObject) continue;

            Vector3 direccionEmpuje = (col.transform.position - transform.position).normalized;
            direccionEmpuje.y = 0;

            PhotonView targetView = col.GetComponent<PhotonView>();
            if (targetView == null) continue;

            if (col.CompareTag("Zombie"))
            {
                HealthSystem targetHealth = col.GetComponent<HealthSystem>();
                if (targetHealth != null)
                {
                    targetView.RPC("RPC_TakeDamage", RpcTarget.All, kickDamage);
                }
                targetView.RPC("RPC_ApplyKnockback", RpcTarget.MasterClient, direccionEmpuje * kickForce);
            }
            else if (col.CompareTag("Player"))
            {
                targetView.RPC("RPC_ApplyKnockback", targetView.Owner, direccionEmpuje * kickForce);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + (transform.forward * 1f), meleeRange);
    }
}