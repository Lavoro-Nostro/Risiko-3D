#if UNITY_EDITOR
using Risiko3D.Runtime.Steam;
using UnityEditor;
using UnityEngine;

namespace Risiko3D.EditorTools
{
    public static class SteamLobbyDebugMenu
    {
        [MenuItem("Risiko3D/Steam Debug/Create Lobby (Max Players From Config)")]
        public static void CreateLobby()
        {
            var service = Object.FindFirstObjectByType<SteamLobbyService>();
            if (service == null)
            {
                Debug.LogError("[Risiko3D][SteamDebug] SteamLobbyService not found in scene/runtime.");
                return;
            }

            var cfg = Resources.Load<Risiko3D.Runtime.Configuration.GameRuntimeConfig>("Runtime/GameRuntimeConfig");
            var players = cfg != null ? cfg.MaxPlayers : 6;
            if (!service.CreateLobby(players, out var error))
            {
                Debug.LogError($"[Risiko3D][SteamDebug] CreateLobby failed: {error}");
                return;
            }

            Debug.Log("[Risiko3D][SteamDebug] CreateLobby requested.");
        }

        [MenuItem("Risiko3D/Steam Debug/Start Match In Current Lobby")]
        public static void StartMatch()
        {
            var service = Object.FindFirstObjectByType<SteamLobbyService>();
            if (service == null)
            {
                Debug.LogError("[Risiko3D][SteamDebug] SteamLobbyService not found in scene/runtime.");
                return;
            }

            if (!service.StartMatch(out var error))
            {
                Debug.LogError($"[Risiko3D][SteamDebug] StartMatch failed: {error}");
                return;
            }

            Debug.Log("[Risiko3D][SteamDebug] StartMatch succeeded.");
        }

        [MenuItem("Risiko3D/Steam Debug/Open Invite Overlay")]
        public static void OpenInviteOverlay()
        {
            var service = Object.FindFirstObjectByType<SteamLobbyService>();
            if (service == null)
            {
                Debug.LogError("[Risiko3D][SteamDebug] SteamLobbyService not found in scene/runtime.");
                return;
            }

            if (!service.OpenInviteOverlay(out var error))
            {
                Debug.LogError($"[Risiko3D][SteamDebug] OpenInviteOverlay failed: {error}");
                return;
            }

            Debug.Log("[Risiko3D][SteamDebug] Invite overlay opened.");
        }
    }
}
#endif

