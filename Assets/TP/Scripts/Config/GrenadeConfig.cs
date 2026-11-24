using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Grenade Config", fileName = "GrenadeConfig")]
    public sealed class GrenadeConfig : ScriptableObject
    {
        public GameObject prefab;
        public int startingGrenades = 3;
        public int maxGrenades = 6;
        public float fuseTime = 3f;
        public float throwForce = 14f;
        public float radius = 5f;
        public float damage = 120f;
        public float knockbackForce = 10f;
        public int poolSize = 6;
    }
}
