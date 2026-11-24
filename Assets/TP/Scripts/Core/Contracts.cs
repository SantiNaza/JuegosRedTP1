using UnityEngine;

namespace TP.Core
{
    // Interfaces de los servicios. Los sistemas dependen de estas interfaces, nunca de clases concretas
    // de otros sistemas (ej: el ProjectileSystem no conoce al EnemySystem, solo a IDamageService).

    public interface IInputService
    {
        Vector2 Move { get; }
        Vector3 PointerScreenPosition { get; }
        bool FireHeld { get; }
        bool ReloadPressed { get; }
        bool MeleePressed { get; }
        bool GrenadePressed { get; }
        bool GrenadeReleased { get; }
        bool SprintHeld { get; }
        bool PausePressed { get; }
    }

    public interface IPlayerService
    {
        bool IsSpawned { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }
        Transform Transform { get; }
        Transform FirePoint { get; }
        Vector3 AimPoint { get; }
        float Health { get; }
        float MaxHealth { get; }
        float Stamina01 { get; }
        /// <summary>Lo usa el arma para frenar al jugador mientras recarga.</summary>
        float MoveSpeedMultiplier { set; }
        void Spawn(Vector3 position);
        void TakeDamage(float amount);
        void Heal(float amount);
        void ApplyKnockback(Vector3 impulse);
    }

    /// <summary>Daño sobre enemigos, desacoplado del EnemySystem concreto.</summary>
    public interface IDamageService
    {
        /// <returns>true si el collider era un enemigo vivo.</returns>
        bool TryDamage(Collider collider, float damage, Vector3 knockback);
        int DamageInRadius(Vector3 center, float radius, float damage, float knockbackForce);
    }

    public interface IEnemyService
    {
        int AliveCount { get; }
        bool Spawn(Config.EnemyTypeConfig type, Vector3 position, float healthMultiplier, float speedMultiplier);
        void DespawnAll();
    }

    public interface IProjectileService
    {
        void Fire(Vector3 origin, Vector3 direction, float damage);
    }

    public interface IWeaponService
    {
        void ResetLoadout();
        void AddMagazines(int amount);
    }

    public interface IGrenadeService
    {
        void ResetLoadout();
        /// <returns>false si ya tenía el máximo.</returns>
        bool AddGrenades(int amount);
    }

    public interface IVfxService
    {
        void Play(VfxType type, Vector3 position, Quaternion rotation);
    }

    public interface IAudioService
    {
        void PlayMenuMusic();
        void PlayGameplayMusic();
        void Play(SfxType sfx);
    }

    public interface IProgressionService
    {
        int Xp { get; }
        int Level { get; }
        int TotalPoints { get; }
        int PointsLeft { get; }
        int MaxPerStat { get; }
        int SelectedModel { get; set; }
        int GetStatLevel(StatType stat);
        bool TryChangeStat(StatType stat, int delta);
        /// <summary>Bonificación total del stat (nivel * valor por punto).</summary>
        float GetBonus(StatType stat);
        void AddXp(int amount);
        void Save();
    }

    public interface IWaveService
    {
        int CurrentWave { get; }
        int TotalWaves { get; }
        int Remaining { get; }
        void StartWaves();
        void Stop();
    }

    public interface IGameFlow
    {
        GameState State { get; }
        void StartGame();
        void TogglePause();
        void Restart();
        void BackToMenu();
        void Quit();
    }

    /// <summary>Estadísticas de la partida actual (para la pantalla final y la XP).</summary>
    public interface IMatchStats
    {
        int Kills { get; }
        int XpEarned { get; }
        int WavesCleared { get; }
        float Duration { get; }
    }
}
