using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Board;
using Risiko3D.Runtime.Match;
using Risiko3D.Runtime.Menu;
using Risiko3D.Runtime.Steam;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Risiko3D.Runtime.Bootstrap
{
    public static class StartupBootstrap
    {
        private const string RuntimeConfigResourcePath = "Runtime/GameRuntimeConfig";
        private static GameRuntimeConfig s_config;
        private static bool s_ready;
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (s_hooked)
            {
                EnsureForScene(SceneManager.GetActiveScene());
                return;
            }

            s_hooked = true;
            s_config = Resources.Load<GameRuntimeConfig>(RuntimeConfigResourcePath);
            if (s_config == null)
            {
                s_config = ScriptableObject.CreateInstance<GameRuntimeConfig>();
                Debug.LogWarning(
                    "[Risiko3D][Startup] Resources/Runtime/GameRuntimeConfig.asset not found. " +
                    "Using in-memory defaults. Create it via Risiko3D/M0/Create Default Runtime Config.");
            }

            var report = StartupHealthChecks.Run(s_config);

            if (report.Warnings.Count > 0)
            {
                foreach (var warning in report.Warnings)
                {
                    Debug.LogWarning($"[Risiko3D][Startup] {warning}");
                }
            }

            if (!report.IsOk)
            {
                foreach (var error in report.Errors)
                {
                    Debug.LogError($"[Risiko3D][Startup] {error}");
                }

                return;
            }

            Debug.Log(
                $"[Risiko3D][Startup] Ready. map={s_config.MapId}, rules={s_config.RulesProfileId}, " +
                $"contract={s_config.RuntimeContractVersion}, svgOnly={s_config.SvgOnlyCardPipeline}");

            s_ready = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureForScene(Scene scene)
        {
            if (!s_ready || s_config == null)
            {
                return;
            }

            SteamRuntimeInstaller.EnsureCreated(s_config);

            var sceneName = scene.name;
            if (!string.IsNullOrWhiteSpace(s_config.MainMenuSceneName)
                && sceneName == s_config.MainMenuSceneName)
            {
                MainMenuRuntimeInstaller.EnsureCreated(s_config);
                return;
            }

            BoardRuntimeInstaller.EnsureCreated(s_config);
            MatchRuntimeInstaller.EnsureCreated(s_config);
        }
    }
}
