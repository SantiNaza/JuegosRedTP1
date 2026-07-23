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

    [Header("Configuración de Granadas")]
    public string prefabGranada = "GranadaFisica";
    public int granadasActuales = 3;
    public float fuerzaLanzamiento = 15f;
    private bool isCooking = false;
    private float cookTimer = 3f;

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
    private GameObject grenadeTextObj;
    private TextMesh grenadeTextMesh;

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
    private Vector3 puntoDeMiraExacto; // Guarda la coordenada exacta del mouse
    private GameObject emptyTextObj; // Nuevo

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (!photonView.IsMine) return;

        currentAmmo = magCapacity;

        photonView.RPC("RPC_CrearTextoRecarga", RpcTarget.AllBuffered);
        photonView.RPC("RPC_CrearTextoDrop", RpcTarget.AllBuffered);
        photonView.RPC("RPC_CrearTextoGranadaJugador", RpcTarget.AllBuffered);
        photonView.RPC("RPC_CrearTextoSinMunicion", RpcTarget.AllBuffered);
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
        if (!photonView.IsMine || LocalPauseMenu.isPaused) return;

        // INICIO DE COCINADO
        if (Input.GetKeyDown(KeyCode.G) && granadasActuales > 0 && !isReloading && !isCooking)
        {
            photonView.RPC("RPC_EstadoCooking", RpcTarget.All, true);
        }

        if (isCooking)
        {
            if (Input.GetKeyUp(KeyCode.G))
            {
                photonView.RPC("RPC_EstadoCooking", RpcTarget.All, false);
                LanzarGranada(cookTimer);
            }
            else if (cookTimer <= 0)
            {
                photonView.RPC("RPC_EstadoCooking", RpcTarget.All, false);
                LanzarGranada(0.01f);
            }
        }

        // Siempre apuntamos al mouse, incluso cocinando
        if (!isMeleeAttacking)
        {
            ApuntarHaciaElMouse();
        }

        // BLOQUEO TÁCTICO: Si estamos recargando o cocinando, no podemos disparar ni soltar cargadores
        if (isReloading || isCooking) return;

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magCapacity && cargadoresActuales > 0)
        {
            StartCoroutine(Recargar());
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q) && cargadoresActuales > 0)
        {
            SoltarCargador();
        }

        if (Input.GetMouseButtonDown(1) && Time.time >= nextMeleeTime)
        {
            nextMeleeTime = Time.time + meleeCooldown;
            AtaqueMelee();
        }

        // EL BLOQUE CORREGIDO (Solo una vez)
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
            else
            {
                // No hay balas ni cargadores
                nextFireTime = Time.time + fireRate; // Evita spamear y saturar la red
                StartCoroutine(MostrarSinMunicion());
            }
        }
    }

    private void LanzarGranada(float tiempoRestante)
    {
        granadasActuales--;

        GameObject granada = PhotonNetwork.Instantiate(prefabGranada, firePoint.position, transform.rotation);

        // SOLUCIÓN A MUROS: Bajamos el ángulo de 0.3f a 0.05f para que sea un tiro casi horizontal
        Vector3 direccionVuelo = (transform.forward + (Vector3.up * 0.05f)).normalized;
        Vector3 velocidadFinal = direccionVuelo * fuerzaLanzamiento;

        GranadaFisica scriptGranada = granada.GetComponent<GranadaFisica>();
        if (scriptGranada != null)
        {
            scriptGranada.photonView.RPC("RPC_InicializarGranada", RpcTarget.All, tiempoRestante, velocidadFinal, photonView.ViewID);
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

    // --- MÉTODOS DE TEXTO 3D EN RED ---

    [PunRPC]
    private void RPC_CrearTextoGranadaJugador()
    {
        grenadeTextObj = new GameObject("TextoCooking");
        grenadeTextObj.transform.SetParent(this.transform);
        grenadeTextObj.transform.localPosition = new Vector3(0f, 3.5f, 0f);

        grenadeTextMesh = grenadeTextObj.AddComponent<TextMesh>();
        grenadeTextMesh.characterSize = 0.15f;
        grenadeTextMesh.fontSize = 40;
        grenadeTextMesh.anchor = TextAnchor.MiddleCenter;
        grenadeTextMesh.alignment = TextAlignment.Center;
        grenadeTextMesh.color = Color.red;

        grenadeTextObj.SetActive(false);
    }

    [PunRPC]
    public void RPC_EstadoCooking(bool cocinando)
    {
        isCooking = cocinando;
        if (cocinando)
        {
            cookTimer = 3f;
            if (grenadeTextObj != null) grenadeTextObj.SetActive(true);
        }
        else
        {
            if (grenadeTextObj != null) grenadeTextObj.SetActive(false);
        }
    }

    // ... (Mantener RPC_CrearTextoRecarga, RPC_CrearTextoDrop, RPC_MostrarTextoRecarga, RPC_MostrarTextoDrop iguales) ...
    private IEnumerator MostrarSinMunicion()
    {
        photonView.RPC("RPC_ToggleSinMunicion", RpcTarget.All, true);
        yield return new WaitForSeconds(1f);
        photonView.RPC("RPC_ToggleSinMunicion", RpcTarget.All, false);
    }

    [PunRPC]
    private void RPC_CrearTextoSinMunicion()
    {
        emptyTextObj = new GameObject("TextoSinMunicion");
        emptyTextObj.transform.SetParent(this.transform);
        emptyTextObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        TextMesh tm = emptyTextObj.AddComponent<TextMesh>();
        tm.text = "¡SIN MUNICIÓN!";
        tm.characterSize = 0.15f; tm.fontSize = 40; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.red;
        emptyTextObj.SetActive(false);
    }

    [PunRPC]
    private void RPC_ToggleSinMunicion(bool mostrar)
    {
        if (emptyTextObj != null) emptyTextObj.SetActive(mostrar);
    }

    // CORRECCIÓN PARA QUE EL COMPAÑERO LO VEA (Altura 1.5f en vez de 2.5f)
    [PunRPC]
    private void RPC_CrearTextoRecarga()
    {
        reloadTextObj = new GameObject("TextoRecarga");
        reloadTextObj.transform.SetParent(this.transform);
        reloadTextObj.transform.localPosition = new Vector3(0f, 1.5f, 0f); // <-- ALTURA CORREGIDA
        TextMesh tm = reloadTextObj.AddComponent<TextMesh>();
        tm.text = "¡RECARGANDO!";
        tm.characterSize = 0.15f; tm.fontSize = 40; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.yellow;
        reloadTextObj.SetActive(false);
    }

    [PunRPC]
    private void RPC_CrearTextoDrop()
    {
        dropTextObj = new GameObject("TextoDrop");
        dropTextObj.transform.SetParent(this.transform);
        dropTextObj.transform.localPosition = new Vector3(0f, 3.0f, 0f);
        dropTextMesh = dropTextObj.AddComponent<TextMesh>();
        dropTextMesh.characterSize = 0.12f; dropTextMesh.fontSize = 35; dropTextMesh.anchor = TextAnchor.MiddleCenter; dropTextMesh.alignment = TextAlignment.Center; dropTextMesh.color = Color.cyan;
        dropTextObj.SetActive(false);
    }

    [PunRPC]
    public void RPC_MostrarTextoRecarga(bool mostrar) { if (reloadTextObj != null) reloadTextObj.SetActive(mostrar); }

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

    private IEnumerator OcultarTextoDropRutina() { yield return new WaitForSeconds(2f); if (dropTextObj != null) dropTextObj.SetActive(false); }

    private void LateUpdate()
    {
        if (mainCamera != null)
        {
            if (reloadTextObj != null && reloadTextObj.activeSelf) reloadTextObj.transform.rotation = mainCamera.transform.rotation;
            if (dropTextObj != null && dropTextObj.activeSelf) dropTextObj.transform.rotation = mainCamera.transform.rotation;
            if (emptyTextObj != null && emptyTextObj.activeSelf) emptyTextObj.transform.rotation = mainCamera.transform.rotation; // Nuevo texto

            if (isCooking && grenadeTextObj != null)
            {
                cookTimer -= Time.deltaTime;
                grenadeTextMesh.text = Mathf.Max(0, cookTimer).ToString("F1");
                grenadeTextObj.transform.rotation = mainCamera.transform.rotation;
            }
        }
    }

    // ... (AtaqueMelee, ApuntarHaciaElMouse, DispararPistola, RPC_AnimacionMelee y RutinaPatadaRetroceso quedan exactamente iguales) ...
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

            // GUARDAMOS EL PUNTO EXACTO PARA LA BALA (alineado a la altura del cañón)
            puntoDeMiraExacto = new Vector3(puntoDeApunto.x, firePoint.position.y, puntoDeApunto.z);
        }
    }

    private void DispararPistola()
    {
        currentAmmo--;

        // Calculamos la rotación de la bala para que cruce desde el cañón hacia el mouse
        Vector3 direccionDisparo = (puntoDeMiraExacto - firePoint.position).normalized;
        Quaternion rotacionBala = Quaternion.LookRotation(direccionDisparo);

        // Instanciamos usando la nueva rotación corregida
        GameObject bulletObj = PhotonNetwork.Instantiate(bulletPrefabName, firePoint.position, rotacionBala);

        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDamage(gunDamage, photonView.ViewID);
        }
    }

    private void AtaqueMelee()
    {
        Vector3 centroDelGolpe = transform.position + (transform.forward * 1f);
        Collider[] impactados = Physics.OverlapSphere(centroDelGolpe, meleeRange);
        foreach (Collider col in impactados)
        {
            // Ignoramos a nosotros mismos para no patearnos solos
            if (col.gameObject == this.gameObject) continue;

            Vector3 direccionEmpuje = (col.transform.position - transform.position).normalized;
            direccionEmpuje.y = 0;

            PhotonView targetView = col.GetComponent<PhotonView>();
            if (targetView == null) continue;

            if (col.CompareTag("Zombie"))
            {
                // A los Zombis siempre se les hace daño y empuje
                HealthSystem targetHealth = col.GetComponent<HealthSystem>();
                if (targetHealth != null) targetView.RPC("RPC_TakeDamage", RpcTarget.All, kickDamage, photonView.ViewID);

                targetView.RPC("RPC_ApplyKnockback", RpcTarget.MasterClient, direccionEmpuje * kickForce);
            }
            else if (col.CompareTag("Player"))
            {
                // LÓGICA DE FUEGO AMIGO: Solo hace daño si LiveOps lo habilita
                if (WaveManager.fuegoAmigoActivado)
                {
                    HealthSystem allyHealth = col.GetComponent<HealthSystem>();
                    if (allyHealth != null)
                    {
                        targetView.RPC("RPC_TakeDamage", RpcTarget.All, kickDamage, photonView.ViewID);
                    }
                }

                // El empuje se lo aplicamos siempre (¡es genial para empujar a un aliado trabado!)
                targetView.RPC("RPC_ApplyKnockback", targetView.Owner, direccionEmpuje * kickForce);
            }
        }
        photonView.RPC("RPC_AnimacionMelee", RpcTarget.All);
    }

    [PunRPC] public void RPC_AnimacionMelee() { StartCoroutine(RutinaPatadaRetroceso()); }

    private IEnumerator RutinaPatadaRetroceso()
    {
        if (photonView.IsMine) isMeleeAttacking = true;
        Quaternion rotacionOriginal = transform.rotation;
        float duracion = 0.25f, t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float progreso = t / duracion;
            float anguloY = Mathf.SmoothStep(0f, 360f, progreso);
            transform.rotation = rotacionOriginal * Quaternion.Euler(0f, anguloY, 0f);
            yield return null;
        }
        transform.rotation = rotacionOriginal;
        if (photonView.IsMine) isMeleeAttacking = false;
    }
}