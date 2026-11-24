using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TP.EditorTools
{
    /// <summary>
    /// Optimizaciones de rendering aplicables / reversibles para medir el "antes y después" con el Profiler.
    /// Ver el README para el detalle de cada técnica y cómo capturar la evidencia.
    /// </summary>
    public static class TPOptimizer
    {
        private const StaticEditorFlags SceneryFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic;

        [MenuItem("TP/3 - Aplicar optimizaciones de escena", priority = 20)]
        public static void ApplySceneOptimizations()
        {
            GameObject level = GameObject.Find("Level");
            if (level == null)
            {
                Debug.LogError("[TP] Abrí la escena TP_Game (o generala con TP/2) antes de optimizar.");
                return;
            }

            int statics = 0, shadowsOff = 0, lightsChanged = 0, materials = 0;

            // 1) Static batching + occlusion: pisos, paredes y props no se mueven nunca.
            foreach (string group in new[] { "Floors", "Walls", "Props" })
            {
                Transform t = level.transform.Find(group);
                if (t == null) continue;
                foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, SceneryFlags);
                    statics++;
                }
            }

            // 2) Los pisos y los props chicos no necesitan proyectar sombras (menos shadow casters).
            shadowsOff += SetShadowCasting(level.transform.Find("Floors"), ShadowCastingMode.Off);
            shadowsOff += SetShadowCasting(level.transform.Find("Props"), ShadowCastingMode.Off);

            // 3) Luces puntuales: sin sombras (cada luz puntual con sombra renderiza un cubemap = 6 pasadas).
            //    Solo la luz direccional mantiene sombras.
            foreach (Light light in Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional) continue;
                if (light.shadows == LightShadows.None) continue;
                Undo.RecordObject(light, "TP optimize light");
                light.shadows = LightShadows.None;
                lightsChanged++;
            }

            // 4) GPU instancing en todos los materiales de la escena y de los prefabs dinámicos.
            materials += EnableInstancing(CollectSceneMaterials());
            materials += EnableInstancing(CollectPrefabMaterials("Assets/TP/Generated/Prefabs"));

            // 5) Skinning más barato en los zombies (2 huesos por vértice, sin motion vectors).
            OptimizeSkinnedPrefabs("Assets/TP/Generated/Prefabs");

            // 6) Sombras: distancia acotada a lo que ve la cámara top-down.
            QualitySettings.shadowDistance = 35f;

            EditorSceneManager.MarkSceneDirty(level.scene);
            EditorSceneManager.SaveScene(level.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TP] Optimizaciones aplicadas: {statics} objetos estáticos, {shadowsOff} renderers sin sombra, " +
                      $"{lightsChanged} luces sin sombra, {materials} materiales con GPU instancing.");
        }

        [MenuItem("TP/3b - Quitar optimizaciones de escena (para comparar)", priority = 21)]
        public static void RevertSceneOptimizations()
        {
            GameObject level = GameObject.Find("Level");
            if (level == null) return;

            foreach (Transform child in level.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);

            SetShadowCasting(level.transform.Find("Floors"), ShadowCastingMode.On);
            SetShadowCasting(level.transform.Find("Props"), ShadowCastingMode.On);

            Transform lights = level.transform.Find("Lights");
            if (lights != null)
                foreach (Light light in lights.GetComponentsInChildren<Light>(true))
                    light.shadows = LightShadows.Soft;

            EditorSceneManager.MarkSceneDirty(level.scene);
            EditorSceneManager.SaveScene(level.scene);
            Debug.Log("[TP] Escena vuelta al estado sin optimizar (para medir el 'antes').");
        }

        [MenuItem("TP/4 - Optimizar texturas y audio importados", priority = 22)]
        public static void OptimizeImportedAssets()
        {
            int textures = 0, clips = 0;

            // Texturas de los modelos (hay normal maps de 8K y difusas de 20-30 MB): tope 2048 + compresión.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Assets", "Assets/FST" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                if (importer.maxTextureSize <= 2048 && importer.textureCompression != TextureImporterCompression.Uncompressed) continue;

                importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, 2048);
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                    importer.textureCompression = TextureImporterCompression.Compressed;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                textures++;
            }

            // Audio: la música (wav de ~30 MB) se reproduce en streaming; los efectos se descomprimen al cargar.
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;

                bool isMusic = path.Contains("Menu.wav") || path.Contains("Gameplay.wav");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = isMusic ? 0.5f : 0.7f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !isMusic;
                importer.SaveAndReimport();
                clips++;
            }

            Debug.Log($"[TP] Assets optimizados: {textures} texturas, {clips} clips de audio.");
        }

        // ------------------------------------------------------------------------------

        private static int SetShadowCasting(Transform root, ShadowCastingMode mode)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(r, "TP shadows");
                r.shadowCastingMode = mode;
                count++;
            }
            return count;
        }

        private static HashSet<Material> CollectSceneMaterials()
        {
            var set = new HashSet<Material>();
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
                foreach (Material m in r.sharedMaterials)
                    if (m != null) set.Add(m);
            return set;
        }

        private static HashSet<Material> CollectPrefabMaterials(string folder)
        {
            var set = new HashSet<Material>();
            if (!AssetDatabase.IsValidFolder(folder)) return set;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null) set.Add(m);
            }
            return set;
        }

        private static int EnableInstancing(HashSet<Material> materials)
        {
            int count = 0;
            foreach (Material m in materials)
            {
                // Los materiales internos de Unity (ej. Default-Particle) no se pueden modificar.
                if (m.enableInstancing || !AssetDatabase.GetAssetPath(m).StartsWith("Assets/")) continue;
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                count++;
            }
            return count;
        }

        private static void OptimizeSkinnedPrefabs(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.quality = SkinQuality.Bone2;
                    smr.skinnedMotionVectors = false;
                    smr.updateWhenOffscreen = false;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
