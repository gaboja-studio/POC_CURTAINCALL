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
        public PlaytestCollectionSettings collection = new();
        public bool Has(string tag) => tags != null && Array.IndexOf(tags, tag) >= 0;
        public PlaytestCollectionSettings Collection => collection ?? new();
        public bool AllowsLogs => Has(Logs) && VersionSelection.Matches(gameVersion, Collection.logVersions);
        public bool AllowsSurveys(string version) => Has(Surveys) && gameVersion == version
            && (Collection.night.Matches(version) || Collection.exit.Matches(version));
        public static PlaytestBuildTags Current()
        {
#if UNITY_EDITOR
            return new PlaytestBuildTags { gameVersion = Application.version, tags = EditorTags(), collection = PlaytestCollectionSettings.Load() };
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

    public static class VersionSelection
    {
        public static bool Valid(string pattern) => pattern != null && System.Text.RegularExpressions.Regex.IsMatch(pattern, @"^(\*|[0-9]+\.[0-9]+\.(?:[0-9]+|x))$");
        public static bool Matches(string version, string pattern)
        {
            if (!Valid(pattern) || version == null || !System.Text.RegularExpressions.Regex.IsMatch(version, @"^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$")) return false;
            if (pattern == "*") return true;
            if (!pattern.EndsWith(".x", StringComparison.Ordinal)) return version == pattern;
            return version.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal);
        }
    }

    [Serializable] public sealed class SurveyDefinition
    {
        public bool enabled = true;
        public string id, title, versions = "0.1.x", formUrl, entry;
        public int round = 1, nightDay = 1;
        public bool Valid => !string.IsNullOrEmpty(id) && System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9_-]{1,40}$")
            && round >= 1 && round <= 999999999 && nightDay >= 1 && VersionSelection.Valid(versions) && SurveyLaunchPolicy.ValidEntry(entry)
            && Uri.TryCreate(formUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "docs.google.com"
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            && System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath, @"^/forms/d/e/[A-Za-z0-9_-]+/viewform$");
        public bool Matches(string version) => enabled && Valid && VersionSelection.Matches(version, versions);
        public bool Legacy(string kind) => round == 1 && id == "M1_" + kind
            && formUrl == (kind == SurveyLaunchPolicy.Night ? SurveyLaunchPolicy.NightForm : SurveyLaunchPolicy.ExitForm)
            && entry == (kind == SurveyLaunchPolicy.Night ? SurveyLaunchPolicy.NightEntry : SurveyLaunchPolicy.ExitEntry);
    }

    [Serializable] public sealed class PlaytestCollectionSettings
    {
        public string logVersions = "*";
        public SurveyDefinition night = new() { id = "M1_NIGHT1", title = "M1 밤 정비", formUrl = SurveyLaunchPolicy.NightForm, entry = SurveyLaunchPolicy.NightEntry };
        public SurveyDefinition exit = new() { id = "M1_EXIT", title = "M1 게임 종료", formUrl = SurveyLaunchPolicy.ExitForm, entry = SurveyLaunchPolicy.ExitEntry };
        public SurveyDefinition For(string kind) => kind == SurveyLaunchPolicy.Night ? night : kind == SurveyLaunchPolicy.Exit ? exit : throw new ArgumentException("Unknown survey trigger.");
        public void Validate()
        {
            if (!VersionSelection.Valid(logVersions) || night == null || exit == null || night.enabled && !night.Valid || exit.enabled && !exit.Valid)
                throw new ArgumentException("버전 범위, 설문 ID·회차, Google Forms 주소·연결 질문을 확인하세요.");
        }
#if UNITY_EDITOR
        public static string SettingsPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Scripts", "Telemetry", "playtest-settings.json");
        public static PlaytestCollectionSettings Load()
        {
            var settings = File.Exists(SettingsPath) ? JsonUtility.FromJson<PlaytestCollectionSettings>(File.ReadAllText(SettingsPath)) : new();
            if (settings == null) throw new ArgumentException("설문 설정 파일을 읽을 수 없습니다.");
            settings.Validate(); return settings;
        }
#endif
    }
}
