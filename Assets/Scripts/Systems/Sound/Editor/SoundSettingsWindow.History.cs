using UnityEditor;
using UnityEngine;

namespace CurtainCall.Sound.Editor
{
    public sealed partial class SoundSettingsWindow
    {
        Vector2 historyScroll;

        void DrawHistoryTab()
        {
            EditorGUILayout.HelpBox(
                "플레이 중 게임 코드가 보낸 재생 요청을 최신순으로 보여 줍니다. 실제 게임에서 각 상황을 겪은 뒤 여기서 어느 코드가 어떤 사운드를 냈는지(또는 왜 안 났는지) 확인합니다.\n" +
                "호출 위치를 누르면 해당 스크립트 줄이 열립니다.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Application.isPlaying ? $"기록 {SoundManager.History.Count}건 (최대 64)" : "플레이 모드가 아닙니다. 마지막 플레이 기록이 남아 있으면 보입니다.", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("기록 지우기", GUILayout.Width(80))) SoundManager.ClearHistory();
            }

            historyScroll = EditorGUILayout.BeginScrollView(historyScroll);
            var history = SoundManager.History;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var record = history[i];
                if (!Matches(record.Request, record.CueId, record.Caller)) continue;
                var situation = catalog.FindSituation(record.Request);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"{record.Time,7:0.0}s", EditorStyles.miniLabel, GUILayout.Width(52));
                    var color = GUI.contentColor;
                    GUI.contentColor = record.Result == SoundPlayResult.Played ? new Color(.45f, .85f, .55f)
                        : record.Result == SoundPlayResult.Cooldown ? new Color(.95f, .8f, .4f) : new Color(1f, .5f, .45f);
                    GUILayout.Label(ResultText(record.Result), EditorStyles.boldLabel, GUILayout.Width(74));
                    GUI.contentColor = color;
                    GUILayout.Label(new GUIContent(situation != null ? situation.label : record.Request, record.Request), GUILayout.Width(150));
                    GUILayout.Label("→ " + (string.IsNullOrEmpty(record.CueId) ? "-" : record.CueId) + (record.Loop ? " (루프)" : ""), GUILayout.Width(110));
                    if (!string.IsNullOrEmpty(record.CallerFile)
                        && GUILayout.Button(new GUIContent(record.Caller, record.CallerFile), EditorStyles.linkLabel))
                        SoundCodeUsage.Open(ToAssetPath(record.CallerFile), record.CallerLine);
                    GUILayout.FlexibleSpace();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static string ResultText(SoundPlayResult result) => result switch
        {
            SoundPlayResult.Played => "재생",
            SoundPlayResult.Cooldown => "간격 대기",
            SoundPlayResult.SituationOff => "상황 꺼짐",
            SoundPlayResult.CueOff => "사운드 꺼짐",
            SoundPlayResult.NoClip => "클립 없음",
            SoundPlayResult.Unknown => "목록에 없음",
            SoundPlayResult.NoCatalog => "카탈로그 없음",
            _ => "재생 안 함",
        };

        static string ToAssetPath(string fullPath)
        {
            string path = fullPath.Replace('\\', '/');
            int index = path.IndexOf("/Assets/", System.StringComparison.Ordinal);
            return index >= 0 ? path.Substring(index + 1) : path;
        }
    }
}
