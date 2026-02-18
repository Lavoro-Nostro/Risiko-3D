using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Board
{
    public static class BoardRuntimeInstaller
    {
        private const string RootName = "Risiko3D_BoardRuntime";

        public static void EnsureCreated(GameRuntimeConfig config)
        {
            if (config == null)
            {
                return;
            }

            var existing = Object.FindFirstObjectByType<BoardBootstrap>();
            if (existing != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            var board = root.AddComponent<BoardBootstrap>();
            board.Initialize(config);
            Debug.Log("[Risiko3D][Board] Runtime board installer created.");
        }
    }
}

