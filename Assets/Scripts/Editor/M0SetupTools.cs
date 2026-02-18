#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Risiko3D.Runtime.Bootstrap;
using Risiko3D.Runtime.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Risiko3D.EditorTools
{
    public static class M0SetupTools
    {
        private const string RuntimeConfigFolder = "Assets/Resources/Runtime";
        private const string RuntimeConfigPath = RuntimeConfigFolder + "/GameRuntimeConfig.asset";
        private const string SteamAppIdPath = "steam_appid.txt";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ResourcesMapFolder = "Assets/Resources/Map";
        private const string BoardMapPngPath = ResourcesMapFolder + "/world_classic_map.png";
        private const string BoardMapPngSourceA = "Assets/Archive/Optional/MapReference/world_map_reference.png";
        private const string BoardMapPngSourceB = "Assets/Art/Map/world_map_reference.png";

        [MenuItem("Risiko3D/M0/Create Default Runtime Config")]
        public static void CreateDefaultRuntimeConfig()
        {
            var cfg = EnsureRuntimeConfigAsset();
            Selection.activeObject = cfg;
            Debug.Log($"[Risiko3D][M0] Runtime config ready: {RuntimeConfigPath}");
        }

        [MenuItem("Risiko3D/Setup/Run Full Editor Setup")]
        public static void RunFullEditorSetup()
        {
            EnsureRuntimeConfigAsset();
            EnsureSteamAppIdFile();
            EnsureInputSystemOnly();
            EnsureScenesInBuildSettings();
            EnsureBoardVisualTexture();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Risiko3D][Setup] Full editor setup completed.");
        }

        [MenuItem("Risiko3D/M0/Run Startup Health Checks")]
        public static void RunStartupHealthChecks()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            var report = StartupHealthChecks.Run(config);

            if (report.Warnings.Count > 0)
            {
                foreach (var warning in report.Warnings)
                {
                    Debug.LogWarning($"[Risiko3D][M0] {warning}");
                }
            }

            if (!report.IsOk)
            {
                foreach (var error in report.Errors)
                {
                    Debug.LogError($"[Risiko3D][M0] {error}");
                }

                Debug.LogError("[Risiko3D][M0] Startup health checks failed.");
                return;
            }

            Debug.Log("[Risiko3D][M0] Startup health checks passed.");
        }

        public static void RunFullEditorSetupSilently()
        {
            EnsureRuntimeConfigAsset();
            EnsureSteamAppIdFile();
            EnsureInputSystemOnly();
            EnsureScenesInBuildSettings();
            EnsureBoardVisualTexture();
        }

        private static GameRuntimeConfig EnsureRuntimeConfigAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(RuntimeConfigFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Runtime");
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<GameRuntimeConfig>();
            AssetDatabase.CreateAsset(config, RuntimeConfigPath);
            return config;
        }

        private static void EnsureSteamAppIdFile()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), SteamAppIdPath);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "480");
                Debug.Log("[Risiko3D][Setup] Created steam_appid.txt with app id 480.");
            }
        }

        private static void EnsureInputSystemOnly()
        {
            var prop = typeof(PlayerSettings).GetProperty("activeInputHandling");
            if (prop == null || !prop.CanWrite)
            {
                return;
            }

            var current = prop.GetValue(null);
            var enumType = prop.PropertyType;
            var desired = System.Enum.GetNames(enumType).FirstOrDefault(n => n.Contains("InputSystemPackage"));
            if (string.IsNullOrWhiteSpace(desired))
            {
                return;
            }

            var desiredValue = System.Enum.Parse(enumType, desired);
            if (!Equals(current, desiredValue))
            {
                prop.SetValue(null, desiredValue);
                Debug.Log("[Risiko3D][Setup] Switched Active Input Handling to Input System Package.");
            }
        }

        private static void EnsureScenesInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (File.Exists(MainMenuScenePath) && scenes.All(s => s.path != MainMenuScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
                Debug.Log("[Risiko3D][Setup] Added MainMenu to Build Settings.");
            }

            if (File.Exists(SampleScenePath) && scenes.All(s => s.path != SampleScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[Risiko3D][Setup] Added SampleScene to Build Settings.");
                return;
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureBoardVisualTexture()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(ResourcesMapFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Map");
            }

            if (!File.Exists(BoardMapPngPath))
            {
                if (File.Exists(BoardMapPngSourceA))
                {
                    File.Copy(BoardMapPngSourceA, BoardMapPngPath, true);
                    Debug.Log("[Risiko3D][Setup] Copied board texture from archive source.");
                }
                else if (File.Exists(BoardMapPngSourceB))
                {
                    File.Copy(BoardMapPngSourceB, BoardMapPngPath, true);
                    Debug.Log("[Risiko3D][Setup] Copied board texture from art source.");
                }
            }

            if (!File.Exists(BoardMapPngPath))
            {
                return;
            }

            AssetDatabase.ImportAsset(BoardMapPngPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(BoardMapPngPath) is TextureImporter ti)
            {
                var needsReimport = false;
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    needsReimport = true;
                }

                if (needsReimport)
                {
                    ti.SaveAndReimport();
                }
            }
        }
    }
}
#endif
