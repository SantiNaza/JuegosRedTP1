using System.Collections.Generic;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Granadas con "cocinado" (mantener G descuenta la mecha, como en el original), física real
    /// con Rigidbody, explosión en área con empuje y efecto visual + flash de luz. Todo con pooling.
    /// </summary>
    public sealed class GrenadeSystem : IGrenadeService, IUpdatable
    {
        private sealed class Grenade
        {
            public GameObject Go;
            public Transform Tr;
            public Rigidbody Body;
            public float FuseLeft;
        }

        private const float SelfDamageFactor = 0.3f;

        private readonly GrenadeConfig config;
        private readonly IInputService input;
        private readonly IPlayerService player;
        private readonly IDamageService damage;
        private readonly IVfxService vfx;
        private readonly IAudioService audio;
        private readonly EventBus events;
        private readonly Pool<Grenade> pool;
        private readonly List<Grenade> active = new List<Grenade>(8);
        private readonly Transform root;

        private int count;
        private bool cooking;
        private float cookTimer;
        private float lastPublishedCook = -1f;

        public GrenadeSystem(GrenadeConfig config, IInputService input, IPlayerService player, IDamageService damage,
                             IVfxService vfx, IAudioService audio, EventBus events,
                             PerformanceConfig performance, Transform poolRoot)
        {
            this.config = config;
            this.input = input;
            this.player = player;
            this.damage = damage;
            this.vfx = vfx;
            this.audio = audio;
            this.events = events;
            root = poolRoot;

            pool = new Pool<Grenade>(
                create: Create,
                onGet: g => g.Go.SetActive(true),
                onRelease: g => g.Go.SetActive(false),
                onDestroy: g => Object.Destroy(g.Go),
                prewarm: config.poolSize,
                poolingEnabled: performance.usePooling);
        }

        public void ResetLoadout()
        {
            count = config.startingGrenades;
            cooking = false;
            Publish();
        }

        public bool AddGrenades(int amount)
        {
            if (count >= config.maxGrenades) return false;
            count = Mathf.Min(config.maxGrenades, count + amount);
            Publish();
            return true;
        }

        public void Tick(float deltaTime)
        {
            TickThrown(deltaTime);

            if (!player.IsSpawned || !player.IsAlive)
            {
                cooking = false;
                return;
            }

            if (!cooking && input.GrenadePressed && count > 0 && config.prefab != null)
            {
                cooking = true;
                cookTimer = config.fuseTime;
                Publish();
            }

            if (!cooking) return;

            cookTimer -= deltaTime;
            if (input.GrenadeReleased || cookTimer <= 0f)
            {
                Throw(Mathf.Max(0.05f, cookTimer));
                return;
            }

            // Publicamos el contador del HUD solo cuando cambia la décima de segundo.
            float rounded = Mathf.Ceil(cookTimer * 10f);
            if (!Mathf.Approximately(rounded, lastPublishedCook))
            {
                lastPublishedCook = rounded;
                Publish();
            }
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) pool.Release(active[i]);
            active.Clear();
        }

        private void Throw(float fuse)
        {
            cooking = false;
            count--;
            Publish();

            Transform firePoint = player.FirePoint;
            Vector3 origin = firePoint != null ? firePoint.position : player.Position + Vector3.up;
            Vector3 direction = (player.Transform.forward + Vector3.up * 0.15f).normalized;

            Grenade g = pool.Get();
            g.Tr.SetPositionAndRotation(origin, Quaternion.identity);
            g.Body.velocity = direction * config.throwForce;
            g.Body.angularVelocity = Random.insideUnitSphere * 5f;
            g.FuseLeft = fuse;
            active.Add(g);
        }

        private void TickThrown(float deltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Grenade g = active[i];
                g.FuseLeft -= deltaTime;
                if (g.FuseLeft > 0f) continue;

                Explode(g.Tr.position);
                int last = active.Count - 1;
                active[i] = active[last];
                active.RemoveAt(last);
                pool.Release(g);
            }
        }

        private void Explode(Vector3 position)
        {
            damage.DamageInRadius(position, config.radius, config.damage, config.knockbackForce);
            vfx.Play(VfxType.Explosion, position, Quaternion.identity);
            audio.Play(SfxType.Explosion);

            if (!player.IsAlive) return;
            Vector3 toPlayer = player.Position - position;
            if (toPlayer.sqrMagnitude <= config.radius * config.radius)
            {
                player.TakeDamage(config.damage * SelfDamageFactor);
                toPlayer.y = 0f;
                player.ApplyKnockback(toPlayer.normalized * config.knockbackForce);
            }
        }

        private void Publish()
        {
            events.Publish(new GrenadesChangedEvent { Count = count, Cooking = cooking, CookTimeLeft = Mathf.Max(0f, cookTimer) });
        }

        private Grenade Create()
        {
            GameObject go = Object.Instantiate(config.prefab, root);
            go.SetActive(false);
            go.layer = Layers.Projectile;

            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            return new Grenade { Go = go, Tr = go.transform, Body = body };
        }
    }
}
