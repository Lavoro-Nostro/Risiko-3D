#if UNITY_EDITOR
using System.IO;
using Risiko3D.Runtime.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Risiko3D.EditorTools
{
    public static class MainMenuSceneTools
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string RuntimeConfigPath = "Assets/Resources/Runtime/GameRuntimeConfig.asset";

        [MenuItem("Risiko3D/Menu/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            var dir = Path.GetDirectoryName(MainMenuScenePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            EnsureRuntimeConfigDefaults();
            Debug.Log($"[Risiko3D][Menu] Main menu scene created: {MainMenuScenePath}");
        }

        private static void EnsureRuntimeConfigDefaults()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(RuntimeConfigPath);
            if (cfg == null)
            {
                return;
            }

            var changed = false;
            if (string.IsNullOrWhiteSpace(cfg.MainMenuSceneName))
            {
                cfg.MainMenuSceneName = "MainMenu";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(cfg.GameplaySceneName))
            {
                cfg.GameplaySceneName = "SampleScene";
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
