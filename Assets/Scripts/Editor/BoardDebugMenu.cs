#if UNITY_EDITOR
using Risiko3D.Runtime.Board;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;

namespace Risiko3D.EditorTools
{
    public static class BoardDebugMenu
    {
        [MenuItem("Risiko3D/Board Debug/Rebuild Board Runtime")]
        public static void RebuildBoardRuntime()
        {
            var existing = Object.FindFirstObjectByType<BoardBootstrap>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var cfg = Resources.Load<Risiko3D.Runtime.Configuration.GameRuntimeConfig>("Runtime/GameRuntimeConfig");
            BoardRuntimeInstaller.EnsureCreated(cfg);
            Debug.Log("[Risiko3D][BoardDebug] Board runtime rebuilt.");
        }

        [MenuItem("Risiko3D/Board Debug/Export Territory Positions From Runtime")]
        public static void ExportTerritoryPositionsFromRuntime()
        {
            var board = Object.FindFirstObjectByType<BoardBootstrap>();
            if (board == null)
            {
                Debug.LogError("[Risiko3D][BoardDebug] BoardBootstrap not found. Enter Play Mode first.");
                return;
            }

            var lines = new StringBuilder();
            lines.AppendLine("{");
            lines.AppendLine("  \"mapId\": \"world-classic\",");
            lines.AppendLine("  \"version\": \"1.0.0\",");
            lines.AppendLine("  \"positions\": [");

            var entries = board.Nodes
                .OrderBy(k => k.Key)
                .Select(k =>
                {
                    var p = k.Value.transform.position;
                    var x = p.x.ToString("0.###", CultureInfo.InvariantCulture);
                    var y = p.y.ToString("0.###", CultureInfo.InvariantCulture);
                    var z = p.z.ToString("0.###", CultureInfo.InvariantCulture);
                    return $"    {{ \"territoryId\": \"{k.Key}\", \"x\": {x}, \"y\": {y}, \"z\": {z} }}";
                })
                .ToArray();

            for (var i = 0; i < entries.Length; i++)
            {
                var suffix = i == entries.Length - 1 ? string.Empty : ",";
                lines.AppendLine(entries[i] + suffix);
            }

            lines.AppendLine("  ]");
            lines.AppendLine("}");

            var path = "Assets/GameData/Map/world-classic-territory-positions.json";
            File.WriteAllText(path, lines.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[Risiko3D][BoardDebug] Exported territory positions to {path}");
        }
    }
}
#endif
