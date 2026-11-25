using System;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Sistema de XP, niveles y puntos para mejorar al personaje (portado del juego original:
    /// mismas claves de PlayerPrefs y el mismo "cifrado" XOR, así no se pierde el progreso guardado).
    /// </summary>
    public sealed class ProgressionService : IProgressionService
    {
        private const int XorKey = 1986;
        private const string XpKey = "experiencia_agente";
        private const string StatKeyPrefix = "stat_";
        private const string ModelKey = "preferred_model";

        private static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));
        private readonly StatsConfig config;
        private readonly string[] statKeys;

        public ProgressionService(StatsConfig config)
        {
            this.config = config;
            statKeys = new string[AllStats.Length];
            for (int i = 0; i < AllStats.Length; i++) statKeys[i] = StatKeyPrefix + AllStats[i];
        }

        public int Xp => PlayerPrefs.HasKey(XpKey) ? PlayerPrefs.GetInt(XpKey) ^ XorKey : 0;
        public int Level => Xp / Mathf.Max(1, config.xpPerLevel) + 1;
        public int TotalPoints => Level + config.basePoints;
        public int MaxPerStat => config.maxPerStat;

        public int PointsLeft
        {
            get
            {
                int spent = 0;
                for (int i = 0; i < AllStats.Length; i++) spent += GetStatLevel(AllStats[i]);
                return TotalPoints - spent;
            }
        }

        public int SelectedModel
        {
            get => PlayerPrefs.GetInt(ModelKey, 0);
            set => PlayerPrefs.SetInt(ModelKey, Mathf.Max(0, value));
        }

        public int GetStatLevel(StatType stat)
        {
            int raw = PlayerPrefs.GetInt(statKeys[(int)stat], 0);
            // Retrocompatibilidad con valores guardados sin cifrar (igual que el original).
            int level = raw <= 10 ? raw : raw ^ XorKey;
            return Mathf.Clamp(level, 0, config.maxPerStat);
        }

        public bool TryChangeStat(StatType stat, int delta)
        {
            int next = GetStatLevel(stat) + delta;
            if (next < 0 || next > config.maxPerStat) return false;
            if (delta > 0 && PointsLeft < delta) return false;

            PlayerPrefs.SetInt(statKeys[(int)stat], next ^ XorKey);
            PlayerPrefs.Save();
            return true;
        }

        public float GetBonus(StatType stat)
        {
            int index = (int)stat;
            float perPoint = config.perPoint != null && index < config.perPoint.Length ? config.perPoint[index] : 0f;
            return GetStatLevel(stat) * perPoint;
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(XpKey, (Xp + amount) ^ XorKey);
        }

        public void Save() => PlayerPrefs.Save();

        public static string DisplayName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Damage: return "Daño";
                case StatType.MagCapacity: return "Cargador";
                case StatType.FireRate: return "Cadencia";
                case StatType.ReloadSpeed: return "Recarga";
                case StatType.MaxHealth: return "Vida máxima";
                case StatType.MoveSpeed: return "Velocidad";
                case StatType.Stamina: return "Resistencia";
                default: return stat.ToString();
            }
        }

        public static StatType[] Stats => AllStats;
    }
}
