using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline.Editor
{
    public static class PlaytestBuildTagMenu
    {
        const string Logs = "CurtainCall/테스트 빌드 태그/로그 수집", Surveys = "CurtainCall/테스트 빌드 태그/0.1.0 설문";
        static void Toggle(string tag) { string key = PlaytestBuildTags.PreferenceKey(tag); EditorPrefs.SetBool(key, !EditorPrefs.GetBool(key, false)); }
        [MenuItem(Logs)] static void ToggleLogs() => Toggle(PlaytestBuildTags.Logs);
        [MenuItem(Logs, true)] static bool LogsChecked() { Menu.SetChecked(Logs, PlaytestBuildTags.Current().AllowsLogs); return !EditorApplication.isPlayingOrWillChangePlaymode; }
        [MenuItem(Surveys)] static void ToggleSurveys() => Toggle(PlaytestBuildTags.Surveys);
        [MenuItem(Surveys, true)] static bool SurveysChecked() { Menu.SetChecked(Surveys, PlaytestBuildTags.Current().Has(PlaytestBuildTags.Surveys)); return !EditorApplication.isPlayingOrWillChangePlaymode; }
        [MenuItem("CurtainCall/테스트 빌드 태그/현재 설정 확인")]
        static void Show() => EditorUtility.DisplayDialog("이번 PC의 다음 빌드", "버전: " + Application.version
            + "\n로그 수집: " + (PlaytestBuildTags.Current().AllowsLogs ? "켜짐" : "꺼짐")
            + "\n0.1.0 설문: " + (PlaytestBuildTags.Current().Has(PlaytestBuildTags.Surveys) ? "켜짐" : "꺼짐")
            + "\n\n각 항목을 따로 켜고 끌 수 있습니다. 새 빌드에만 적용되며 기본값은 둘 다 꺼짐입니다.", "확인");

        const string ResetMain = "CurtainCall/설문 캐시 초기화/기본 플레이어만";
        const string ResetAll = "CurtainCall/설문 캐시 초기화/MPPM 보조 플레이어 포함";
        [MenuItem(ResetMain)] static void ResetMainCache() => ConfirmReset(false);
        [MenuItem(ResetAll)] static void ResetAllCaches() => ConfirmReset(true);
        [MenuItem(ResetMain, true)] [MenuItem(ResetAll, true)]
        static bool CanReset() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        static void ConfirmReset(bool includeVirtualPlayers)
        {
            if (!CanReset()) return;
            string target = includeVirtualPlayers ? "기본 플레이어와 모든 MPPM 보조 플레이어" : "기본 플레이어";
            if (!EditorUtility.DisplayDialog("설문 캐시 초기화", target + "의 0.1.0 설문 열림 기록을 지웁니다."
                + "\n첫날 밤·종료 설문을 다시 확인할 수 있습니다. 기존 로그와 Google 설문 응답은 유지됩니다."
                + "\n같은 PC 저장공간을 쓰는 빌드에도 적용됩니다. MPPM 보조 플레이어도 모두 재생을 멈춘 뒤 진행하세요.", "초기화", "취소")) return;
            try
            {
                int count = ResetSurveyCaches(Application.persistentDataPath, includeVirtualPlayers);
                EditorUtility.DisplayDialog("설문 캐시 초기화 완료", "설문 열림 기록 " + count + "개를 지웠습니다."
                    + "\n0.1.0 설문 태그를 켜고 다시 플레이하세요. 기존 로그는 그대로입니다.", "확인");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("설문 캐시 초기화 미완료", "일부 기록을 지우지 못했습니다."
                    + "\n기본·MPPM 플레이어의 재생을 모두 멈추고 폴더 접근 권한을 확인한 뒤 다시 시도하세요.", "확인");
            }
        }

        internal static int ResetSurveyCaches(string persistentPath, bool includeVirtualPlayers)
        {
            string root = Path.GetFullPath(Path.Combine(persistentPath, "TelemetryPipeline"));
            if (!PlainDirectory(root)) return 0;
            int count = ResetMarkers(Path.Combine(root, "SurveyCache"));
            string clients = Path.Combine(root, "clients");
            if (!includeVirtualPlayers || !PlainDirectory(clients)) return count;
            foreach (string client in Directory.GetDirectories(clients))
                if (PlainDirectory(client)) count += ResetMarkers(Path.Combine(client, "SurveyCache"));
            return count;
        }
        static bool PlainDirectory(string path) => Directory.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        static int ResetMarkers(string directory)
        {
            if (!PlainDirectory(directory)) return 0;
            int count = 0;
            foreach (string file in Directory.GetFiles(directory, "*.opened", SearchOption.TopDirectoryOnly))
            {
                // Delete only our two forms' marker files, never logs, nested folders or link targets.
                if (!Regex.IsMatch(Path.GetFileName(file), "^0\\.1\\.0-(NIGHT1|EXIT)-[0-9a-f]{64}\\.opened$", RegexOptions.IgnoreCase)
                    || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                File.Delete(file); count++;
            }
            return count;
        }
    }
}
