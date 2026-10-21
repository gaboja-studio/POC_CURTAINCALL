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
    }
}
