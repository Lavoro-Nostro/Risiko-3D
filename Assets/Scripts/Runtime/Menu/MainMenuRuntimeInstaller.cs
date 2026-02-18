using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Menu
{
    public static class MainMenuRuntimeInstaller
    {
        private const string RootName = "Risiko3D_MainMenuRuntime";

        public static void EnsureCreated(GameRuntimeConfig config)
        {
            if (config == null)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<MainMenuUiToolkit>();
            if (existing != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            var lan = root.AddComponent<LanDiscoveryService>();
            var menu = root.AddComponent<MainMenuUiToolkit>();
            menu.Initialize(config, lan);
            Debug.Log("[Risiko3D][Menu] Runtime installer created.");
        }
    }
}
