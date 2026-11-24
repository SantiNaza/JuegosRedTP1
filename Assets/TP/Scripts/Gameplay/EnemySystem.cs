using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>Modelo de datos de un enemigo. El GameObject es solo su representación visual/física.</summary>
    public sealed class Enemy
    {
        public EnemyTypeConfig Type;
        public GameObject Go;
        public Transform Tr;
        public NavMeshAgent Agent;
        public Collider[] Colliders;
        public Renderer[] Renderers;
        public bool Alive;
        public float Health;
        public float MaxHealth;
        public float NextAttackTime;
        public float RepathTimer;
        public float FlashTimer;
        public float KnockbackTimer;
        public Vector3 KnockbackVelocity;
    }

    /// <summary>
    /// Toda la IA de enemigos en un único sistema: persecución con NavMesh (recalculo de camino
    /// escalonado cada N segundos, no cada frame), ataque cuerpo a cuerpo, knockback, daño, muerte,
    /// división al morir y pooling por tipo de enemigo.
    /// </summary>
    public sealed class EnemySystem : IEnemyService, IDamageService, IUpdatable
    {
        private const float FlashDuration = 0.1f;
        private const float KnockbackDuration = 0.2f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly IPlayerService player;
        private readonly IVfxService vfx;
        private readonly EventBus events;
        private readonly PerformanceConfig performance;
        private readonly Transform root;

        private readonly Dictionary<EnemyTypeConfig, Pool<Enemy>> pools = new Dictionary<EnemyTypeConfig, Pool<Enemy>>(8);
        private readonly Dictionary<int, Enemy> byCollider = new Dictionary<int, Enemy>(256);
        private readonly List<Enemy> active = new List<Enemy>(128);
        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly MaterialPropertyBlock flashBlock = new MaterialPropertyBlock();

        public int AliveCount => active.Count;

        public EnemySystem(IPlayerService player, IVfxService vfx, EventBus events,
                           PerformanceConfig performance, Transform poolRoot)
        {
            this.player = player;
            this.vfx = vfx;
            this.events = events;
            this.performance = performance;
            root = poolRoot;
            flashBlock.SetColor(ColorId, new Color(1f, 0.25f, 0.25f, 1f));
        }

        /// <summary>Crea el pool del tipo (y de su división) antes de la primera wave para evitar picos.</summary>
        public void Prewarm(EnemyTypeConfig type)
        {
            if (type == null) return;
            GetPool(type);
            if (type.splitInto != null && type.splitInto != type) GetPool(type.splitInto);
        }

        public bool Spawn(EnemyTypeConfig type, Vector3 position, float healthMultiplier, float speedMultiplier)
        {
            if (type == null || type.prefab == null) return false;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return false;

            Enemy e = GetPool(type).Get();
            e.Tr.SetPositionAndRotation(hit.position, Quaternion.identity);
            e.Go.SetActive(true);
            e.Agent.Warp(hit.position);
            e.Agent.speed = type.moveSpeed * speedMultiplier;
            e.Agent.isStopped = false;

            e.MaxHealth = type.maxHealth * healthMultiplier;
            e.Health = e.MaxHealth;
            e.Alive = true;
            e.NextAttackTime = 0f;
            e.KnockbackTimer = 0f;
            e.FlashTimer = 0f;
            // Escalonamos el primer recalculo para que no lo hagan todos en el mismo frame.
            e.RepathTimer = Random.value * performance.enemyRepathInterval;

            active.Add(e);
            events.Publish(new EnemySpawnedEvent { AliveCount = active.Count });
            return true;
        }

        public void Tick(float deltaTime)
        {
            bool hasTarget = player.IsSpawned && player.IsAlive;
            Vector3 target = player.Position;
            float now = Time.time;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Enemy e = active[i];

                if (e.FlashTimer > 0f)
                {
                    e.FlashTimer -= deltaTime;
                    if (e.FlashTimer <= 0f) SetFlash(e, false);
                }

                if (e.KnockbackTimer > 0f)
                {
                    e.KnockbackTimer -= deltaTime;
                    if (e.Agent.isOnNavMesh) e.Agent.Move(e.KnockbackVelocity * deltaTime);
                    continue;
                }

                if (!hasTarget)
                {
                    if (e.Agent.isOnNavMesh) e.Agent.isStopped = true;
                    continue;
                }

                Vector3 toTarget = target - e.Tr.position;
                toTarget.y = 0f;
                float range = e.Type.attackRange;

                if (toTarget.sqrMagnitude <= range * range)
                {
                    if (e.Agent.isOnNavMesh) e.Agent.isStopped = true;
                    if (toTarget.sqrMagnitude > 0.001f) e.Tr.rotation = Quaternion.LookRotation(toTarget);

                    if (now >= e.NextAttackTime)
                    {
                        e.NextAttackTime = now + e.Type.attackCooldown;
                        player.TakeDamage(e.Type.damage);
                    }
                    continue;
                }

                e.RepathTimer -= deltaTime;
                if (e.RepathTimer <= 0f && e.Agent.isOnNavMesh)
                {
                    e.RepathTimer = performance.enemyRepathInterval;
                    e.Agent.isStopped = false;
                    e.Agent.SetDestination(target);
                }
            }
        }

        // ------------------------------ Daño ------------------------------

        public bool TryDamage(Collider collider, float damage, Vector3 knockback)
        {
            if (collider == null || !byCollider.TryGetValue(collider.GetInstanceID(), out Enemy e) || !e.Alive)
                return false;

            ApplyDamage(e, damage, knockback);
            return true;
        }

        public int DamageInRadius(Vector3 center, float radius, float damage, float knockbackForce)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, Layers.EnemyMask,
                                                       QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                if (!byCollider.TryGetValue(overlapBuffer[i].GetInstanceID(), out Enemy e) || !e.Alive) continue;

                Vector3 dir = e.Tr.position - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
                ApplyDamage(e, damage, dir * knockbackForce);
                hits++;
            }
            return hits;
        }

        public void DespawnAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Enemy e = active[i];
                e.Alive = false;
                pools[e.Type].Release(e);
            }
            active.Clear();
        }

        private void ApplyDamage(Enemy e, float damage, Vector3 knockback)
        {
            e.Health -= damage;
            SetFlash(e, true);
            e.FlashTimer = FlashDuration;

            if (knockback.sqrMagnitude > 0.01f)
            {
                e.KnockbackVelocity = knockback;
                e.KnockbackTimer = KnockbackDuration;
                if (e.Agent.isOnNavMesh) e.Agent.isStopped = true;
            }

            if (e.Health <= 0f) Kill(e);
        }

        private void Kill(Enemy e)
        {
            e.Alive = false;
            Vector3 position = e.Tr.position;
            EnemyTypeConfig type = e.Type;

            active.Remove(e);
            pools[type].Release(e);

            vfx.Play(VfxType.EnemyDeath, position + Vector3.up * 0.8f, Quaternion.identity);

            // Enemigo que se divide al morir: los hijos se registran como enemigos de la misma wave.
            if (type.splitInto != null && type.splitCount > 0)
            {
                for (int i = 0; i < type.splitCount; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * 0.8f;
                    Spawn(type.splitInto, position + new Vector3(offset.x, 0f, offset.y), 1f, 1f);
                }
            }

            events.Publish(new EnemyKilledEvent { Position = position, XpReward = type.xpReward, AliveCount = active.Count });
        }

        // ------------------------------ Pooling ------------------------------

        private Pool<Enemy> GetPool(EnemyTypeConfig type)
        {
            if (pools.TryGetValue(type, out Pool<Enemy> pool)) return pool;

            pool = new Pool<Enemy>(
                create: () => CreateEnemy(type),
                onGet: null,
                onRelease: ReleaseEnemy,
                onDestroy: DestroyEnemy,
                prewarm: performance.enemyPrewarmPerType,
                poolingEnabled: performance.usePooling);
            pools.Add(type, pool);
            return pool;
        }

        private Enemy CreateEnemy(EnemyTypeConfig type)
        {
            GameObject go = Object.Instantiate(type.prefab, root);
            go.name = type.displayName;
            go.SetActive(false);
            SetLayerRecursively(go, Layers.Enemy);

            var e = new Enemy
            {
                Type = type,
                Go = go,
                Tr = go.transform,
                Agent = go.GetComponent<NavMeshAgent>(),
                Renderers = go.GetComponentsInChildren<Renderer>(true)
            };

            if (e.Agent == null) e.Agent = go.AddComponent<NavMeshAgent>();
            e.Agent.radius = 0.3f;
            e.Agent.autoBraking = false;
            e.Agent.angularSpeed = 720f;
            e.Agent.acceleration = 20f;
            e.Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            if (go.GetComponent<Collider>() == null) go.AddComponent<CapsuleCollider>();
            e.Colliders = go.GetComponentsInChildren<Collider>(true);
            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;

            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null) animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            for (int i = 0; i < e.Colliders.Length; i++) byCollider[e.Colliders[i].GetInstanceID()] = e;
            return e;
        }

        private void ReleaseEnemy(Enemy e)
        {
            SetFlash(e, false);
            e.Go.SetActive(false);
        }

        private void DestroyEnemy(Enemy e)
        {
            for (int i = 0; i < e.Colliders.Length; i++)
                if (e.Colliders[i] != null) byCollider.Remove(e.Colliders[i].GetInstanceID());
            Object.Destroy(e.Go);
        }

        private void SetFlash(Enemy e, bool on)
        {
            for (int i = 0; i < e.Renderers.Length; i++)
                e.Renderers[i].SetPropertyBlock(on ? flashBlock : null);
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
