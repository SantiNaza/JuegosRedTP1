using System;
using System.Collections.Generic;
using UnityEngine;

namespace TP.Config
{
    [Serializable]
    public struct WaveSpawnEntry
    {
        public EnemyTypeConfig type;
        public int count;
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public List<WaveSpawnEntry> entries = new List<WaveSpawnEntry>();
        public float spawnInterval = 0.8f;
        public float healthMultiplier = 1f;
        public float speedMultiplier = 1f;
        [Tooltip("Marca la wave como 'de élite' (se anuncia en el HUD).")]
        public bool isEliteWave;
    }

    [CreateAssetMenu(menuName = "TP/Wave Config", fileName = "WaveConfig")]
    public sealed class WaveConfig : ScriptableObject
    {
        public List<WaveDefinition> waves = new List<WaveDefinition>();
        public float firstWaveDelay = 3f;
        public float timeBetweenWaves = 5f;
        [Tooltip("Máximo de enemigos vivos a la vez (controla el pico de carga).")]
        public int maxAliveEnemies = 45;
        public float minSpawnDistanceToPlayer = 8f;
    }
}
