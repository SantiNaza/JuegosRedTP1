using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Weapon Config", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        public float damage = 25f;
        [Tooltip("Segundos entre disparos.")] public float fireInterval = 0.2f;
        public int magazineSize = 15;
        public float reloadTime = 1.6f;
        public int startingMagazines = 4;
        public GameObject bulletPrefab;
        public float bulletSpeed = 30f;
        public float bulletLifetime = 1.5f;
        public float bulletRadius = 0.12f;
    }
}
