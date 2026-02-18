using System;
using System.Collections.Generic;
using System.IO;
using Risiko3D.Runtime.Configuration;
using Risiko3D.Runtime.Contracts;
using UnityEngine;

namespace Risiko3D.Runtime.Bootstrap
{
    public static class StartupHealthChecks
    {
        public static StartupHealthReport Run(GameRuntimeConfig config)
        {
            var report = new StartupHealthReport();
            if (config == null)
            {
                report.Errors.Add("GameRuntimeConfig is missing.");
                return report;
            }

            CheckRequiredPath(report, config.MapJsonPath);
            CheckRequiredPath(report, config.TerritorySymbolManifestPath);
            CheckRequiredPath(report, config.TerritoryCardsPath);
            CheckRequiredPath(report, config.ObjectiveCardsPath);
            CheckRequiredPath(report, config.RuleCardsPath);
            CheckRequiredPath(report, config.MapLocalizationItPath);
            CheckRequiredPath(report, config.MapLocalizationEnPath);
            CheckRequiredPath(report, config.TerritoryPositionsPath);

            if (config.MinPlayers < 2)
            {
                report.Errors.Add($"MinPlayers must be >= 2. Current: {config.MinPlayers}.");
            }

            if (config.MaxPlayers > 6)
            {
                report.Errors.Add($"MaxPlayers must be <= 6 for rules freeze. Current: {config.MaxPlayers}.");
            }

            if (config.MinPlayers > config.MaxPlayers)
            {
                report.Errors.Add("MinPlayers cannot exceed MaxPlayers.");
            }

            if (!config.SvgOnlyCardPipeline)
            {
                report.Warnings.Add("SvgOnlyCardPipeline is disabled. Current default baseline expects SVG-only.");
            }

            if (!ContractHandshake.IsClientCompatible(
                    config.RuntimeContractVersion,
                    config.ContractMinVersion,
                    config.ContractMaxVersion,
                    out var handshakeError))
            {
                report.Errors.Add($"Contract handshake failed: {handshakeError}");
            }

            if (!Version.TryParse(config.ProtocolVersion, out _))
            {
                report.Errors.Add($"ProtocolVersion is not valid semantic version: {config.ProtocolVersion}");
            }

            if (string.IsNullOrWhiteSpace(config.MapId))
            {
                report.Errors.Add("MapId cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(config.RulesProfileId))
            {
                report.Errors.Add("RulesProfileId cannot be empty.");
            }

            SteamSdkHealthChecks.Run(config, report);

            return report;
        }

        public static bool ValidateLobbyPlayerRange(
            int requestedPlayers,
            GameRuntimeConfig config,
            out string error)
        {
            error = string.Empty;
            if (config == null)
            {
                error = "GameRuntimeConfig is missing.";
                return false;
            }

            const int hardMinPlayers = 2;
            if (requestedPlayers < hardMinPlayers)
            {
                error = $"Requested players {requestedPlayers} is below minimum {hardMinPlayers}.";
                return false;
            }

            if (requestedPlayers > config.MaxPlayers)
            {
                error = $"Requested players {requestedPlayers} exceeds maximum {config.MaxPlayers}.";
                return false;
            }

            return true;
        }

        private static void CheckRequiredPath(StartupHealthReport report, string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
            {
                report.Errors.Add("A required content path is empty.");
                return;
            }

            var absolutePath = ToAbsolutePath(projectRelativePath);
            if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
            {
                report.Errors.Add($"Missing path: {projectRelativePath}");
            }
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            var normalized = projectRelativePath.Replace("\\", "/");
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

    public sealed class StartupHealthReport
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool IsOk => Errors.Count == 0;
    }
}
