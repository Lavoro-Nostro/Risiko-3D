using UnityEngine;

namespace Risiko3D.Runtime.Steam
{
    public sealed class SteamRuntime : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }

        public bool TryInitialize(uint appId, out string error)
        {
            error = string.Empty;
            if (IsInitialized)
            {
                return true;
            }

#if DISABLESTEAMWORKS
            IsInitialized = true;
            return true;
#else
            if (Steamworks.SteamAPI.RestartAppIfNecessary(new Steamworks.AppId_t(appId)))
            {
                error = "Steam requires app restart via Steam client.";
                return false;
            }

            if (!Steamworks.SteamAPI.Init())
            {
                error = "SteamAPI.Init failed.";
                return false;
            }

            IsInitialized = true;
            return true;
#endif
        }

        private void Update()
        {
#if !DISABLESTEAMWORKS
            if (IsInitialized)
            {
                Steamworks.SteamAPI.RunCallbacks();
            }
#endif
        }

        private void OnDestroy()
        {
#if !DISABLESTEAMWORKS
            if (IsInitialized)
            {
                Steamworks.SteamAPI.Shutdown();
                IsInitialized = false;
            }
#endif
        }
    }
}
