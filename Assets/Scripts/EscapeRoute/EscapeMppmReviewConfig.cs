#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using CurtainCall.Settings;

namespace CurtainCall.EscapeRoute
{
    // Multiplayer input/quality overrides require a local opt-in; ordinary Play stays solo.
    public static class EscapeMppmReviewConfig
    {
        public static string Key => "CurtainCall.Escape.MppmReview." + PlayerSettings.productGUID;
        public const string PursuerScene = "Assets/Scenes/Tests/EscapeRoute/Pursuer/UnderstagePursuerPlaytest.unity";
        public static int PursuerPlayers
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(Key + ".PursuerPlayers", 1), 1, 4);
            set => EditorPrefs.SetInt(Key + ".PursuerPlayers", Mathf.Clamp(value, 1, 4));
        }
        internal static bool TryPursuerSession(Scene scene, out bool client, out int participants)
        {
            participants = PursuerPlayers;
            client = false;
            if (scene.path != PursuerScene || participants <= 1) return false;
            var type = Type.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualProjectsEditor, UnityEditor.MultiplayerModule");
            if (type != null)
                client = (bool)(type.GetProperty("IsClone", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? false)
                    || (bool)(type.GetProperty("IsScenarioClone", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? false);
            return true;
        }
        [Serializable] public class Settings { public string folder; public string[] clones; public bool lowMemoryPreview; }
        static int originalMipmapLimit = -1;
        static int originalFrameRate, originalVSyncCount;
        static bool previewConfigured;
        const string WorkerCountKey = "CurtainCall.Escape.MppmReview.ImportWorkers";

        [InitializeOnLoadMethod]
        static void RegisterPreviewRestore()
        {
            EditorApplication.playModeStateChanged -= RestorePreview;
            EditorApplication.playModeStateChanged += RestorePreview;
        }

        static void RestorePreview(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && ShouldReleaseImportWorkers())
            {
                // Idle import workers retained several GB alongside the loaded facility.
                // SessionState survives the Play domain reload and is local to this editor process.
                if (SessionState.GetInt(WorkerCountKey, -1) < 0)
                    SessionState.SetInt(WorkerCountKey, AssetDatabase.DesiredWorkerCount);
                AssetDatabase.DesiredWorkerCount = 0;
                AssetDatabase.ForceToDesiredWorkerCount();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                int workers = SessionState.GetInt(WorkerCountKey, -1);
                if (workers >= 0) { AssetDatabase.DesiredWorkerCount = workers; SessionState.EraseInt(WorkerCountKey); }
            }
            if (state != PlayModeStateChange.ExitingPlayMode || !previewConfigured) return;
            Application.targetFrameRate = originalFrameRate;
            QualitySettings.vSyncCount = originalVSyncCount;
            if (originalMipmapLimit >= 0) QualitySettings.globalTextureMipmapLimit = originalMipmapLimit;
            originalMipmapLimit = -1;
            previewConfigured = false;
        }
        static bool ShouldReleaseImportWorkers()
        {
            if (TryGet(out _, out _)) return true;
            var spaces = GameSettings.Base.SpaceTransition;
            return SceneManager.GetSceneByName(spaces.WaitingRoomScene).isLoaded
                || SceneManager.GetSceneByName(spaces.EscapeRouteScene).isLoaded
                || SceneManager.GetSceneByPath(PursuerScene).isLoaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareBackground()
        {
            if (!TryGet(out _, out _)) return;
            var settings = JsonUtility.FromJson<Settings>(EditorPrefs.GetString(Key));
            originalFrameRate = Application.targetFrameRate;
            originalVSyncCount = QualitySettings.vSyncCount;
            previewConfigured = true;
            Application.runInBackground = true;
            Application.targetFrameRate = settings.lowMemoryPreview ? 24 : 40;
            QualitySettings.vSyncCount = 0;
            // Multiple editors share one machine. This opt-in affects preview textures only.
            // Keep the authored quality settings and ordinary Play unchanged after the review.
            if (settings.lowMemoryPreview)
            {
                originalMipmapLimit = QualitySettings.globalTextureMipmapLimit;
                QualitySettings.globalTextureMipmapLimit = Mathf.Max(2, originalMipmapLimit);
            }
        }
        public static bool TryGet(out string folder, out int slot) => TryGet(out folder, out slot, out _);
        public static bool TryGet(out string folder, out int slot, out int participantCount)
        {
            folder = null; slot = 0; participantCount = 1;
            string json = EditorPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return false;
            var settings = JsonUtility.FromJson<Settings>(json);
            if (settings == null || string.IsNullOrEmpty(settings.folder)) return false;
            participantCount = 1 + (settings.clones?.Length ?? 0);
            var type = Type.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualProjectsEditor, UnityEditor.MultiplayerModule", true);
            bool clone = (bool)type.GetProperty("IsClone", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (clone)
            {
                string identifier = type.GetProperty("CloneIdentifier", BindingFlags.Public | BindingFlags.Static).GetValue(null).ToString();
                slot = Array.IndexOf(settings.clones ?? Array.Empty<string>(), identifier) + 1;
                if (slot < 1 || slot > 3) throw new InvalidOperationException("Unconfigured MPPM review clone: " + identifier);
            }
            folder = Path.GetFullPath(settings.folder);
            return true;
        }
    }
}
#endif
