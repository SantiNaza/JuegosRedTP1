using System.Collections.Generic;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Efectos visuales (partículas + flashes de luz) reutilizables: un pool por tipo de efecto.
    /// Cada efecto vuelve al pool cuando termina su "lifetime"; las luces se apagan con un fade.
    /// </summary>
    public sealed class VfxSystem : IVfxService, IUpdatable
    {
        private sealed class VfxInstance
        {
            public GameObject Go;
            public Transform Tr;
            public ParticleSystem[] Particles;
            public Light Light;
            public float BaseIntensity;
            public float TimeLeft;
            public float Lifetime;
            public int Type;
        }

        private readonly Pool<VfxInstance>[] pools;
        private readonly float[] lifetimes;
        private readonly List<VfxInstance> active = new List<VfxInstance>(128);
        private readonly Transform root;

        public int ActiveCount => active.Count;

        public VfxSystem(VfxConfig config, bool poolingEnabled, Transform poolRoot)
        {
            root = poolRoot;
            int typeCount = System.Enum.GetValues(typeof(VfxType)).Length;
            pools = new Pool<VfxInstance>[typeCount];
            lifetimes = new float[typeCount];

            if (config == null) return;
            for (int i = 0; i < config.effects.Count; i++)
            {
                VfxEntry entry = config.effects[i];
                if (entry.prefab == null) continue;

                int typeIndex = (int)entry.type;
                GameObject prefab = entry.prefab;
                lifetimes[typeIndex] = entry.lifetime > 0f ? entry.lifetime : 1f;

                pools[typeIndex] = new Pool<VfxInstance>(
                    create: () => Create(prefab, typeIndex),
                    onGet: v => v.Go.SetActive(true),
                    onRelease: Deactivate,
                    onDestroy: v => Object.Destroy(v.Go),
                    prewarm: entry.prewarm,
                    poolingEnabled: poolingEnabled);
            }
        }

        public void Play(VfxType type, Vector3 position, Quaternion rotation)
        {
            Pool<VfxInstance> pool = pools[(int)type];
            if (pool == null) return;

            VfxInstance vfx = pool.Get();
            vfx.Tr.SetPositionAndRotation(position, rotation);
            vfx.Lifetime = lifetimes[(int)type];
            vfx.TimeLeft = vfx.Lifetime;

            for (int i = 0; i < vfx.Particles.Length; i++) vfx.Particles[i].Play(false);
            if (vfx.Light != null)
            {
                vfx.Light.enabled = true;
                vfx.Light.intensity = vfx.BaseIntensity;
            }
            active.Add(vfx);
        }

        public void Tick(float deltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                VfxInstance vfx = active[i];
                vfx.TimeLeft -= deltaTime;

                if (vfx.Light != null && vfx.Light.enabled)
                {
                    // Flash corto: la luz se apaga en el primer 30% de la vida del efecto.
                    float t = 1f - (vfx.Lifetime - vfx.TimeLeft) / (vfx.Lifetime * 0.3f);
                    if (t <= 0f) vfx.Light.enabled = false;
                    else vfx.Light.intensity = vfx.BaseIntensity * t;
                }

                if (vfx.TimeLeft > 0f) continue;

                // Quitamos con swap para no desplazar la lista (O(1)).
                int last = active.Count - 1;
                active[i] = active[last];
                active.RemoveAt(last);
                pools[vfx.Type].Release(vfx);
            }
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) pools[active[i].Type].Release(active[i]);
            active.Clear();
        }

        private VfxInstance Create(GameObject prefab, int typeIndex)
        {
            GameObject go = Object.Instantiate(prefab, root);
            var vfx = new VfxInstance
            {
                Go = go,
                Tr = go.transform,
                Particles = go.GetComponentsInChildren<ParticleSystem>(true),
                Light = go.GetComponentInChildren<Light>(true),
                Type = typeIndex
            };

            for (int i = 0; i < vfx.Particles.Length; i++)
            {
                ParticleSystem.MainModule main = vfx.Particles[i].main;
                main.playOnAwake = false;
            }
            if (vfx.Light != null) vfx.BaseIntensity = vfx.Light.intensity;
            return vfx;
        }

        private static void Deactivate(VfxInstance vfx)
        {
            for (int i = 0; i < vfx.Particles.Length; i++)
                vfx.Particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (vfx.Light != null) vfx.Light.enabled = false;
            vfx.Go.SetActive(false);
        }
    }
}
