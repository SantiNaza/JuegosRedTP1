using System.Collections.Generic;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Sistema de waves: cuenta regresiva → spawn escalonado (con tope de enemigos vivos) → pelea →
    /// wave superada → siguiente wave automática. Al completar todas, publica AllWavesClearedEvent (victoria).
    /// La dificultad crece por cantidad, mezcla de tipos, vida/velocidad y élites (ver WaveConfig).
    /// </summary>
    public sealed class WaveSystem : IWaveService, IUpdatable
    {
        private enum Phase { Idle, Countdown, Spawning, Finished }

        private readonly WaveConfig config;
        private readonly MapData map;
        private readonly IEnemyService enemies;
        private readonly IPlayerService player;
        private readonly IAudioService audio;
        private readonly EventBus events;
        private readonly System.Action<EnemyTypeConfig> prewarm;

        private readonly List<EnemyTypeConfig> spawnQueue = new List<EnemyTypeConfig>(128);
        private Phase phase = Phase.Idle;
        private int waveIndex = -1;
        private float timer;
        private int waveTotal;
        private int lastPublishedRemaining = -1;
        private bool prewarmed;

        public int CurrentWave => waveIndex + 1;
        public int TotalWaves => config.waves.Count;
        public int Remaining => spawnQueue.Count + enemies.AliveCount;

        public WaveSystem(WaveConfig config, MapData map, IEnemyService enemies, IPlayerService player,
                          IAudioService audio, EventBus events, System.Action<EnemyTypeConfig> prewarm)
        {
            this.config = config;
            this.map = map;
            this.enemies = enemies;
            this.player = player;
            this.audio = audio;
            this.events = events;
            this.prewarm = prewarm;
        }

        /// <summary>Crea de antemano los pools de todos los tipos de enemigo que van a aparecer.</summary>
        public void PrewarmPools()
        {
            if (prewarmed) return;
            prewarmed = true;
            for (int w = 0; w < config.waves.Count; w++)
                for (int i = 0; i < config.waves[w].entries.Count; i++)
                    prewarm?.Invoke(config.waves[w].entries[i].type);
        }

        public void StartWaves()
        {
            PrewarmPools();
            waveIndex = -1;
            BeginCountdown(config.firstWaveDelay);
        }

        public void Stop()
        {
            phase = Phase.Idle;
            spawnQueue.Clear();
        }

        public void Tick(float deltaTime)
        {
            switch (phase)
            {
                case Phase.Countdown:
                    timer -= deltaTime;
                    if (timer <= 0f) BeginWave(waveIndex + 1);
                    break;

                case Phase.Spawning:
                    TickSpawning(deltaTime);
                    PublishProgress();
                    if (spawnQueue.Count == 0 && enemies.AliveCount == 0) CompleteWave();
                    break;
            }
        }

        private void BeginCountdown(float delay)
        {
            phase = Phase.Countdown;
            timer = delay;
        }

        private void BeginWave(int index)
        {
            waveIndex = index;
            WaveDefinition wave = config.waves[index];

            spawnQueue.Clear();
            for (int i = 0; i < wave.entries.Count; i++)
            {
                WaveSpawnEntry entry = wave.entries[i];
                for (int n = 0; n < entry.count; n++) spawnQueue.Add(entry.type);
            }
            Shuffle(spawnQueue);

            waveTotal = spawnQueue.Count;
            timer = 0f;
            phase = Phase.Spawning;
            lastPublishedRemaining = -1;

            audio.Play(SfxType.WaveStart);
            events.Publish(new WaveStartedEvent
            {
                Wave = CurrentWave,
                TotalWaves = TotalWaves,
                EnemiesInWave = waveTotal,
                HasElite = wave.isEliteWave
            });
        }

        private void TickSpawning(float deltaTime)
        {
            if (spawnQueue.Count == 0) return;

            timer -= deltaTime;
            if (timer > 0f || enemies.AliveCount >= config.maxAliveEnemies) return;

            WaveDefinition wave = config.waves[waveIndex];
            int last = spawnQueue.Count - 1;
            if (enemies.Spawn(spawnQueue[last], PickSpawnPoint(), wave.healthMultiplier, wave.speedMultiplier))
            {
                spawnQueue.RemoveAt(last);
                timer = wave.spawnInterval;
            }
        }

        private void CompleteWave()
        {
            events.Publish(new WaveClearedEvent { Wave = CurrentWave, TotalWaves = TotalWaves });

            if (waveIndex >= config.waves.Count - 1)
            {
                phase = Phase.Finished;
                events.Publish(new AllWavesClearedEvent { TotalWaves = TotalWaves });
                return;
            }

            BeginCountdown(config.timeBetweenWaves);
        }

        private void PublishProgress()
        {
            int remaining = Remaining;
            if (remaining == lastPublishedRemaining) return;
            lastPublishedRemaining = remaining;
            events.Publish(new WaveProgressEvent { Remaining = remaining, Total = Mathf.Max(waveTotal, remaining) });
        }

        private Vector3 PickSpawnPoint()
        {
            List<Vector3> points = map.enemySpawns;
            if (points == null || points.Count == 0) return map.playerSpawn + new Vector3(10f, 0f, 10f);

            float minSqr = config.minSpawnDistanceToPlayer * config.minSpawnDistanceToPlayer;
            Vector3 playerPos = player.Position;
            Vector3 candidate = points[0];

            // Probamos algunos puntos al azar y nos quedamos con uno lejos del jugador.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                candidate = points[Random.Range(0, points.Count)];
                if ((candidate - playerPos).sqrMagnitude >= minSqr) break;
            }

            Vector2 jitter = Random.insideUnitCircle * 0.5f;
            return candidate + new Vector3(jitter.x, 0f, jitter.y);
        }

        private static void Shuffle(List<EnemyTypeConfig> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                EnemyTypeConfig tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
