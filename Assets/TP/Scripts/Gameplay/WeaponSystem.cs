using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Pistola con cargadores (como el original): disparo, recarga, cargadores de repuesto,
    /// patada cuerpo a cuerpo con empuje. Las mejoras de stats se aplican al iniciar la partida.
    /// </summary>
    public sealed class WeaponSystem : IWeaponService, IUpdatable
    {
        private readonly WeaponConfig config;
        private readonly PlayerConfig playerConfig;
        private readonly IInputService input;
        private readonly IPlayerService player;
        private readonly IProjectileService projectiles;
        private readonly IDamageService damage;
        private readonly IAudioService audio;
        private readonly IProgressionService progression;
        private readonly EventBus events;

        private float bulletDamage;
        private float fireInterval;
        private int magazineSize;
        private float reloadTime;

        private int ammo;
        private int spareMagazines;
        private bool reloading;
        private float reloadTimer;
        private float nextFireTime;
        private float nextKickTime;
        private float emptyMessageCooldown;

        public WeaponSystem(WeaponConfig config, PlayerConfig playerConfig, IInputService input, IPlayerService player,
                            IProjectileService projectiles, IDamageService damage, IAudioService audio,
                            IProgressionService progression, EventBus events)
        {
            this.config = config;
            this.playerConfig = playerConfig;
            this.input = input;
            this.player = player;
            this.projectiles = projectiles;
            this.damage = damage;
            this.audio = audio;
            this.progression = progression;
            this.events = events;
        }

        public void ResetLoadout()
        {
            bulletDamage = config.damage + progression.GetBonus(StatType.Damage);
            fireInterval = Mathf.Max(0.05f, config.fireInterval - progression.GetBonus(StatType.FireRate));
            magazineSize = config.magazineSize + Mathf.RoundToInt(progression.GetBonus(StatType.MagCapacity));
            reloadTime = Mathf.Max(0.3f, config.reloadTime - progression.GetBonus(StatType.ReloadSpeed));

            ammo = magazineSize;
            spareMagazines = config.startingMagazines;
            reloading = false;
            nextFireTime = 0f;
            nextKickTime = 0f;
            player.MoveSpeedMultiplier = 1f;
            PublishAmmo();
        }

        public void AddMagazines(int amount)
        {
            spareMagazines += amount;
            PublishAmmo();
        }

        public void Tick(float deltaTime)
        {
            if (!player.IsSpawned || !player.IsAlive) return;

            float now = Time.time;
            if (emptyMessageCooldown > 0f) emptyMessageCooldown -= deltaTime;

            if (reloading)
            {
                reloadTimer -= deltaTime;
                if (reloadTimer <= 0f) FinishReload();
                return;
            }

            if (input.MeleePressed && now >= nextKickTime)
            {
                nextKickTime = now + playerConfig.kickCooldown;
                Kick();
            }

            if (input.ReloadPressed && ammo < magazineSize && spareMagazines > 0)
            {
                StartReload();
                return;
            }

            if (!input.FireHeld || now < nextFireTime) return;

            if (ammo > 0)
            {
                nextFireTime = now + fireInterval;
                Shoot();
            }
            else if (spareMagazines > 0)
            {
                StartReload();
            }
            else if (emptyMessageCooldown <= 0f)
            {
                emptyMessageCooldown = 1f;
                events.Publish(new HudMessageEvent { Text = "¡SIN MUNICIÓN!", Color = Color.red, Duration = 1f });
            }
        }

        private void Shoot()
        {
            ammo--;
            Transform firePoint = player.FirePoint;
            Vector3 origin = firePoint != null ? firePoint.position : player.Position + Vector3.up;
            Vector3 direction = player.AimPoint - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.25f) direction = player.Transform.forward;

            projectiles.Fire(origin, direction, bulletDamage);
            audio.Play(SfxType.Shoot);
            PublishAmmo();
        }

        private void Kick()
        {
            Vector3 center = player.Position + player.Transform.forward * 1f + Vector3.up * 0.8f;
            damage.DamageInRadius(center, playerConfig.kickRange, playerConfig.kickDamage, playerConfig.kickForce);
            player.PlayMeleeSpin();
        }

        private void StartReload()
        {
            reloading = true;
            reloadTimer = reloadTime;
            player.MoveSpeedMultiplier = 0.75f;
            PublishAmmo();
        }

        private void FinishReload()
        {
            reloading = false;
            spareMagazines--;
            ammo = magazineSize;
            player.MoveSpeedMultiplier = 1f;
            PublishAmmo();
        }

        private void PublishAmmo()
        {
            events.Publish(new AmmoChangedEvent
            {
                InMagazine = ammo,
                MagazineSize = magazineSize,
                SpareMagazines = spareMagazines,
                Reloading = reloading
            });
        }
    }
}
