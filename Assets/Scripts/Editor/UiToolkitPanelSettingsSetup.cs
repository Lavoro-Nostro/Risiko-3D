#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Risiko3D.Editor
{
    [InitializeOnLoad]
    public static class UiToolkitPanelSettingsSetup
    {
        private const string AssetFolder = "Assets/Resources/UI/BoardLegend";
        private const string AssetPath = AssetFolder + "/BoardLegendPanelSettings.asset";

        static UiToolkitPanelSettingsSetup()
        {
            EditorApplication.delayCall += EnsurePanelSettingsAsset;
        }

        private static void EnsurePanelSettingsAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath) != null)
            {
                return;
            }

            Directory.CreateDirectory(AssetFolder);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.clearColor = false;
            panel.sortingOrder = 500;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.referenceDpi = 96f;
            panel.fallbackDpi = 96f;

            if (panel.themeStyleSheet == null)
            {
                var themeGuids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
                foreach (var guid in themeGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                    if (theme != null)
                    {
                        panel.themeStyleSheet = theme;
                        break;
                    }
                }
            }

            AssetDatabase.CreateAsset(panel, AssetPath);
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
