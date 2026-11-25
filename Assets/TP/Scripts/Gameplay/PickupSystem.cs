using System.Collections.Generic;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Pickups de vida, cargadores y granadas en los puntos del mapa (colores magenta / cyan / naranja).
    /// Sin colliders ni OnTriggerEnter: se chequea la distancia al jugador en un tick de baja frecuencia.
    /// </summary>
    public sealed class PickupSystem : IUpdatable
    {
        private enum Kind { Health, Ammo, Grenade }

        private sealed class Pickup
        {
            public Kind Kind;
            public GameObject Go;
            public Transform Tr;
            public bool Available;
            public float RespawnTimer;
        }

        private readonly PickupConfig config;
        private readonly IPlayerService player;
        private readonly IWeaponService weapon;
        private readonly IGrenadeService grenades;
        private readonly IVfxService vfx;
        private readonly EventBus events;
        private readonly List<Pickup> pickups = new List<Pickup>(32);
        private readonly Transform root;

        public PickupSystem(PickupConfig config, MapData map, IPlayerService player, IWeaponService weapon,
                            IGrenadeService grenades, IVfxService vfx, EventBus events, Transform root)
        {
            this.config = config;
            this.player = player;
            this.weapon = weapon;
            this.grenades = grenades;
            this.vfx = vfx;
            this.events = events;
            this.root = root;

            Create(Kind.Health, config.healthPrefab, map.healthSpawns);
            Create(Kind.Ammo, config.ammoPrefab, map.ammoSpawns);
            Create(Kind.Grenade, config.grenadePrefab, map.grenadeSpawns);
        }

        /// <summary>Se registra con intervalo (ej. 0.1 s): no hace falta chequear cada frame.</summary>
        public void Tick(float deltaTime)
        {
            bool canCollect = player.IsSpawned && player.IsAlive;
            Vector3 playerPos = player.Position;
            float radiusSqr = config.pickupRadius * config.pickupRadius;

            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup p = pickups[i];

                if (!p.Available)
                {
                    p.RespawnTimer -= deltaTime;
                    if (p.RespawnTimer <= 0f) SetAvailable(p, true);
                    continue;
                }

                if (!canCollect) continue;
                Vector3 delta = p.Tr.position - playerPos;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr && TryApply(p)) Collect(p);
            }
        }

        public void ResetAll()
        {
            for (int i = 0; i < pickups.Count; i++) SetAvailable(pickups[i], true);
        }

        private bool TryApply(Pickup p)
        {
            switch (p.Kind)
            {
                case Kind.Health:
                    if (player.Health >= player.MaxHealth) return false;
                    player.Heal(config.healAmount);
                    events.Publish(new HudMessageEvent { Text = "+VIDA", Color = Color.green, Duration = 0.8f });
                    return true;
                case Kind.Ammo:
                    weapon.AddMagazines(config.magazinesPerPickup);
                    events.Publish(new HudMessageEvent { Text = "+CARGADORES", Color = Color.cyan, Duration = 0.8f });
                    return true;
                case Kind.Grenade:
                    if (!grenades.AddGrenades(1)) return false;
                    events.Publish(new HudMessageEvent { Text = "+GRANADA", Color = new Color(1f, 0.5f, 0f), Duration = 0.8f });
                    return true;
            }
            return false;
        }

        private void Collect(Pickup p)
        {
            vfx.Play(VfxType.Pickup, p.Tr.position, Quaternion.identity);
            SetAvailable(p, false);
            p.RespawnTimer = config.respawnTime;
        }

        private static void SetAvailable(Pickup p, bool available)
        {
            p.Available = available;
            p.Go.SetActive(available);
        }

        // Los pickups son pocos y fijos: se crean una vez al iniciar y después solo se activan/desactivan.
        private void Create(Kind kind, GameObject prefab, List<Vector3> points)
        {
            if (prefab == null || points == null) return;
            for (int i = 0; i < points.Count; i++)
            {
                GameObject go = Object.Instantiate(prefab, points[i] + Vector3.up * 0.5f, Quaternion.identity, root);
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                pickups.Add(new Pickup { Kind = kind, Go = go, Tr = go.transform, Available = true });
            }
        }
    }
}
