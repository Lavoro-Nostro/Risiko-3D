#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Risiko3D.EditorTools
{
    public static class ProjectAutoSetup
    {
        private const string PrefKey = "Risiko3D.AutoSetup.LastProject";

        [InitializeOnLoadMethod]
        private static void Run()
        {
            var projectPath = System.IO.Directory.GetCurrentDirectory();
            var last = EditorPrefs.GetString(PrefKey, string.Empty);
            if (last == projectPath)
            {
                return;
            }

            M0SetupTools.RunFullEditorSetupSilently();
            EditorPrefs.SetString(PrefKey, projectPath);
            Debug.Log("[Risiko3D][Setup] Auto-setup applied for this project.");
        }
    }
}
#endif

