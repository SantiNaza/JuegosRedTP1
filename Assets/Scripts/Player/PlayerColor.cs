using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
public class PlayerColor : MonoBehaviourPun
{
    [Header("Renderer")]
    [SerializeField] private Renderer playerRenderer;

    [Header("Player Colors")]
    [SerializeField]
    private Color[] playerColors = new Color[5]
    {
        Color.green,
        Color.red,
        Color.blue,
        Color.yellow,
        Color.black
    };

    private void Start()
    {
        if (playerRenderer == null)
        {
            playerRenderer = GetComponentInChildren<Renderer>();
        }

        int playerIndex = photonView.Owner.ActorNumber - 1;

        if (playerIndex < 0)
        {
            playerIndex = 0;
        }

        if (playerIndex >= playerColors.Length)
        {
            playerIndex = playerColors.Length - 1;
        }

        playerRenderer.material.color = playerColors[playerIndex];
    }
}