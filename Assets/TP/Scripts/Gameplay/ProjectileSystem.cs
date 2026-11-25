using System.Collections.Generic;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Balas reutilizables (pool). No usan colliders ni OnTriggerEnter: cada tick hacen un SphereCast
    /// del tramo recorrido, lo que evita que atraviesen enemigos a alta velocidad y no genera GC.
    /// </summary>
    public sealed class ProjectileSystem : IProjectileService, IUpdatable
    {
        private sealed class Projectile
        {
            public GameObject Go;
            public Transform Tr;
            public TrailRenderer Trail;
            public Vector3 Direction;
            public float Damage;
            public float TimeLeft;
        }

        private readonly WeaponConfig config;
        private readonly IDamageService damage;
        private readonly IVfxService vfx;
        private readonly Pool<Projectile> pool;
        private readonly List<Projectile> active = new List<Projectile>(64);
        private readonly Transform root;

        public int ActiveCount => active.Count;

        public ProjectileSystem(WeaponConfig config, IDamageService damage, IVfxService vfx,
                                PerformanceConfig performance, Transform poolRoot)
        {
            this.config = config;
            this.damage = damage;
            this.vfx = vfx;
            root = poolRoot;

            pool = new Pool<Projectile>(
                create: Create,
                onGet: p => p.Go.SetActive(true),
                onRelease: p => p.Go.SetActive(false),
                onDestroy: p => Object.Destroy(p.Go),
                prewarm: performance.bulletPrewarm,
                poolingEnabled: performance.usePooling);
        }

        public void Fire(Vector3 origin, Vector3 direction, float dmg)
        {
            if (config.bulletPrefab == null) return;

            direction.y = 0f;
            direction.Normalize();

            Projectile p = pool.Get();
            p.Tr.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            p.Direction = direction;
            p.Damage = dmg;
            p.TimeLeft = config.bulletLifetime;
            // El trail guarda posiciones viejas: al reutilizar la bala hay que limpiarlo.
            if (p.Trail != null) p.Trail.Clear();
            active.Add(p);

            vfx.Play(VfxType.MuzzleFlash, origin, p.Tr.rotation);
        }

        public void Tick(float deltaTime)
        {
            float step = config.bulletSpeed * deltaTime;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Projectile p = active[i];
                p.TimeLeft -= deltaTime;
                Vector3 position = p.Tr.position;

                if (Physics.SphereCast(position, config.bulletRadius, p.Direction, out RaycastHit hit, step,
                                       Layers.BulletHitMask, QueryTriggerInteraction.Ignore))
                {
                    Quaternion facing = Quaternion.LookRotation(hit.normal);
                    bool hitEnemy = damage.TryDamage(hit.collider, p.Damage, p.Direction * 2f);
                    vfx.Play(hitEnemy ? VfxType.ImpactBlood : VfxType.ImpactWall, hit.point, facing);
                    Remove(i);
                    continue;
                }

                if (p.TimeLeft <= 0f)
                {
                    Remove(i);
                    continue;
                }

                p.Tr.position = position + p.Direction * step;
            }
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) pool.Release(active[i]);
            active.Clear();
        }

        private void Remove(int index)
        {
            Projectile p = active[index];
            int last = active.Count - 1;
            active[index] = active[last];
            active.RemoveAt(last);
            pool.Release(p);
        }

        private Projectile Create()
        {
            GameObject go = Object.Instantiate(config.bulletPrefab, root);
            go.SetActive(false);
            // Por si el prefab trae colliders: las balas se resuelven por SphereCast, no por física.
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            return new Projectile
            {
                Go = go,
                Tr = go.transform,
                Trail = go.GetComponentInChildren<TrailRenderer>(true)
            };
        }
    }
}
