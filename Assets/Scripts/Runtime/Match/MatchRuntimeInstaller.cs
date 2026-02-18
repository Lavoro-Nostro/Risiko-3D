using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Match
{
    public static class MatchRuntimeInstaller
    {
        private const string RootName = "Risiko3D_MatchRuntime";

        public static void EnsureCreated(GameRuntimeConfig config)
        {
            if (config == null)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<HostAuthoritativeMatchLoop>();
            if (existing != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            var loop = root.AddComponent<HostAuthoritativeMatchLoop>();
            loop.Initialize(config);

            var hud = root.AddComponent<MatchHudUiToolkit>();
            hud.Initialize(config);
            Debug.Log("[Risiko3D][MatchLoop] Runtime installer created.");
        }
    }
}
