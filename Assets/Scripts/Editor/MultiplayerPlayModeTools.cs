#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

namespace Risiko3D.EditorTools
{
    public static class MultiplayerPlayModeTools
    {
        private const string DisableSteamworksSymbol = "DISABLESTEAMWORKS";
        private const string EnableMenu = "Risiko3D/Multiplayer Play Mode/Enable Editor Lobby Simulation";
        private const string DisableMenu = "Risiko3D/Multiplayer Play Mode/Disable Editor Lobby Simulation";

        [MenuItem(EnableMenu)]
        public static void EnableEditorLobbySimulation()
        {
            SetDefineSymbol(enabled: true);
        }

        [MenuItem(DisableMenu)]
        public static void DisableEditorLobbySimulation()
        {
            SetDefineSymbol(enabled: false);
        }

        [MenuItem(EnableMenu, true)]
        [MenuItem(DisableMenu, true)]
        private static bool ValidateMenus()
        {
            return EditorUserBuildSettings.selectedBuildTargetGroup != BuildTargetGroup.Unknown;
        }

        private static void SetDefineSymbol(bool enabled)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown)
            {
                UnityEngine.Debug.LogError("[Risiko3D][MPPM] Unknown build target group.");
                return;
            }

#pragma warning disable CS0618
            var raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#pragma warning restore CS0618
            var symbols = raw
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var has = symbols.Contains(DisableSteamworksSymbol, StringComparer.Ordinal);
            if (enabled && !has)
            {
                symbols.Add(DisableSteamworksSymbol);
            }
            else if (!enabled && has)
            {
                symbols.RemoveAll(symbol => string.Equals(symbol, DisableSteamworksSymbol, StringComparison.Ordinal));
            }
            else
            {
                UnityEngine.Debug.Log($"[Risiko3D][MPPM] No change. {DisableSteamworksSymbol} enabled={enabled}.");
                return;
            }

            var next = string.Join(";", symbols);
#pragma warning disable CS0618
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, next);
#pragma warning restore CS0618
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[Risiko3D][MPPM] {DisableSteamworksSymbol} enabled={enabled} for {group}. Recompile triggered.");
        }
    }
}
#endif
