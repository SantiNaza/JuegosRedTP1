using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerModel : MonoBehaviourPun
{
    [Header("Los 5 modelos (hijos del prefab, en orden 0..4)")]
    [SerializeField] private GameObject[] models;

    // El modelo que quedó activo, para que PlayerColor lo pueda teñir
    public GameObject ActiveModel { get; private set; }

    private void Awake()
    {
        int index = GetModelIndex();

        for (int i = 0; i < models.Length; i++)
        {
            bool on = (i == index);
            if (models[i] != null) models[i].SetActive(on);
            if (on) ActiveModel = models[i];
        }
    }

    private int GetModelIndex()
    {
        if (photonView.Owner != null &&
            photonView.Owner.CustomProperties.TryGetValue("model", out object m))
        {
            int idx = (int)m;
            if (idx >= 0 && idx < models.Length)
                return idx;
        }
        return 0;
    }
}