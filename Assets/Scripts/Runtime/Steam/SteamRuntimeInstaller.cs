using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Steam
{
    public static class SteamRuntimeInstaller
    {
        private const string RuntimeRootName = "Risiko3D_SteamRuntime";

        public static void EnsureCreated(GameRuntimeConfig config)
        {
            if (config == null || !config.EnableSteamSdkHealthChecks)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<SteamRuntime>();
            if (existing != null)
            {
                return;
            }

            var root = new GameObject(RuntimeRootName);
            Object.DontDestroyOnLoad(root);

            var runtime = root.AddComponent<SteamRuntime>();
            var appId = config.ExpectedDevSteamAppId == 0 ? 480u : config.ExpectedDevSteamAppId;
            if (!runtime.TryInitialize(appId, out var initError))
            {
                Debug.LogError($"[Risiko3D][Steam] Runtime init failed: {initError}");
                Object.Destroy(root);
                return;
            }

            var lobby = root.AddComponent<SteamLobbyService>();
            lobby.Initialize(config, runtime);

            lobby.LobbyCreated += result =>
                Debug.Log(result.Success
                    ? $"[Risiko3D][Steam] Lobby created: {result.LobbyId}"
                    : $"[Risiko3D][Steam] Lobby create failed: {result.Message}");
            lobby.LobbyJoined += result =>
                Debug.Log(result.Success
                    ? $"[Risiko3D][Steam] Lobby joined: {result.LobbyId}"
                    : $"[Risiko3D][Steam] Lobby join failed: {result.Message}");
            lobby.MatchStarted += result =>
                Debug.Log(result.Success
                    ? $"[Risiko3D][Steam] Match started in lobby: {result.LobbyId}"
                    : $"[Risiko3D][Steam] Match start failed: {result.Message}");

            Debug.Log("[Risiko3D][Steam] Runtime + lobby service installed.");
        }
    }
}

