#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Risiko3D.EditorTools
{
    [InitializeOnLoad]
    public static class EditorSelectionGuard
    {
        private static double _nextCheckAt;

        static EditorSelectionGuard()
        {
            _nextCheckAt = EditorApplication.timeSinceStartup + 0.5d;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextCheckAt)
            {
                return;
            }

            _nextCheckAt = now + 0.5d;
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                return;
            }

            var filtered = selected.Where(o => o != null).ToArray();
            if (filtered.Length == selected.Length)
            {
                return;
            }

            Selection.objects = filtered;
            if (filtered.Length == 0)
            {
                Selection.activeObject = null;
            }

            RebuildInspectorTracker();
            Debug.LogWarning("[Risiko3D][Editor] Cleared invalid null object(s) from current selection.");
        }

        private static void OnSelectionChanged()
        {
            // Immediate sanitize to avoid inspector trying to bind null targets.
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                return;
            }

            if (selected.All(o => o != null))
            {
                return;
            }

            Selection.objects = selected.Where(o => o != null).ToArray();
            RebuildInspectorTracker();
        }

        [MenuItem("Risiko3D/Tools/Editor Recovery/Clear Selection + Rebuild Inspector")]
        private static void RecoverInspectorTargets()
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            Selection.activeObject = null;
            RebuildInspectorTracker();
            Debug.Log("[Risiko3D][Editor] Inspector recovery executed (selection cleared + tracker rebuilt).");
        }

        private static void RebuildInspectorTracker()
        {
            EditorApplication.delayCall += () =>
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            };
        }
    }
}
#endif
