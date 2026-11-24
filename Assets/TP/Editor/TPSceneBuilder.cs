using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TP.Bootstrap;
using TP.Config;
using TP.Core;

namespace TP.EditorTools
{
    /// <summary>
    /// Herramientas de editor (no cuentan como MonoBehaviours). Arman todo el contenido del juego a partir
    /// de los assets que ya tenía el proyecto: prefabs "limpios" (sin scripts viejos ni Photon),
    /// efectos de partículas, configs (ScriptableObjects), el mapa desde la textura, el NavMesh y la escena.
    ///
    /// La escena se genera SIN optimizar a propósito (para medir el "antes" con el Profiler).
    /// Después: menú TP/3 - Aplicar optimizaciones de escena.
    /// </summary>
    public static class TPSceneBuilder
    {
        private const string Root = "Assets/TP/Generated";
        private const string PrefabsDir = Root + "/Prefabs";
        private const string VfxDir = Root + "/Prefabs/VFX";
        private const string ConfigsDir = Root + "/Configs";
        private const string EnemiesDir = Root + "/Configs/Enemies";
        private const string MaterialsDir = Root + "/Materials";
        private const string ScenePath = Root + "/TP_Game.unity";
        private const string NavMeshPath = Root + "/TP_NavMesh.asset";

        // GUIDs de los assets del juego original (si no se encuentran, se buscan por nombre).
        private const string PlayerGuid = "6f61dc103a945294d9bec0f9f4f2df81";
        private const string ZombieGuid = "5b64ddf8b7eaada4abfd4c5a920b556d";
        private const string ZombieFastGuid = "31b0eb0069f610340981b9b506ad2859";
        private const string ZombieTankGuid = "8f13172a901f6e449b5df097bdfc97b1";
        private const string WallGuid = "096ab0b0bcc99d841b03f62024dc00cb";
        private const string FloorGuid = "ce1389b21d426cc4fa462cd9c3addfb6";
        private const string MapTextureGuid = "de524c9560d49b4469be229b8457428b";

        private const float TileSize = 2f;
        private const float WallHeight = 2.5f;
        private const int TotalWaves = 10;

        // =====================================================================================
        // 1) CAPAS
        // =====================================================================================

        [MenuItem("TP/1 - Configurar proyecto (capas)", priority = 1)]
        public static void SetupLayers()
        {
            EnsureLayer(Layers.EnemyName);
            EnsureLayer(Layers.EnvironmentName);
            EnsureLayer(Layers.ProjectileName);
            EnsureLayer(Layers.PlayerName);
            Debug.Log("[TP] Capas listas: Enemy, Environment, Projectile, Player.");
        }

        private static void EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            Debug.LogError($"[TP] No hay lugar para la capa {layerName}.");
        }

        // =====================================================================================
        // 2) GENERAR TODO
        // =====================================================================================

        [MenuItem("TP/2 - Generar escena y configs", priority = 2)]
        public static void GenerateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SetupLayers();
            Layers.Initialize();
            EnsureFolders();

