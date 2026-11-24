using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        public GameObject prefab;
        public float moveSpeed = 5f;
        public float sprintMultiplier = 1.5f;
        public float maxStamina = 4f;
        public float maxHealth = 100f;
        public float kickDamage = 15f;
        public float kickRange = 2f;
        public float kickCooldown = 0.8f;
        public float kickForce = 8f;
    }
}
