using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PhotonView))]
public class PressurePlate : MonoBehaviourPun
{
    public string playerTag = "Player";
    public PressureDoor door;

    [Header("Visual")]
    public Transform plateVisual;
    public float pressedDepth = 0.1f;

    public double PressedTime { get; private set; } = -100;

    private bool isLocalPlayerInside = false;
    private Transform localPlayerTransform;

    private GameObject interactTextObj;
    private TextMesh interactTextMesh;

    // ¡LA SOLUCIÓN! Guardar la posición inicial absoluta y usar un candado
    private Vector3 initialVisualPos;
    private bool isAnimating = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;

        // Guardamos la posición original UNA sola vez al nacer
        if (plateVisual != null) initialVisualPos = plateVisual.localPosition;

        CrearTextoInteraccion();
    }

    private void CrearTextoInteraccion()
    {
        interactTextObj = new GameObject("TextoPlaca_" + gameObject.name);
        interactTextMesh = interactTextObj.AddComponent<TextMesh>();

        interactTextMesh.text = "[E] PARA ABRIR";
        interactTextMesh.characterSize = 0.15f;
        interactTextMesh.fontSize = 40;
        interactTextMesh.anchor = TextAnchor.MiddleCenter;
        interactTextMesh.alignment = TextAlignment.Center;
        interactTextMesh.color = Color.green;

        interactTextObj.SetActive(false);
    }

    void Update()
    {
        if (isLocalPlayerInside && localPlayerTransform != null)
        {
            interactTextObj.transform.position = localPlayerTransform.position + new Vector3(0f, 3.5f, 0f);

            if (Camera.main != null)
            {
                interactTextObj.transform.rotation = Camera.main.transform.rotation;
            }

            // Validamos con el candado para no spamear la red ni la animación
            if (Input.GetKeyDown(KeyCode.E) && !isAnimating)
            {
                photonView.RPC(nameof(RPC_RegisterPress), RpcTarget.MasterClient, PhotonNetwork.Time);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv != null && pv.IsMine)
        {
            isLocalPlayerInside = true;
            localPlayerTransform = other.transform;
            interactTextObj.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        PhotonView pv = other.GetComponent<PhotonView>();
        if (pv != null && pv.IsMine)
        {
            isLocalPlayerInside = false;
            localPlayerTransform = null;
            interactTextObj.SetActive(false);
        }
    }

    [PunRPC]
    private void RPC_RegisterPress(double timestamp)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PressedTime = timestamp;

        // El servidor manda la orden a TODOS de reproducir la animación
        photonView.RPC(nameof(RPC_AnimarPlaca), RpcTarget.All);

        if (door != null) door.EvaluatePlates();
    }

    [PunRPC]
    private void RPC_AnimarPlaca()
    {
        if (!isAnimating) StartCoroutine(RutinaHundirPlaca());
    }

    private System.Collections.IEnumerator RutinaHundirPlaca()
    {
        if (plateVisual == null) yield break;

        isAnimating = true; // Cerramos el candado

        // Bajamos tomando como referencia la posición absoluta original
        plateVisual.localPosition = initialVisualPos - (Vector3.up * pressedDepth);

        yield return new WaitForSeconds(0.5f);

        // Devolvemos la placa exactamente a donde empezó
        plateVisual.localPosition = initialVisualPos;

        isAnimating = false; // Abrimos el candado
    }
}