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

            var guidance = root.AddComponent<BoardGuidanceUiToolkit>();
            guidance.Initialize(config);

            var physicalCards = root.AddComponent<BoardPhysicalCardsToolkit>();
            physicalCards.Initialize(config);

            var seatCamera = root.AddComponent<BoardSeatCameraSpawner>();
            seatCamera.Initialize(config);

            var turntableRotator = root.AddComponent<BoardTurntableVisualRotator>();
            turntableRotator.Initialize(config);
            Debug.Log("[Risiko3D][MatchLoop] Runtime installer created.");
        }
    }
}
