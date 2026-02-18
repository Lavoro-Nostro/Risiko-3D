using System;
using System.Globalization;
using System.IO;
using Risiko3D.Runtime.Configuration;
using UnityEngine;

namespace Risiko3D.Runtime.Bootstrap
{
    public static class SteamSdkHealthChecks
    {
        public static void Run(GameRuntimeConfig config, StartupHealthReport report)
        {
            if (config == null || report == null)
            {
                return;
            }

#if DISABLESTEAMWORKS
            report.Warnings.Add("Steam SDK probe skipped: DISABLESTEAMWORKS is enabled (editor simulation mode).");
            return;
#endif

            if (!config.EnableSteamSdkHealthChecks)
            {
                report.Warnings.Add("Steam SDK health checks disabled in runtime config.");
                return;
            }

            var appIdPath = ToAbsolutePath(config.SteamAppIdPath);
            if (!File.Exists(appIdPath))
            {
                report.Errors.Add($"Missing Steam app id file: {config.SteamAppIdPath}");
                return;
            }

            var appIdText = File.ReadAllText(appIdPath).Trim();
            if (!uint.TryParse(appIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
            {
                report.Errors.Add($"steam_appid.txt is not a valid unsigned integer: '{appIdText}'.");
                return;
            }

            if (appId == 0)
            {
                report.Errors.Add("Steam app id must be greater than 0.");
                return;
            }

            if (config.ExpectedDevSteamAppId != 0 && appId != config.ExpectedDevSteamAppId)
            {
                report.Warnings.Add(
                    $"steam_appid.txt value '{appId}' differs from ExpectedDevSteamAppId '{config.ExpectedDevSteamAppId}'.");
            }

            if (!TryRunSteamInitProbe(appId, out var probeError))
            {
                report.Errors.Add($"Steam API probe failed: {probeError}");
                return;
            }
        }

        private static bool TryRunSteamInitProbe(uint appId, out string error)
        {
            error = string.Empty;

            var steamApiType = FindType("Steamworks.SteamAPI");
            var appIdType = FindType("Steamworks.AppId_t");
            if (steamApiType == null || appIdType == null)
            {
                error = "Steamworks.NET types not found. Ensure package import completed.";
                return false;
            }

            try
            {
                var appIdValue = Activator.CreateInstance(appIdType, appId);
                var restartMethod = steamApiType.GetMethod("RestartAppIfNecessary");
                var initMethod = steamApiType.GetMethod("Init");
                var shutdownMethod = steamApiType.GetMethod("Shutdown");

                if (restartMethod == null || initMethod == null || shutdownMethod == null)
                {
                    error = "SteamAPI methods missing (RestartAppIfNecessary/Init/Shutdown).";
                    return false;
                }

                var restartRequired = (bool)restartMethod.Invoke(null, new[] { appIdValue });
                if (restartRequired)
                {
                    error = "RestartAppIfNecessary returned true. Launch via Steam client for runtime testing.";
                    return false;
                }

                var initOk = (bool)initMethod.Invoke(null, Array.Empty<object>());
                if (!initOk)
                {
                    error = "SteamAPI.Init returned false. Verify Steam client is running and app id is valid.";
                    return false;
                }

                shutdownMethod.Invoke(null, Array.Empty<object>());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            var direct = Type.GetType(fullName);
            if (direct != null)
            {
                return direct;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            var normalized = (projectRelativePath ?? string.Empty).Replace("\\", "/");
            var root = Directory.GetParent(Application.dataPath);
            if (root == null)
            {
                return normalized;
            }

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(root.FullName, normalized);
            }

            return Path.Combine(root.FullName, normalized);
        }
    }
}
