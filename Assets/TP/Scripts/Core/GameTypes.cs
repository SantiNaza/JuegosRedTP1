using UnityEngine;

namespace TP.Core
{
    public enum GameState { Menu, Playing, Paused, Victory, Defeat }

    public enum VfxType { MuzzleFlash = 0, ImpactWall = 1, ImpactBlood = 2, Explosion = 3, EnemyDeath = 4, Pickup = 5 }

    public enum SfxType { Shoot, UiClick, WaveStart, Explosion }

    /// <summary>Stats mejorables con los puntos que da el nivel (mismo sistema de XP del juego original).</summary>
    public enum StatType { Damage = 0, MagCapacity = 1, FireRate = 2, ReloadSpeed = 3, MaxHealth = 4, MoveSpeed = 5, Stamina = 6 }

    // ------------------------------------------------------------------
    // Eventos (structs → publicar no genera GC)
    // ------------------------------------------------------------------

    public struct GameStateChangedEvent { public GameState Previous; public GameState Current; }

    public struct EnemySpawnedEvent { public int AliveCount; }

    public struct EnemyKilledEvent { public Vector3 Position; public int XpReward; public int AliveCount; }

    public struct WaveStartedEvent { public int Wave; public int TotalWaves; public int EnemiesInWave; public bool HasElite; }

    public struct WaveClearedEvent { public int Wave; public int TotalWaves; }

    public struct WaveProgressEvent { public int Remaining; public int Total; }

    public struct AllWavesClearedEvent { public int TotalWaves; }

    public struct PlayerHealthChangedEvent { public float Current; public float Max; }

    public struct PlayerDiedEvent { public Vector3 Position; }

    public struct AmmoChangedEvent { public int InMagazine; public int MagazineSize; public int SpareMagazines; public bool Reloading; }

    public struct GrenadesChangedEvent { public int Count; public bool Cooking; public float CookTimeLeft; }

    public struct HudMessageEvent { public string Text; public Color Color; public float Duration; }

    /// <summary>Nombres de capas físicas. El editor (TP/1 - Configurar proyecto) las crea si no existen.</summary>
    public static class Layers
    {
        public const string EnemyName = "Enemy";
        public const string EnvironmentName = "Environment";
        public const string ProjectileName = "Projectile";
        public const string PlayerName = "Player";

        public static int Enemy { get; private set; }
        public static int Environment { get; private set; }
        public static int Projectile { get; private set; }
        public static int Player { get; private set; }

        public static int EnemyMask => 1 << Enemy;
        public static int BulletHitMask => (1 << Enemy) | (1 << Environment) | 1; // + Default

        public static void Initialize()
        {
            Enemy = Resolve(EnemyName);
            Environment = Resolve(EnvironmentName);
            Projectile = Resolve(ProjectileName);
            Player = Resolve(PlayerName);
        }

        private static int Resolve(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) return layer;
            Debug.LogWarning($"[Layers] Falta la capa '{layerName}'. Corré el menú TP/1 - Configurar proyecto.");
            return 0;
        }
    }

    /// <summary>
    /// Bandera que sobrevive a la recarga de escena para "Reiniciar" (arranca directo en la partida)
    /// en lugar de volver a mostrar el menú inicial.
    /// </summary>
    public static class SessionFlags
    {
        public static bool StartGameOnLoad;
    }
}
