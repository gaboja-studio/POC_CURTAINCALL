#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Explicit, local-only editor opt-in. Cleared after the review; ordinary Play stays solo.
    public static class EscapeMppmReviewConfig
    {
        public static string Key => "CurtainCall.Escape.MppmReview." + PlayerSettings.productGUID;
        [Serializable] public class Settings { public string folder; public string[] clones; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrepareBackground()
        {
            if (!TryGet(out _, out _)) return;
            Application.runInBackground = true;
            Application.targetFrameRate = 40;
            QualitySettings.vSyncCount = 0;
        }
        public static bool TryGet(out string folder, out int slot)
        {
            folder = null; slot = 0;
            string json = EditorPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return false;
            var settings = JsonUtility.FromJson<Settings>(json);
            if (settings == null || string.IsNullOrEmpty(settings.folder)) return false;
            var type = Type.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualProjectsEditor, UnityEditor.MultiplayerModule", true);
            bool clone = (bool)type.GetProperty("IsClone", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (clone)
            {
                string identifier = type.GetProperty("CloneIdentifier", BindingFlags.Public | BindingFlags.Static).GetValue(null).ToString();
                slot = Array.IndexOf(settings.clones, identifier) + 1;
                if (slot < 1 || slot > 3) throw new InvalidOperationException("Unconfigured MPPM review clone: " + identifier);
            }
            folder = Path.GetFullPath(settings.folder);
            return true;
        }
    }
}
#endif
