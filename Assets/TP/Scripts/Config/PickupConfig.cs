using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Pickup Config", fileName = "PickupConfig")]
    public sealed class PickupConfig : ScriptableObject
    {
        public GameObject healthPrefab;
        public GameObject ammoPrefab;
        public GameObject grenadePrefab;
        public float healAmount = 40f;
        public int magazinesPerPickup = 2;
        public float respawnTime = 25f;
        public float pickupRadius = 1.2f;
    }
}
