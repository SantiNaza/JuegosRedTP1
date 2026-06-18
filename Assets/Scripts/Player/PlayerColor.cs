using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
public class PlayerColor : MonoBehaviourPun
{
    [Header("Renderer")]
    [SerializeField] private Renderer playerRenderer;

    private void Start()
    {
        if (playerRenderer == null)
            playerRenderer = GetComponentInChildren<Renderer>();

        // LOG TEMPORAL
        if (photonView.Owner.CustomProperties.TryGetValue("color", out object dbg))
            Debug.Log($"[PlayerColor] Actor {photonView.Owner.ActorNumber} color guardado: {(int)dbg}");
        else
            Debug.Log($"[PlayerColor] Actor {photonView.Owner.ActorNumber} SIN property color");

        int index = GetColorIndex();
        playerRenderer.material.color = GameColors.Palette[index];
    }

    private int GetColorIndex()
    {
        // 1) Color elegido en el selector (CustomProperty "color")
        if (photonView.Owner.CustomProperties.TryGetValue("color", out object c))
        {
            int idx = (int)c;
            if (idx >= 0 && idx < GameColors.Palette.Length)
                return idx;
        }

        // 2) Fallback al esquema viejo por ActorNumber (por si entrás directo a Gameplay en tests)
        int fallback = photonView.Owner.ActorNumber - 1;
        if (fallback < 0) fallback = 0;
        if (fallback >= GameColors.Palette.Length) fallback = GameColors.Palette.Length - 1;
        return fallback;
    }
}