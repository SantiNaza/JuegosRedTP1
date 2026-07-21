using UnityEngine;
using Photon.Pun;

public class GranadaFisica : MonoBehaviourPun
{
    [Header("Configuración de Explosión")]
    public float radioExplosion = 5f;
    public float danoExplosion = 80f;
    public float fuerzaEmpuje = 12f;

    [Header("Visuales")]
    public GameObject esferaAmarilla;
    public MeshRenderer modeloGranada;

    // Variables para el texto generado por código
    private GameObject textoObj;
    private TextMesh textoTemporizador;

    private float timer;
    private bool estaActiva = false;
    private int ownerViewID;
    private bool yaExploto = false;

    void Awake()
    {
        // HACEMOS LO MISMO QUE EN TU JUGADOR: Creamos el texto por código
        textoObj = new GameObject("TextoGranada");
        textoObj.transform.SetParent(this.transform);
        textoObj.transform.localPosition = new Vector3(0f, 1f, 0f);

        textoTemporizador = textoObj.AddComponent<TextMesh>();
        // Usamos TUS valores exactos que se ven geniales
        textoTemporizador.characterSize = 0.15f;
        textoTemporizador.fontSize = 40;
        textoTemporizador.anchor = TextAnchor.MiddleCenter;
        textoTemporizador.alignment = TextAlignment.Center;
        textoTemporizador.color = Color.red;
    }

    [PunRPC]
    public void RPC_InicializarGranada(float tiempoRestante, Vector3 velocidad, int shooterID)
    {
        timer = tiempoRestante;
        ownerViewID = shooterID;
        estaActiva = true;

        if (esferaAmarilla != null)
        {
            // Ajustamos el tamaño de la esfera visual al daño real
            esferaAmarilla.transform.localScale = new Vector3(radioExplosion * 2, radioExplosion * 2, radioExplosion * 2);
            esferaAmarilla.SetActive(false);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.velocity = velocidad; // Sincronizamos la fuerza del lanzamiento en red
        }
    }

    void Update()
    {
        if (!estaActiva || yaExploto) return;

        timer -= Time.deltaTime;

        if (textoTemporizador != null)
        {
            textoTemporizador.text = Mathf.Max(0, timer).ToString("F1");
        }

        if (timer <= 0f && photonView.IsMine)
        {
            Explotar();
        }
    }

    void LateUpdate()
    {
        // 1. Evitamos que el texto gire locamente cuando la granada rueda por el piso
        // 2. Lo hacemos mirar de frente a la cámara local
        if (!estaActiva || yaExploto || textoObj == null || Camera.main == null) return;

        textoObj.transform.position = transform.position + (Vector3.up * 1f);
        textoObj.transform.rotation = Camera.main.transform.rotation;
    }

    private void Explotar()
    {
        yaExploto = true;
        photonView.RPC("RPC_VisualExplosion", RpcTarget.All);

        Collider[] impactados = Physics.OverlapSphere(transform.position, radioExplosion);
        foreach (Collider col in impactados)
        {
            if (col.CompareTag("Zombie") || col.CompareTag("Player"))
            {
                Vector3 direccionEmpuje = (col.transform.position - transform.position).normalized;
                direccionEmpuje.y = 0;

                PhotonView targetView = col.GetComponent<PhotonView>();
                HealthSystem targetHealth = col.GetComponent<HealthSystem>();

                if (targetHealth != null && targetView != null)
                {
                    targetView.RPC("RPC_TakeDamage", RpcTarget.All, danoExplosion, ownerViewID);

                    if (col.CompareTag("Zombie"))
                    {
                        targetView.RPC("RPC_ApplyKnockback", RpcTarget.MasterClient, direccionEmpuje * fuerzaEmpuje);
                    }
                    else if (col.CompareTag("Player"))
                    {
                        targetView.RPC("RPC_ApplyKnockback", targetView.Owner, direccionEmpuje * fuerzaEmpuje);
                    }
                }
            }
        }

        Invoke(nameof(DestruirEnRed), 0.5f);
    }

    [PunRPC]
    private void RPC_VisualExplosion()
    {
        yaExploto = true;
        if (modeloGranada != null) modeloGranada.enabled = false;
        if (textoObj != null) textoObj.SetActive(false);
        if (esferaAmarilla != null) esferaAmarilla.SetActive(true);

        // FRENAMOS LA FÍSICA: Esto hace que la explosión se quede clavada en el piso
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void DestruirEnRed()
    {
        if (photonView.IsMine) PhotonNetwork.Destroy(gameObject);
    }
}