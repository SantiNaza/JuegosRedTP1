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
    public TextMesh textoTemporizador;

    private float timer;
    private bool estaActiva = false;
    private int ownerViewID;
    private bool yaExploto = false;

    [PunRPC]
    public void RPC_InicializarGranada(float tiempoRestante, Vector3 velocidad, int shooterID)
    {
        timer = tiempoRestante;
        ownerViewID = shooterID;
        estaActiva = true;

        if (esferaAmarilla != null)
        {
            // MAGIA: El código escala la esfera amarilla automáticamente para que mida el doble del radio (el diámetro).
            // Así, el área visual siempre será EXACTAMENTE IGUAL al área de daño matemático.
            esferaAmarilla.transform.localScale = new Vector3(radioExplosion * 2, radioExplosion * 2, radioExplosion * 2);
            esferaAmarilla.SetActive(false);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.velocity = velocidad;
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

    // NUEVO: Desvinculamos el texto del giro de la granada
    void LateUpdate()
    {
        if (!estaActiva || yaExploto || textoTemporizador == null) return;

        // 1. Evitamos que el texto "orbite" fijando su posición absoluta justo encima del centro
        textoTemporizador.transform.position = transform.position + (Vector3.up * 1f);

        // 2. Lo hacemos mirar a la cámara (Billboarding)
        if (Camera.main != null)
        {
            textoTemporizador.transform.rotation = Camera.main.transform.rotation;
        }
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
        if (textoTemporizador != null) textoTemporizador.gameObject.SetActive(false);
        if (esferaAmarilla != null) esferaAmarilla.SetActive(true);
    }

    private void DestruirEnRed()
    {
        if (photonView.IsMine) PhotonNetwork.Destroy(gameObject);
    }
}