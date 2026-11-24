using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Enemy Type", fileName = "Enemy_")]
    public sealed class EnemyTypeConfig : ScriptableObject
    {
        public string displayName = "Zombie";
        public GameObject prefab;
        public float maxHealth = 100f;
        public float moveSpeed = 2.5f;
        public float damage = 10f;
        public float attackRange = 1.6f;
        public float attackCooldown = 1f;
        public int xpReward = 5;
        [Tooltip("Si se asigna, al morir se divide en 'splitCount' enemigos de este tipo.")]
        public EnemyTypeConfig splitInto;
        public int splitCount;
    }
}
