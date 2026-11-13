using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding.Editor
{
    [InitializeOnLoad]
    static class NavigationPulseEditorPreview
    {
        static double nextTick;
        static NavigationPulseEditorPreview() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 1.0 / 30;
            foreach (var sequence in Object.FindObjectsByType<NavigationPulseSequence>(FindObjectsSortMode.None))
                if (sequence.PreviewInEditor)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                    return;
                }
        }
    }
}