            try
            {
                EditorUtility.DisplayProgressBar("TP", "Creando prefabs limpios...", 0.1f);
                GameObject player = CleanPrefabCopy(PlayerGuid, "player", "Player", null);
                GameObject zombie = CleanPrefabCopy(ZombieGuid, "Zombie1", "Zombie_Normal", null);
                GameObject zombieFast = CleanPrefabCopy(ZombieFastGuid, "ZombieFast", "Zombie_Fast", null);
                GameObject zombieTank = CleanPrefabCopy(ZombieTankGuid, "ZombieTank", "Zombie_Tank", null);
                GameObject zombieSplitter = CleanPrefabCopy(ZombieGuid, "Zombie1", "Zombie_Splitter", 1.25f);
                GameObject zombieElite = CleanPrefabCopy(ZombieTankGuid, "ZombieTank", "Zombie_Elite", 1.7f);

                EditorUtility.DisplayProgressBar("TP", "Creando proyectiles, pickups y efectos...", 0.3f);
                GameObject bullet = CreateBulletPrefab();
                GameObject grenade = CreateGrenadePrefab();
                GameObject healthPickup = CreatePickupPrefab("Pickup_Health", "Food_01", new Color(0.3f, 1f, 0.4f));
                GameObject ammoPickup = CreatePickupPrefab("Pickup_Ammo", "Box_01", new Color(0.3f, 0.8f, 1f));
                GameObject grenadePickup = CreatePickupPrefab("Pickup_Grenade", "Bucket_01", new Color(1f, 0.55f, 0.1f));
                VfxConfig vfx = CreateVfx();

                EditorUtility.DisplayProgressBar("TP", "Creando configs...", 0.45f);
                var enemyNormal = Enemy("Enemy_Normal", "Zombie", zombie, 100f, 2.3f, 10f, 1.6f, 1f, 5, null, 0, false);
                var enemyFast = Enemy("Enemy_Fast", "Zombie rápido", zombieFast, 50f, 4.2f, 6f, 1.5f, 0.7f, 5, null, 0, false);
                var enemyTank = Enemy("Enemy_Tank", "Zombie tanque", zombieTank, 320f, 1.7f, 22f, 1.8f, 1.4f, 15, null, 0, false);
                var enemySplitter = Enemy("Enemy_Splitter", "Zombie divisor", zombieSplitter, 130f, 2.3f, 10f, 1.7f, 1f, 8, enemyFast, 2, false);
                var enemyElite = Enemy("Enemy_Elite", "Élite", zombieElite, 1400f, 2.1f, 30f, 2.3f, 1.2f, 60, null, 0, false);

                EditorUtility.DisplayProgressBar("TP", "Construyendo el mapa desde la textura...", 0.6f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                MapData map = BuildLevel(out Vector3 mapCenter);

                EditorUtility.DisplayProgressBar("TP", "Guardando configs y escena...", 0.85f);
                GameConfig gameConfig = CreateConfigs(player, bullet, grenade, healthPickup, ammoPickup, grenadePickup, vfx, map,
                                                      enemyNormal, enemyFast, enemyTank, enemySplitter, enemyElite);
                CreateCameraAndBootstrap(gameConfig, mapCenter);

                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                Debug.Log("[TP] Listo. Escena generada en " + ScenePath + " (sin optimizar). Dale Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // =====================================================================================
        // PREFABS
        // =====================================================================================

        /// <summary>
        /// Copia un prefab del juego original quitándole TODO script propio/Photon (y los scripts faltantes),
        /// las UIs world-space y los TextMesh de nombre. Queda solo la representación visual + física.
        /// </summary>
        private static GameObject CleanPrefabCopy(string guid, string fallbackName, string newName, float? scale)
        {
            GameObject source = LoadPrefab(guid, fallbackName);
            if (source == null)
            {
                Debug.LogError($"[TP] No se encontró el prefab original '{fallbackName}'.");
                return null;
            }

            GameObject instance = Object.Instantiate(source);
            instance.name = newName;
            StripScripts(instance);

            foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
                if (canvas != null && canvas.gameObject != instance) Object.DestroyImmediate(canvas.gameObject);
            foreach (TextMesh text in instance.GetComponentsInChildren<TextMesh>(true))
                if (text != null) Object.DestroyImmediate(text.gameObject);

            if (scale.HasValue) instance.transform.localScale *= scale.Value;

            bool isPlayer = newName == "Player";
            if (isPlayer)
            {
                instance.tag = "Player";
                if (instance.GetComponent<Rigidbody>() == null) instance.AddComponent<Rigidbody>();
                if (instance.GetComponent<Collider>() == null)
                {
                    var capsule = instance.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0f, 0.9f, 0f);
                    capsule.height = 1.8f;
                    capsule.radius = 0.4f;
                }
            }
            else
            {
                instance.tag = "Untagged";
                if (instance.GetComponent<NavMeshAgent>() == null) instance.AddComponent<NavMeshAgent>();
                if (instance.GetComponent<Collider>() == null) instance.AddComponent<CapsuleCollider>();
            }

            string path = $"{PrefabsDir}/{newName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>Quita scripts faltantes y cualquier MonoBehaviour que no sea de Unity/TextMeshPro.</summary>
        private static void StripScripts(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            // Varias pasadas por si hay [RequireComponent] entre scripts viejos.
            for (int pass = 0; pass < 4; pass++)
            {
                bool removed = false;
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string ns = mb.GetType().Namespace ?? string.Empty;
                    if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                    Object.DestroyImmediate(mb);
                    removed = true;
                }
                if (!removed) break;
            }
        }

        private static GameObject CreateBulletPrefab()
        {
            Material bulletMat = SaveMaterial("Bullet", CreateLitMaterial(new Color(1f, 0.85f, 0.3f), new Color(3f, 2.2f, 0.6f)));
            Material trailMat = SaveMaterial("BulletTrail", CreateUnlitMaterial(new Color(1f, 0.75f, 0.25f, 0.8f)));

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bullet";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.16f;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = bulletMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.08f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.2f;
            trail.sharedMaterial = trailMat;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            return SavePrefab(go, "Bullet");
        }

        private static GameObject CreateGrenadePrefab()
        {
            Material mat = SaveMaterial("Grenade", CreateLitMaterial(new Color(0.2f, 0.3f, 0.15f), Color.black));
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Grenade";
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.drag = 0.4f;
            body.angularDrag = 0.6f;

            string[] bouncy = AssetDatabase.FindAssets("Bounciness t:PhysicMaterial");
            if (bouncy.Length > 0)
                go.GetComponent<SphereCollider>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<PhysicMaterial>(AssetDatabase.GUIDToAssetPath(bouncy[0]));

            return SavePrefab(go, "Grenade");
        }

        private static GameObject CreatePickupPrefab(string name, string propName, Color glow)
        {
            var root = new GameObject(name);
            GameObject prop = LoadPrefab(null, propName);
            if (prop != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prop);
                model.transform.SetParent(root.transform, false);
                FitToSize(model, 0.8f);
                StripScripts(model);
            }
            else
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform, false);
                cube.transform.localScale = Vector3.one * 0.5f;
            }

