using System;
using System.IO;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline
{
    [Serializable]
    public sealed class PlaytestBuildTags
    {
        public const string FileName = "curtaincall-build-tags.json";
        public const string Logs = "LOGS", Surveys = "SURVEY_010";
        public string gameVersion;
        public string[] tags = Array.Empty<string>();
        public bool Has(string tag) => tags != null && Array.IndexOf(tags, tag) >= 0;
        public bool AllowsLogs => Has(Logs);
        public bool AllowsSurveys(string version) => Has(Surveys) && version == "0.1.0" && gameVersion == version;
        public static PlaytestBuildTags Current()
        {
#if UNITY_EDITOR
            return new PlaytestBuildTags { gameVersion = Application.version, tags = EditorTags() };
#else
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, FileName);
                return File.Exists(path) ? JsonUtility.FromJson<PlaytestBuildTags>(File.ReadAllText(path)) ?? new() : new();
            }
            catch { return new(); } // Missing or corrupt tags never enable collection.
#endif
        }
#if UNITY_EDITOR
        public static string PreferenceKey(string tag)
        {
            string path = Application.dataPath.Replace('\\', '/');
            int virtualProject = path.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            if (virtualProject >= 0) path = path.Substring(0, virtualProject) + "/Assets";
            return "CurtainCall.Playtest." + DurableOutbox.Sha256(path.ToLowerInvariant()) + "." + tag;
        }
        static string[] EditorTags()
        {
            var result = new System.Collections.Generic.List<string>();
            foreach (string tag in new[] { Logs, Surveys })
                if (UnityEditor.EditorPrefs.GetBool(PreferenceKey(tag), false)) result.Add(tag);
            return result.ToArray();
        }
#endif
    }
}
