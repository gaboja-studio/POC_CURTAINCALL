using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CurtainCall.Telemetry.Pipeline.Editor
{
    public static class PlaytestBuildTagMenu
    {
        const string Logs = "CurtainCall/테스트 빌드 태그/로그 수집", Surveys = "CurtainCall/테스트 빌드 태그/설문 사용";
        static void Toggle(string tag) { string key = PlaytestBuildTags.PreferenceKey(tag); EditorPrefs.SetBool(key, !EditorPrefs.GetBool(key, false)); }
        [MenuItem(Logs)] static void ToggleLogs() => Toggle(PlaytestBuildTags.Logs);
        [MenuItem(Logs, true)] static bool LogsChecked() { Menu.SetChecked(Logs, PlaytestBuildTags.Current().AllowsLogs); return !EditorApplication.isPlayingOrWillChangePlaymode; }
        [MenuItem(Surveys)] static void ToggleSurveys() => Toggle(PlaytestBuildTags.Surveys);
        [MenuItem(Surveys, true)] static bool SurveysChecked() { Menu.SetChecked(Surveys, PlaytestBuildTags.Current().Has(PlaytestBuildTags.Surveys)); return !EditorApplication.isPlayingOrWillChangePlaymode; }
        [MenuItem("CurtainCall/테스트 빌드 태그/현재 설정 확인")]
        static void Show() => EditorUtility.DisplayDialog("이번 PC의 다음 빌드", "버전: " + Application.version
            + "\n로그 수집: " + (PlaytestBuildTags.Current().AllowsLogs ? "켜짐" : "꺼짐")
            + "\n설문 사용: " + (PlaytestBuildTags.Current().Has(PlaytestBuildTags.Surveys) ? "켜짐" : "꺼짐")
            + "\n현재 버전 설문 대상: " + (PlaytestBuildTags.Current().AllowsSurveys(Application.version) ? "해당" : "없음")
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
            if (!EditorUtility.DisplayDialog("설문 캐시 초기화", target + "의 설문 열림 기록을 지웁니다."
                + "\n첫날 밤·종료 설문을 다시 확인할 수 있습니다. 기존 로그와 Google 설문 응답은 유지됩니다."
                + "\n같은 PC 저장공간을 쓰는 빌드에도 적용됩니다. MPPM 보조 플레이어도 모두 재생을 멈춘 뒤 진행하세요.", "초기화", "취소")) return;
            try
            {
                int count = ResetSurveyCaches(Application.persistentDataPath, includeVirtualPlayers);
                EditorUtility.DisplayDialog("설문 캐시 초기화 완료", "설문 열림 기록 " + count + "개를 지웠습니다."
                    + "\n현재 버전에 맞는 설문 설정과 설문 사용 태그를 확인하세요. 기존 로그는 그대로입니다.", "확인");
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
                // Delete only our survey markers, never logs, nested folders or link targets.
                if (!Regex.IsMatch(Path.GetFileName(file), "^(?:0\\.1\\.0-(NIGHT1|EXIT)|campaign)-[0-9a-f]{64}\\.opened$", RegexOptions.IgnoreCase)
                    || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                File.Delete(file); count++;
            }
            return count;
        }
    }

    public sealed class PlaytestCollectionWindow : EditorWindow
    {
        PlaytestCollectionSettings settings;
        Label result;
        [MenuItem("CurtainCall/로그·설문 설정")]
        public static void ShowWindow() => GetWindow<PlaytestCollectionWindow>("로그·설문 설정");
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            try { settings = PlaytestCollectionSettings.Load(); }
            catch (Exception error) { rootVisualElement.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); return; }
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.paddingLeft = 12; scroll.style.paddingRight = 12;
            rootVisualElement.Add(scroll);
            scroll.Add(new HelpBox("저장 후 새 빌드에 적용됩니다. 실제 로그에는 0.2.1 같은 정확한 버전을 남깁니다. 태그는 PC마다 별도로 켜세요.", HelpBoxMessageType.Info));
            Text(scroll, "로그 대상 버전", settings.logVersions, v => settings.logVersions = v.Trim());
            scroll.Add(new Label("버전 예: 0.2.0 (하나), 0.2.x (패치 전체), * (모든 버전)"));
            Survey(scroll, "밤 정비 설문", settings.night, true);
            Survey(scroll, "런 퇴장 설문", settings.exit, false);
            result = new Label(); result.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(new Button(() => {
                try {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생을 멈춘 뒤 저장하세요.");
                    settings.Validate(); File.WriteAllText(PlaytestCollectionSettings.SettingsPath, JsonUtility.ToJson(settings, true));
                    result.text = "저장 완료. 기존 배포 게임은 바뀌지 않습니다. 새 폼은 응답 시트의 대시보드 연결도 필요합니다.";
                } catch (Exception error) { result.text = "저장하지 못했습니다: " + error.Message; }
            }) { text = "설정 저장" });
            scroll.Add(result);
        }
        static void Text(VisualElement parent, string label, string value, Action<string> changed)
        {
            var field = new TextField(label) { value = value }; field.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(field);
        }
        static void Survey(VisualElement parent, string heading, SurveyDefinition survey, bool night)
        {
            var panel = new VisualElement(); panel.style.marginTop = 16; panel.style.marginBottom = 16; parent.Add(panel);
            var title = new Label(heading); title.style.unityFontStyleAndWeight = FontStyle.Bold; panel.Add(title);
            var enabled = new Toggle("사용") { value = survey.enabled }; enabled.RegisterValueChangedCallback(e => survey.enabled = e.newValue); panel.Add(enabled);
            Text(panel, "설문 이름", survey.title, v => survey.title = v);
            Text(panel, "대상 버전", survey.versions, v => survey.versions = v.Trim());
            Text(panel, "설문 ID", survey.id, v => survey.id = v.Trim());
            var round = new IntegerField("설문 회차") { value = survey.round }; round.RegisterValueChangedCallback(e => survey.round = e.newValue); panel.Add(round);
            panel.Add(new HelpBox("같은 ID·회차는 PC 사용자별 최초 1회입니다. 패치만 바꿔도 다시 열리지 않습니다. 다시 받으려면 회차를 올리고 새 빌드를 만드세요. 다른 설문으로 교체할 때는 새 ID를 쓰세요.", HelpBoxMessageType.Info));
            if (night) { var day = new IntegerField("몇 일차 밤에 열기") { value = survey.nightDay }; day.RegisterValueChangedCallback(e => survey.nightDay = e.newValue); panel.Add(day); }
            else panel.Add(new Label("런을 플레이한 뒤 나가기·호스트 종료·연결 끊김으로 퇴장할 때"));
            Text(panel, "설문 주소", survey.formUrl, v => survey.formUrl = v.Trim());
            Text(panel, "자동 입력 질문 ID", survey.entry, v => survey.entry = v.Trim());
            var prefill = new TextField("미리 채워진 링크"); panel.Add(prefill);
            panel.Add(new Button(() => {
                if (!Uri.TryCreate(prefill.value, UriKind.Absolute, out var uri)) return;
                var match = Regex.Match(uri.Query, @"(?:[?&])(entry\.[0-9]+)=CC_LINK_SAMPLE(?:&|$)");
                if (!match.Success) { prefill.label = "CC_LINK_SAMPLE 링크 필요"; return; }
                survey.formUrl = uri.GetLeftPart(UriPartial.Path); survey.entry = match.Groups[1].Value;
                var fields = panel.Query<TextField>().ToList();
                fields.Find(f => f.label == "설문 주소").value = survey.formUrl;
                fields.Find(f => f.label == "자동 입력 질문 ID").value = survey.entry;
            }) { text = "미리 채워진 링크에서 주소·질문 가져오기" });
        }
    }
}
