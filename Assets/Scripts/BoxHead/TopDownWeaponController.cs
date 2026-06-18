using UnityEngine;
using Photon.Pun;
using System.Collections;

public class TopDownWeaponController : MonoBehaviourPun
{
    [Header("Configuración de Pistola (Modificable por Stats)")]
    public string bulletPrefabName = "Bullet";
    public float gunDamage = 25f;
    public float fireRate = 0.5f;
    public int magCapacity = 15;
    public float reloadSpeed = 2f;

    [Header("Configuración de Drop de Cargadores")]
    public string droppedMagPrefabName = "CargadorSuelto";

    [Header("Estado del Arma")]
    private int currentAmmo;
    public int cargadoresActuales = 3;
    public bool isReloading { get; private set; } = false;
    private float nextFireTime = 0f;
    public Transform firePoint;

    // --- Textos 3D ---
    private GameObject reloadTextObj;
    private GameObject dropTextObj;
    private TextMesh dropTextMesh;

    [Header("Configuración de Melee")]
    public float meleeDamage = 50f;
    public float meleeRange = 2f;
    public float meleeCooldown = 1f;
    private float nextMeleeTime = 0f;

    [Header("Configuración de Patada")]
    public float kickDamage = 10f;
    public float kickForce = 8f;
    private bool isMeleeAttacking = false;

    public Camera mainCamera;

    void Start()
    {
        // CLAVE 1: Todos los clones en la partida necesitan registrar la cámara local 
        // de esta compu para poder orientar sus carteles correctamente.
        if (mainCamera == null) mainCamera = Camera.main;

        // CLAVE 2: En vez de apagar el script con 'enabled = false', hacemos un return directo.
        // Así el script queda activo y permite que LateUpdate() funcione para los enemigos/aliados.
        if (!photonView.IsMine) return;

        currentAmmo = magCapacity;

        // Creamos los textos de recarga y drop al inicio (solo el dueño dispara el buffer)
        photonView.RPC("RPC_CrearTextoRecarga", RpcTarget.AllBuffered);
        photonView.RPC("RPC_CrearTextoDrop", RpcTarget.AllBuffered);
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
        // NUEVO: Agregamos "|| LocalPauseMenu.isPaused" para que el arma se bloquee si el menú está abierto
        if (!photonView.IsMine || isReloading || LocalPauseMenu.isPaused) return;

        // Si estamos dando la patada, bloqueamos la mira para permitir la inclinación al cielo
        if (!isMeleeAttacking)
        {
            ApuntarHaciaElMouse();
        }

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magCapacity && cargadoresActuales > 0)
        {
            StartCoroutine(Recargar());
            return;
        }

