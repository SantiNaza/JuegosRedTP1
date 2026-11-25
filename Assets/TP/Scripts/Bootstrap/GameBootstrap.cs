using UnityEngine;
using UnityEngine.AI;
using TP.Config;
using TP.Core;
using TP.Gameplay;
using TP.UI;

namespace TP.Bootstrap
{
    /// <summary>
    /// ÚNICO MonoBehaviour propio del proyecto y único Singleton permitido (composition root).
    /// En Awake crea todos los servicios y sistemas (clases C# puras), les inyecta sus dependencias,
    /// los registra en el Service Locator y en el UpdateManager, e inyecta el UpdateManager en el
    /// PlayerLoop de Unity. No tiene Update/FixedUpdate/LateUpdate.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig config = null;
        [SerializeField] private Camera mainCamera;

        /// <summary>Solo para el arranque. Ningún sistema accede a otros sistemas a través de esto.</summary>
        public static GameBootstrap Instance { get; private set; }

        /// <summary>Servicios registrados (para debug, tests o herramientas).</summary>
        public ServiceLocator Services { get; private set; }

        private UpdateManager updates;
        private PlayerLoopInjector playerLoop;
        private EventBus events;
        private NavMeshDataInstance navMeshInstance;
        private bool composed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (config == null)
            {
                Debug.LogError("[Bootstrap] Falta asignar el GameConfig. Usá el menú TP/2 - Generar escena y configs.");
                return;
            }

            Compose();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (composed) Teardown();
            Instance = null;
        }

        private void Compose()
        {
            Layers.Initialize();
            if (Layers.Projectile != 0 && Layers.Player != 0)
                Physics.IgnoreLayerCollision(Layers.Projectile, Layers.Player, true);
            ApplyPerformanceSettings(config.performance);

            if (config.map.navMesh != null) navMeshInstance = NavMesh.AddNavMeshData(config.map.navMesh);
            else Debug.LogError("[Bootstrap] El MapData no tiene NavMesh horneado. Regenerá la escena (menú TP).");

            if (mainCamera == null) mainCamera = Camera.main;
            Transform pools = new GameObject("Pools").transform;
            pools.SetParent(transform, false);
            bool pooling = config.performance.usePooling;

            // ---- Núcleo ----
            Services = new ServiceLocator();
            events = new EventBus();
            updates = new UpdateManager();
            Services.Register(events);
            Services.Register(updates);

            // ---- Servicios base ----
            var input = new InputService();
            var progression = new ProgressionService(config.stats);
            var audio = new AudioService(gameObject, config.presentation);
            var vfx = new VfxSystem(config.vfx, pooling, pools);

            // ---- Gameplay ----
            var player = new PlayerSystem(config.player, input, progression, events, mainCamera);
            var enemies = new EnemySystem(player, vfx, events, config.performance, pools);
            var projectiles = new ProjectileSystem(config.weapon, enemies, vfx, config.performance, pools);
            var weapon = new WeaponSystem(config.weapon, config.player, input, player, projectiles, enemies,
                                          audio, progression, events);
            var grenades = new GrenadeSystem(config.grenade, input, player, enemies, vfx, audio, events,
                                             config.performance, pools);
            var waves = new WaveSystem(config.waves, config.map, enemies, player, audio, events, enemies.Prewarm);
            var pickups = new PickupSystem(config.pickups, config.map, player, weapon, grenades, vfx, events, pools);
            var cameraSystem = new CameraSystem(mainCamera, config.presentation, player, config.map.playerSpawn);
            var flow = new GameFlowService(updates, events, input, player, waves, weapon, grenades, progression,
                                           audio, config.stats, config.map);

            // ---- UI ----
            var ui = new UIService(events, flow, flow, player, progression, audio, config.presentation.menuBackground,
                                   PlayerSystem.GetModelNames(config.player.prefab), config.performance.showFpsCounter);

            // ---- Registro en el Service Locator (por interfaz) ----
            Services.Register<IInputService>(input);
            Services.Register<IProgressionService>(progression);
            Services.Register<IAudioService>(audio);
            Services.Register<IVfxService>(vfx);
            Services.Register<IPlayerService>(player);
            Services.Register<IEnemyService>(enemies);
            Services.Register<IDamageService>(enemies);
            Services.Register<IProjectileService>(projectiles);
            Services.Register<IWeaponService>(weapon);
            Services.Register<IGrenadeService>(grenades);
            Services.Register<IWaveService>(waves);
            Services.Register<IGameFlow>(flow);
            Services.Register<IMatchStats>(flow);
            Services.Register(ui);

            // ---- Game Loop (orden de ejecución y frecuencia) ----
            updates.Register(input, order: -100, pausable: false);
            updates.Register(flow, order: -90, pausable: false);
            updates.Register((IUpdatable)player, order: 0);
            updates.Register((IFixedUpdatable)player, order: 0);
            updates.Register(weapon, order: 10);
            updates.Register(grenades, order: 20);
            updates.Register(projectiles, order: 30);
            updates.Register(enemies, order: 40);
            updates.Register(waves, order: 50);
            updates.Register(pickups, order: 60, interval: 0.1f);   // no frecuente: 10 veces por segundo
            updates.Register(vfx, order: 70);
            updates.Register(ui.Hud, order: 100);
            updates.Register(cameraSystem, order: 0);

            playerLoop = new PlayerLoopInjector(updates);
            playerLoop.Inject();
            composed = true;

            // ---- Arranque ----
            // Los pools de enemigos se crean durante la carga de la escena (no en medio de la partida).
            waves.PrewarmPools();
            flow.ShowMenu();
            if (SessionFlags.StartGameOnLoad)
            {
                SessionFlags.StartGameOnLoad = false;
                flow.StartGame();
            }
        }

        private void Teardown()
        {
            playerLoop.Dispose();
            updates.Clear();
            Services.DisposeAll();
            events.Clear();
            if (navMeshInstance.valid) navMeshInstance.Remove();
            Time.timeScale = 1f;
            composed = false;
        }

        private static void ApplyPerformanceSettings(PerformanceConfig performance)
        {
            Application.targetFrameRate = performance.targetFrameRate;
            if (performance.targetFrameRate > 0) QualitySettings.vSyncCount = 0;
        }
    }
}
