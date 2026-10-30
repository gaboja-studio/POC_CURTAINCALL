using UnityEditor;
using UnityEngine;

namespace CurtainCall.PerformanceBackground.Editor
{
    [CustomEditor(typeof(PerformanceBackgroundMotion))]
    public sealed class PerformanceBackgroundMotionEditor : UnityEditor.Editor
    {
        static PerformanceBackgroundMotion preview;
        static double startedAt;
        static double lastTick;

        [InitializeOnLoadMethod]
        static void RegisterCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.quitting += StopPreview;
            EditorApplication.playModeStateChanged += _ => StopPreview();
        }

        public override void OnInspectorGUI()
        {
            // Restore before changing reference poses or serialized parameters.
            using (new EditorGUI.DisabledScope(preview != null)) DrawDefaultInspector();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorGUILayout.HelpBox("Preview는 Scene 뷰에서만 재생합니다. 중지·저장·Play 전환 시 원래 자세로 복구됩니다.", MessageType.Info);
            if (preview == target)
            {
                if (GUILayout.Button("Stop preview / Restore pose")) StopPreview();
            }
            else if (GUILayout.Button("Preview whole venue"))
            {
                BeginPreview((PerformanceBackgroundMotion)target);
            }
        }

        public static void BeginPreview(PerformanceBackgroundMotion motion)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || motion == null) return;
            StopPreview();
            preview = motion;
            startedAt = EditorApplication.timeSinceStartup;
            lastTick = startedAt;
            preview.PreviewParticles(18f, true);
            EditorApplication.update += Tick;
        }

        void OnDisable() { if (preview == target) StopPreview(); }

        static void Tick()
        {
            if (preview == null) { StopPreview(); return; }
            preview.Sample(EditorApplication.timeSinceStartup - startedAt);
            double now = EditorApplication.timeSinceStartup;
            preview.PreviewParticles((float)System.Math.Min(.1, now - lastTick), false);
            lastTick = now;
            SceneView.RepaintAll();
        }

        public static void StopPreview()
        {
            EditorApplication.update -= Tick;
            if (preview != null) preview.RestoreRestPose();
            preview = null;
            SceneView.RepaintAll();
        }

        sealed class SaveGuard : AssetModificationProcessor
        {
            static string[] OnWillSaveAssets(string[] paths)
            {
                StopPreview();
                return paths;
            }
        }
    }
}