        if (Input.GetKeyDown(KeyCode.G) && cargadoresActuales > 0)
        {
            SoltarCargador();
        }

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                nextFireTime = Time.time + fireRate;
                DispararPistola();
            }
            else if (cargadoresActuales > 0)
            {
                StartCoroutine(Recargar());
            }
        }

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

        cargadoresActuales--;
        currentAmmo = magCapacity;
        isReloading = false;

        photonView.RPC("RPC_MostrarTextoRecarga", RpcTarget.All, false);
    }

    private void SoltarCargador()
    {
        cargadoresActuales--;

        PhotonNetwork.Instantiate(droppedMagPrefabName, firePoint.position, Quaternion.identity);

        photonView.RPC("RPC_MostrarTextoDrop", RpcTarget.All, cargadoresActuales);
    }

    public void RecibirCargador()
    {
        cargadoresActuales++;
    }

    // --- MÉTODOS DE TEXTO 3D ---

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
    private void RPC_CrearTextoDrop()
    {
        dropTextObj = new GameObject("TextoDrop");
        dropTextObj.transform.SetParent(this.transform);
        dropTextObj.transform.localPosition = new Vector3(0f, 3.0f, 0f);

        dropTextMesh = dropTextObj.AddComponent<TextMesh>();
        dropTextMesh.characterSize = 0.12f;
        dropTextMesh.fontSize = 35;
        dropTextMesh.anchor = TextAnchor.MiddleCenter;
        dropTextMesh.alignment = TextAlignment.Center;
        dropTextMesh.color = Color.cyan;

        dropTextObj.SetActive(false);
    }

    [PunRPC]
    public void RPC_MostrarTextoRecarga(bool mostrar)
    {
        if (reloadTextObj != null) reloadTextObj.SetActive(mostrar);
    }

    [PunRPC]
    public void RPC_MostrarTextoDrop(int restantes)
    {
        if (dropTextObj != null && dropTextMesh != null)
        {
            dropTextMesh.text = restantes + " cargadores restantes";
            dropTextObj.SetActive(true);

            StartCoroutine(OcultarTextoDropRutina());
        }
    }

    private IEnumerator OcultarTextoDropRutina()
    {
        yield return new WaitForSeconds(2f);
        if (dropTextObj != null)
        {
            dropTextObj.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        // Al estar activo el script para todos los jugadores, esta sección se va a ejecutar 
        // continuamente en cada réplica online, obligando a los carteles a mirar de frente 
        // a la cámara local sin importar cuánto rote el personaje sobre su propio eje.
        if (mainCamera != null)
        {
            if (reloadTextObj != null && reloadTextObj.activeSelf)
            {
                reloadTextObj.transform.rotation = mainCamera.transform.rotation;
            }

            if (dropTextObj != null && dropTextObj.activeSelf)
            {
                dropTextObj.transform.rotation = mainCamera.transform.rotation;
            }
        }
    }

    // --- FIN MÉTODOS DE TEXTO ---

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
            // Le pasamos también el ViewID de tu jugador
            bulletScript.SetDamage(gunDamage, photonView.ViewID);
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
                    // Sumamos el photonView.ViewID al final
                    targetView.RPC("RPC_TakeDamage", RpcTarget.All, kickDamage, photonView.ViewID);
                }
                targetView.RPC("RPC_ApplyKnockback", RpcTarget.MasterClient, direccionEmpuje * kickForce);
            }
            else if (col.CompareTag("Player"))
            {
                targetView.RPC("RPC_ApplyKnockback", targetView.Owner, direccionEmpuje * kickForce);
            }
        }
        // Lo agregás como la última línea de tu AtaqueMelee()
        photonView.RPC("RPC_AnimacionMelee", RpcTarget.All);

    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + (transform.forward * 1f), meleeRange);
    }

    [PunRPC]
    public void RPC_AnimacionMelee()
    {
        StartCoroutine(RutinaPatadaRetroceso());
    }

    private System.Collections.IEnumerator RutinaPatadaRetroceso()
    {
        // Bloqueamos el mouse para que no pelee contra la animación
        if (photonView.IsMine) isMeleeAttacking = true;

        // Memorizamos adónde estábamos apuntando
        Quaternion rotacionOriginal = transform.rotation;

    
        float duracion = 0.25f;
        float t = 0f;

        // Vuelta entera de 360 grados
        while (t < duracion)
        {
            t += Time.deltaTime;

            // Calculamos el porcentaje de la animación (de 0 a 1)
            float progreso = t / duracion;

            // SmoothStep hace que el giro empiece rápido y frene con un poco de suavidad al final
            float anguloY = Mathf.SmoothStep(0f, 360f, progreso);

            // Aplicamos el giro EXCLUSIVAMENTE en el eje Y. Cero resbalones.
            transform.rotation = rotacionOriginal * Quaternion.Euler(0f, anguloY, 0f);

            yield return null;
        }

        // Nos aseguramos de que quede clavado exactamente en la dirección original
        transform.rotation = rotacionOriginal;

        // Le devolvemos el control de la mira al jugador
        if (photonView.IsMine) isMeleeAttacking = false;
    }
}