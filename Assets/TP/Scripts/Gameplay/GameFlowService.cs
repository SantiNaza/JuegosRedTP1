using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Máquina de estados de la partida: Menú → Jugando ⇄ Pausa → Victoria / Derrota.
    /// La pausa detiene de verdad la lógica: el UpdateManager deja de llamar a los sistemas pausables
    /// y Time.timeScale = 0 congela física, NavMesh, animaciones y partículas.
    /// </summary>
    public sealed class GameFlowService : IGameFlow, IMatchStats, IUpdatable, IDisposable
    {
        private readonly UpdateManager updates;
        private readonly EventBus events;
        private readonly IInputService input;
        private readonly IPlayerService player;
        private readonly IWaveService waves;
        private readonly IWeaponService weapon;
        private readonly IGrenadeService grenades;
        private readonly IProgressionService progression;
        private readonly IAudioService audio;
        private readonly StatsConfig stats;
        private readonly MapData map;

        private float startTime;

        public GameState State { get; private set; } = GameState.Menu;
        public int Kills { get; private set; }
        public int XpEarned { get; private set; }
        public int WavesCleared { get; private set; }
        public float Duration { get; private set; }

        public GameFlowService(UpdateManager updates, EventBus events, IInputService input, IPlayerService player,
                               IWaveService waves, IWeaponService weapon, IGrenadeService grenades,
                               IProgressionService progression, IAudioService audio, StatsConfig stats, MapData map)
        {
            this.updates = updates;
            this.events = events;
            this.input = input;
            this.player = player;
            this.waves = waves;
            this.weapon = weapon;
            this.grenades = grenades;
            this.progression = progression;
            this.audio = audio;
            this.stats = stats;
            this.map = map;

            events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            events.Subscribe<WaveClearedEvent>(OnWaveCleared);
            events.Subscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            events.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        public void Dispose()
        {
            events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            events.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
            events.Unsubscribe<AllWavesClearedEvent>(OnAllWavesCleared);
            events.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void ShowMenu()
        {
            SetPausedWorld(false);
            audio.PlayMenuMusic();
            ChangeState(GameState.Menu);
        }

        public void StartGame()
        {
            if (State != GameState.Menu) return;

            Kills = 0;
            XpEarned = 0;
            WavesCleared = 0;
            Duration = 0f;
            startTime = Time.time;

            player.Spawn(map.playerSpawn);
            weapon.ResetLoadout();
            grenades.ResetLoadout();
            waves.StartWaves();

            audio.PlayGameplayMusic();
            SetPausedWorld(false);
            ChangeState(GameState.Playing);
        }

        /// <summary>No pausable: escucha la tecla de pausa incluso con el juego detenido.</summary>
        public void Tick(float deltaTime)
        {
            if (input.PausePressed && (State == GameState.Playing || State == GameState.Paused)) TogglePause();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                SetPausedWorld(true);
                ChangeState(GameState.Paused);
            }
            else if (State == GameState.Paused)
            {
                SetPausedWorld(false);
                ChangeState(GameState.Playing);
            }
        }

        public void Restart()
        {
            SessionFlags.StartGameOnLoad = true;
            ReloadScene();
        }

        public void BackToMenu()
        {
            SessionFlags.StartGameOnLoad = false;
            ReloadScene();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------ Eventos ------------------------------

        private void OnEnemyKilled(EnemyKilledEvent e)
        {
            if (State != GameState.Playing) return;
            Kills++;
            XpEarned += e.XpReward;
        }

        private void OnWaveCleared(WaveClearedEvent e)
        {
            WavesCleared = e.Wave;
            XpEarned += stats.waveClearXp;
            events.Publish(new HudMessageEvent { Text = "¡OLEADA SUPERADA!", Color = Color.green, Duration = 2f });
        }

        private void OnAllWavesCleared(AllWavesClearedEvent e) => EndMatch(true);

        private void OnPlayerDied(PlayerDiedEvent e) => EndMatch(false);

        private void EndMatch(bool victory)
        {
            if (State != GameState.Playing) return;

            if (victory) XpEarned += stats.victoryXp;
            Duration = Time.time - startTime;
            waves.Stop();

            progression.AddXp(XpEarned);
            progression.Save();

            SetPausedWorld(true);
            ChangeState(victory ? GameState.Victory : GameState.Defeat);
        }

        // ------------------------------ Internos ------------------------------

        private void SetPausedWorld(bool paused)
        {
            updates.SetPaused(paused);
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        private void ChangeState(GameState next)
        {
            GameState previous = State;
            State = next;
            events.Publish(new GameStateChangedEvent { Previous = previous, Current = next });
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