            // Pequeña luz de color para que el pickup se distinga (carga de luces a optimizar).
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = glow;
            light.range = 2.5f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;

            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return SavePrefab(root, name);
        }

        // =====================================================================================
        // EFECTOS VISUALES (partículas generadas por código)
        // =====================================================================================

        private static VfxConfig CreateVfx()
        {
            Material particleMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            var config = LoadOrCreate<VfxConfig>($"{ConfigsDir}/VfxConfig.asset", out _);
            config.effects = new List<VfxEntry>
            {
                Vfx(VfxType.MuzzleFlash, Particles("VFX_MuzzleFlash", particleMat, 8, 0.05f, 0.08f, 3f, 6f, 0.15f, 0.35f,
                        new Color(1f, 0.9f, 0.4f), new Color(1f, 0.5f, 0.1f), 0f, ParticleSystemShapeType.Cone, 15f,
                        new Color(1f, 0.7f, 0.3f), 3.5f, 2.5f), 0.12f, 20),
                Vfx(VfxType.ImpactWall, Particles("VFX_ImpactWall", particleMat, 12, 0.15f, 0.35f, 3f, 7f, 0.04f, 0.09f,
                        new Color(1f, 0.85f, 0.4f), new Color(1f, 0.6f, 0.2f), 1.2f, ParticleSystemShapeType.Cone, 35f,
                        Color.clear, 0f, 0f), 0.4f, 20),
                Vfx(VfxType.ImpactBlood, Particles("VFX_ImpactBlood", particleMat, 16, 0.25f, 0.5f, 2f, 5f, 0.08f, 0.18f,
                        new Color(0.55f, 0.02f, 0.02f), new Color(0.3f, 0.6f, 0.1f), 1.5f, ParticleSystemShapeType.Cone, 40f,
                        Color.clear, 0f, 0f), 0.55f, 20),
                Vfx(VfxType.Explosion, Particles("VFX_Explosion", particleMat, 70, 0.5f, 1.1f, 3f, 9f, 0.5f, 1.4f,
                        new Color(1f, 0.75f, 0.25f), new Color(0.35f, 0.3f, 0.28f), -0.2f, ParticleSystemShapeType.Sphere, 0f,
                        new Color(1f, 0.55f, 0.2f), 7f, 12f), 1.2f, 4),
                Vfx(VfxType.EnemyDeath, Particles("VFX_EnemyDeath", particleMat, 30, 0.4f, 0.8f, 1.5f, 4f, 0.15f, 0.4f,
                        new Color(0.45f, 0.05f, 0.05f), new Color(0.2f, 0.35f, 0.1f), 0.8f, ParticleSystemShapeType.Sphere, 0f,
                        Color.clear, 0f, 0f), 0.9f, 12),
                Vfx(VfxType.Pickup, Particles("VFX_Pickup", particleMat, 24, 0.4f, 0.7f, 1f, 3f, 0.08f, 0.18f,
                        new Color(0.4f, 1f, 0.6f), new Color(0.4f, 0.8f, 1f), -0.6f, ParticleSystemShapeType.Circle, 0f,
                        new Color(0.5f, 1f, 0.7f), 2f, 3f), 0.8f, 3)
            };
            EditorUtility.SetDirty(config);
            return config;
        }

        private static VfxEntry Vfx(VfxType type, GameObject prefab, float lifetime, int prewarm)
            => new VfxEntry { type = type, prefab = prefab, lifetime = lifetime, prewarm = prewarm };

        private static GameObject Particles(string name, Material material, int burst, float lifeMin, float lifeMax,
                                            float speedMin, float speedMax, float sizeMin, float sizeMax,
                                            Color colorA, Color colorB, float gravity, ParticleSystemShapeType shapeType,
                                            float coneAngle, Color lightColor, float lightIntensity, float lightRange)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = Mathf.Max(0.1f, lifeMax);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = burst * 2;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = shapeType;
            shape.radius = shapeType == ParticleSystemShapeType.Sphere ? 0.3f : 0.05f;
            shape.angle = coneAngle;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (lightIntensity > 0f)
            {
                var lightGo = new GameObject("Flash");
                lightGo.transform.SetParent(go.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColor;
                light.intensity = lightIntensity;
                light.range = lightRange;
                light.shadows = LightShadows.None;
                light.enabled = false;
            }

            return SavePrefab(go, name, VfxDir);
        }

        // =====================================================================================
        // MAPA (misma convención de colores que el LevelGenerator original)
        // =====================================================================================

        private static MapData BuildLevel(out Vector3 center)
        {
            var map = LoadOrCreate<MapData>($"{ConfigsDir}/MapData.asset", out _);
            map.enemySpawns = new List<Vector3>();
            map.ammoSpawns = new List<Vector3>();
            map.healthSpawns = new List<Vector3>();
            map.grenadeSpawns = new List<Vector3>();
            map.playerSpawn = Vector3.zero;

            Texture2D texture = LoadAsset<Texture2D>(MapTextureGuid, "mapaZombies t:Texture2D");
            GameObject wallPrefab = LoadPrefab(WallGuid, "wallPrefab");
            GameObject floorPrefab = LoadPrefab(FloorGuid, "floorPrefab");
            if (texture == null || wallPrefab == null || floorPrefab == null)
            {
                Debug.LogError("[TP] Faltan la textura del mapa o los prefabs de pared/piso del proyecto original.");
                center = Vector3.zero;
                return map;
            }
            MakeReadable(texture);

            Transform level = new GameObject("Level").transform;
            Transform floors = Child(level, "Floors");
            Transform walls = Child(level, "Walls");
            Transform props = Child(level, "Props");
            Transform lights = Child(level, "Lights");

            List<GameObject> propPrefabs = LoadPropPrefabs();
            var random = new System.Random(1234);
            bool playerFound = false;
            int width = texture.width;
            int height = texture.height;
            var isWall = new bool[width, height];

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Color c = texture.GetPixel(x, y);
                    isWall[x, y] = c.a >= 0.1f && Match(c, Color.black);
                }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color c = texture.GetPixel(x, y);
                    if (c.a < 0.1f) continue;
                    Vector3 pos = new Vector3(x * TileSize, 0f, y * TileSize);

                    if (isWall[x, y])
                    {
                        SpawnTile(wallPrefab, walls, pos, WallHeight, false);
                        continue;
                    }

                    SpawnTile(floorPrefab, floors, pos, 0f, true);

                    if (Match(c, Color.blue))
                    {
                        if (!playerFound) map.playerSpawn = pos;
                        playerFound = true;
                    }
                    else if (Match(c, Color.red)) map.enemySpawns.Add(pos);
                    else if (Match(c, Color.cyan)) map.ammoSpawns.Add(pos);
                    else if (Match(c, Color.magenta)) map.healthSpawns.Add(pos);
                    else if (Match(c, new Color(1f, 0.5f, 0f))) map.grenadeSpawns.Add(pos);
                    else if (Match(c, Color.white))
                    {
                        // Props contra las paredes (decoración con muchos materiales = carga de GPU).
                        Vector3 wallDir = AdjacentWall(isWall, x, y);
                        if (wallDir != Vector3.zero && propPrefabs.Count > 0 && random.NextDouble() < 0.35)
                        {
                            GameObject prefab = propPrefabs[random.Next(propPrefabs.Count)];
                            var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props);
                            FitToSize(prop, 0.9f + (float)random.NextDouble() * 0.5f);
                            prop.transform.position = pos + wallDir * (TileSize * 0.3f);
                            prop.transform.rotation = Quaternion.Euler(0f, random.Next(0, 360), 0f);
                            StripScripts(prop);
                        }
                    }
                    else
                    {
                        // Gris (revivir) / amarillo (extracción) del mapa viejo: quedan como puntos de luz.
                        AddPointLight(lights, pos + Vector3.up * 2.2f, new Color(0.6f, 0.8f, 1f), 9f);
                    }

                    // Luces de ambiente repartidas por el mapa (con sombras: el "antes" sin optimizar).
                    if (x % 5 == 2 && y % 5 == 2)
                        AddPointLight(lights, pos + Vector3.up * 2.4f, new Color(1f, 0.72f, 0.45f), 8f);
                }
            }

            center = new Vector3((width - 1) * TileSize * 0.5f, 0f, (height - 1) * TileSize * 0.5f);
            if (!playerFound) map.playerSpawn = center;

            AddDirectionalLight();
            ConfigureRenderSettings();

            map.navMesh = BakeNavMesh(floors, walls);
            EditorUtility.SetDirty(map);
            return map;
        }

        private static void SpawnTile(GameObject prefab, Transform parent, Vector3 pos, float height, bool isFloor)
        {
            var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            tile.transform.position = pos;
            tile.transform.rotation = Quaternion.identity;

            Renderer renderer = tile.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 size = renderer.bounds.size;
                Vector3 scale = tile.transform.localScale;
                if (size.x > 0.0001f) scale.x *= TileSize / size.x;
                if (size.z > 0.0001f) scale.z *= TileSize / size.z;
                if (!isFloor && size.y > 0.0001f) scale.y *= height / size.y;
                tile.transform.localScale = scale;

                // Apoyamos la pared sobre el piso y el piso queda con su cara superior en y = 0.
                Bounds b = renderer.bounds;
                float lift = isFloor ? -b.max.y : -b.min.y;
                tile.transform.position += new Vector3(0f, lift, 0f);
            }

            if (tile.GetComponentInChildren<Collider>() == null) tile.AddComponent<BoxCollider>();

            int layer = isFloor ? LayerMask.NameToLayer("Ground") : Layers.Environment;
            if (layer >= 0) SetLayer(tile, layer);
        }

        private static Vector3 AdjacentWall(bool[,] isWall, int x, int y)
        {
            int w = isWall.GetLength(0), h = isWall.GetLength(1);
            if (x + 1 < w && isWall[x + 1, y]) return Vector3.right;
            if (x - 1 >= 0 && isWall[x - 1, y]) return Vector3.left;
            if (y + 1 < h && isWall[x, y + 1]) return Vector3.forward;
            if (y - 1 >= 0 && isWall[x, y - 1]) return Vector3.back;
            return Vector3.zero;
        }

        private static void AddPointLight(Transform parent, Vector3 position, Color color, float range)
        {
            var go = new GameObject("Point Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
        }

        private static void AddDirectionalLight()
        {
            var go = new GameObject("Directional Light (Luna)");
            go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.7f, 0.9f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            RenderSettings.sun = light;
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.17f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.04f, 0.04f, 0.06f);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 60f;
            RenderSettings.skybox = null;
        }

        private static NavMeshData BakeNavMesh(Transform floors, Transform walls)
        {
            var sources = new List<NavMeshBuildSource>();
            var temp = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();

            NavMeshBuilder.CollectSources(floors, ~0, NavMeshCollectGeometry.RenderMeshes, 0, markups, temp);
            sources.AddRange(temp);

            temp.Clear();
            NavMeshBuilder.CollectSources(walls, ~0, NavMeshCollectGeometry.RenderMeshes, 1, markups, temp); // 1 = Not Walkable
            sources.AddRange(temp);

            var bounds = new Bounds(floors.position, Vector3.zero);
            foreach (Renderer r in floors.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            foreach (Renderer r in walls.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            bounds.Expand(4f);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 45f;

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            data.name = "TP_NavMesh";

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(data, NavMeshPath);
            return data;
        }

        // =====================================================================================
        // CONFIGS
        // =====================================================================================

        private static EnemyTypeConfig Enemy(string file, string displayName, GameObject prefab, float hp, float speed,
                                             float damage, float range, float cooldown, int xp,
                                             EnemyTypeConfig splitInto, int splitCount, bool overwrite)
        {
            var e = LoadOrCreate<EnemyTypeConfig>($"{EnemiesDir}/{file}.asset", out bool created);
            e.prefab = prefab;
            if (created || overwrite)
            {
                e.displayName = displayName;
                e.maxHealth = hp;
                e.moveSpeed = speed;
                e.damage = damage;
                e.attackRange = range;
                e.attackCooldown = cooldown;
                e.xpReward = xp;
                e.splitInto = splitInto;
                e.splitCount = splitCount;
            }
            EditorUtility.SetDirty(e);
            return e;
        }

        private static GameConfig CreateConfigs(GameObject player, GameObject bullet, GameObject grenade,
                                                GameObject healthPickup, GameObject ammoPickup, GameObject grenadePickup,
                                                VfxConfig vfx, MapData map, EnemyTypeConfig normal, EnemyTypeConfig fast,
                                                EnemyTypeConfig tank, EnemyTypeConfig splitter, EnemyTypeConfig elite)
        {
            var playerConfig = LoadOrCreate<PlayerConfig>($"{ConfigsDir}/PlayerConfig.asset", out _);
            playerConfig.prefab = player;

            var weapon = LoadOrCreate<WeaponConfig>($"{ConfigsDir}/WeaponConfig.asset", out _);
            weapon.bulletPrefab = bullet;

            var grenadeConfig = LoadOrCreate<GrenadeConfig>($"{ConfigsDir}/GrenadeConfig.asset", out _);
            grenadeConfig.prefab = grenade;

            var pickups = LoadOrCreate<PickupConfig>($"{ConfigsDir}/PickupConfig.asset", out _);
            pickups.healthPrefab = healthPickup;
            pickups.ammoPrefab = ammoPickup;
            pickups.grenadePrefab = grenadePickup;

            var stats = LoadOrCreate<StatsConfig>($"{ConfigsDir}/StatsConfig.asset", out _);
            var performance = LoadOrCreate<PerformanceConfig>($"{ConfigsDir}/PerformanceConfig.asset", out _);

            var waves = LoadOrCreate<WaveConfig>($"{ConfigsDir}/WaveConfig.asset", out bool wavesCreated);
            if (wavesCreated || waves.waves.Count == 0) waves.waves = BuildWaves(normal, fast, tank, splitter, elite);

            var presentation = LoadOrCreate<PresentationConfig>($"{ConfigsDir}/PresentationConfig.asset", out _);
            if (presentation.menuBackground == null) presentation.menuBackground = LoadAsset<Texture>(null, "MenuBg t:Texture2D");
            if (presentation.menuMusic == null) presentation.menuMusic = LoadAsset<AudioClip>(null, "Menu t:AudioClip");
            if (presentation.gameplayMusic == null) presentation.gameplayMusic = LoadAsset<AudioClip>(null, "Gameplay t:AudioClip");
            if (presentation.shootClip == null) presentation.shootClip = LoadAsset<AudioClip>(null, "Attack t:AudioClip");
            if (presentation.uiClickClip == null) presentation.uiClickClip = LoadAsset<AudioClip>(null, "Button t:AudioClip");
            if (presentation.waveStartClip == null) presentation.waveStartClip = LoadAsset<AudioClip>(null, "StartGame t:AudioClip");

            var game = LoadOrCreate<GameConfig>($"{ConfigsDir}/GameConfig.asset", out _);
            game.player = playerConfig;
            game.weapon = weapon;
            game.grenade = grenadeConfig;
            game.waves = waves;
            game.stats = stats;
            game.pickups = pickups;
            game.vfx = vfx;
            game.presentation = presentation;
            game.map = map;
            game.performance = performance;

            foreach (Object o in new Object[] { playerConfig, weapon, grenadeConfig, pickups, stats, performance, waves, presentation, game })
                EditorUtility.SetDirty(o);
            return game;
        }

        /// <summary>
        /// 10 waves con dificultad creciente: más cantidad, nuevos tipos (rápidos, tanques, divisores),
        /// más vida y velocidad, spawn más rápido y un élite en las waves 5 y 10.
        /// </summary>
        private static List<WaveDefinition> BuildWaves(EnemyTypeConfig normal, EnemyTypeConfig fast, EnemyTypeConfig tank,
                                                       EnemyTypeConfig splitter, EnemyTypeConfig elite)
        {
            var list = new List<WaveDefinition>();
            for (int n = 1; n <= TotalWaves; n++)
            {
                var wave = new WaveDefinition
                {
                    spawnInterval = Mathf.Max(0.25f, 0.9f - 0.06f * n),
                    healthMultiplier = 1f + 0.08f * (n - 1),
                    speedMultiplier = 1f + 0.03f * (n - 1),
                    isEliteWave = n % 5 == 0
                };
                Add(wave, normal, 4 + 2 * n);
                Add(wave, fast, n >= 2 ? n : 0);
                Add(wave, tank, n >= 3 ? n / 2 : 0);
                Add(wave, splitter, n >= 4 ? n - 2 : 0);
                Add(wave, elite, n % 5 == 0 ? n / 5 : 0);
                list.Add(wave);
            }
            return list;
        }

        private static void Add(WaveDefinition wave, EnemyTypeConfig type, int count)
        {
            if (count > 0 && type != null) wave.entries.Add(new WaveSpawnEntry { type = type, count = count });
        }

        private static void CreateCameraAndBootstrap(GameConfig config, Vector3 mapCenter)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 80f;
            camera.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = mapCenter + new Vector3(0f, 25f, -14f);
            camGo.transform.LookAt(mapCenter);

            var bootGo = new GameObject("Bootstrap");
            var bootstrap = bootGo.AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("mainCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================================
        // UTILIDADES
        // =====================================================================================

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "TP");
            EnsureFolder("Assets/TP", "Generated");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(PrefabsDir, "VFX");
            EnsureFolder(Root, "Configs");
            EnsureFolder(ConfigsDir, "Enemies");
            EnsureFolder(Root, "Materials");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject LoadPrefab(string guid, string fallbackName)
        {
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var byGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (byGuid != null) return byGuid;
            }
            foreach (string g in AssetDatabase.FindAssets(fallbackName + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.StartsWith(Root)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) == fallbackName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static T LoadAsset<T>(string guid, string search) where T : Object
        {
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var byGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
                if (byGuid != null) return byGuid;
            }
            string[] found = AssetDatabase.FindAssets(search);
            return found.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(found[0])) : null;
        }

        private static List<GameObject> LoadPropPrefabs()
        {
            var list = new List<GameObject>();
            foreach (string folder in new[] { "Assets/LowPolyMedievalPropsLite/Prefabs", "Assets/Assets/Barril Prefab" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                    if (prefab != null && prefab.GetComponentInChildren<Renderer>() != null) list.Add(prefab);
                }
            }
            return list;
        }

        private static void MakeReadable(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void FitToSize(GameObject go, float targetSize)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (max < 0.0001f) return;
            go.transform.localScale *= Mathf.Clamp(targetSize / max, 0.05f, 20f);
        }

        private static bool Match(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.1f && Mathf.Abs(a.g - b.g) < 0.1f && Mathf.Abs(a.b - b.b) < 0.1f;

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }

        private static GameObject SavePrefab(GameObject go, string name, string folder = PrefabsDir)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Material CreateLitMaterial(Color color, Color emission)
        {
            var mat = new Material(Shader.Find("Standard")) { color = color, enableInstancing = true };
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            return mat;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            return new Material(shader) { color = color };
        }

        private static Material SaveMaterial(string name, Material mat)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(mat);
                existing.shader = mat.shader;
                Object.DestroyImmediate(mat);
                return existing;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
