using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>기획/디자이너용 추격 단계별 전등 연출 설정 창. 플레이 중에도 값을 바꾸면 즉시 반영되고 단계·암전을 미리 볼 수 있다.</summary>
    public sealed class PursuitLightingWindow : EditorWindow
    {
        int stageIndex, previewRoom;
        Vector2 scroll;
        GUIStyle chip;
        [MenuItem("Tools/Curtain Call/Pursuit/Lighting Designer")]
        public static PursuitLightingWindow Open()
        {
            var window = GetWindow<PursuitLightingWindow>("추격 전등 연출");
            window.minSize = new Vector2(420, 560); window.Show(); return window;
        }
        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable() => EditorApplication.update -= Tick;
        double next;
        void Tick() { if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup > next) { next = EditorApplication.timeSinceStartup + .2; Repaint(); } }

        void OnGUI()
        {
            chip ??= new GUIStyle(EditorStyles.miniButton) { fixedHeight = 18, padding = new RectOffset(6, 6, 1, 1) };
            var asset = GameSettings.WaitingRoom;
            if (asset == null) { EditorGUILayout.HelpBox("WaitingRoomSettings 에셋을 찾을 수 없습니다.", MessageType.Error); return; }
            var so = new SerializedObject(asset); so.Update();
            var pursuit = so.FindProperty("pursuit");
            var root = pursuit.FindPropertyRelative("Lighting");
            var stages = root.FindPropertyRelative("Stages");
            var pursuitStages = pursuit.FindPropertyRelative("Stages");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Label("WaitingRoomSettings › 추격 › 전등 연출", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("에셋 선택", EditorStyles.toolbarButton)) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(50))) { so.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(asset); }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (EditorApplication.isPlaying) DrawLive(stages.arraySize);

            Header("공통");
            Prop(root, "Enabled", "단계 연출 사용");
            Prop(root, "IncludeUnassignedLights", "방 미지정 조명 포함");
            Prop(root, "TransitionSeconds", "밝기·색 전환 (초)");

            Header("단계별 설정");
            EditorGUILayout.HelpBox("단계 번호는 추격 단계(시작 시간)와 같습니다. 1~4단계 연출은 누적 적용이 원칙이므로 앞 단계 효과를 뒤 단계에서도 유지해 주세요. 조명 단계가 부족하면 마지막 조명 단계를 사용합니다.", MessageType.None);
            DrawOverview(stages, pursuitStages);
            if (stages.arraySize == 0) {
                if (GUILayout.Button("기본 4단계 만들기")) { Undo.RecordObject(asset, "기본 전등 단계"); asset.Pursuit.Lighting.Stages = new PursuitLightSettings().Stages; EditorUtility.SetDirty(asset); GUIUtility.ExitGUI(); }
            } else {
                stageIndex = Mathf.Clamp(stageIndex, 0, stages.arraySize - 1);
                int tab = GUILayout.Toolbar(stageIndex, Enumerable.Range(0, stages.arraySize).Select(i => StageLabel(i, pursuitStages)).ToArray());
                // 입력 칸에 포커스가 남아 있으면 단계를 바꿔도 이전 값이 보이므로 포커스를 해제한다.
                if (tab != stageIndex) { stageIndex = tab; GUI.FocusControl(null); }
                var stage = stages.GetArrayElementAtIndex(stageIndex);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    GUILayout.Label("밝기 / 색", EditorStyles.boldLabel);
                    Prop(stage, "Brightness", "조명 밝기 배율"); Prop(stage, "AmbientBrightness", "환경광 배율");
                    Prop(stage, "Tint", "색조"); Prop(stage, "TintAmount", "색조 적용량");
                    DrawSwatch(stage);
                    GUILayout.Space(4);
                    Prop(stage, "CracklePerMinute", "조명당 분당 지직임"); Prop(stage, "CrackleSeconds", "지직임 길이 (초)");
                    Prop(stage, "CrackleDip", "지직일 때 최저 밝기"); Prop(stage, "Sparks", "스파크 개수");
                    GUILayout.Space(4);
                    Prop(stage, "EntryFlickers", "깜빡임 횟수"); Prop(stage, "EntryFlickerCycle", "1회 길이 (초)");
                    GUILayout.Space(4);
                    Prop(stage, "RoomFlickerInterval", "평균 간격 (초, 0=끔)"); Prop(stage, "RoomFlickerJitter", "간격 랜덤 ±(초)");
                    Prop(stage, "RoomFlickerSeconds", "깜빡임 길이 (초)"); Prop(stage, "NeighborDistance", "근처 방 거리 (m)");
                    Prop(stage, "RoomFlickerOffIntensity", "꺼짐 밝기 배율"); Prop(stage, "RoomFlickerOffAmbient", "꺼짐 환경광 배율(방 안)");
                }
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUI.DisabledScope(stageIndex == 0))
                        if (GUILayout.Button("이전 단계 값 복사")) { so.ApplyModifiedProperties(); CopyFromPrevious(asset); GUIUtility.ExitGUI(); }
                    if (GUILayout.Button("단계 추가")) { so.ApplyModifiedProperties(); AddStage(asset); GUIUtility.ExitGUI(); }
                    using (new EditorGUI.DisabledScope(stages.arraySize <= 1))
                        if (GUILayout.Button("단계 제거")) { stages.DeleteArrayElementAtIndex(stageIndex); stageIndex = Mathf.Max(0, stageIndex - 1); }
                }
            }

            EditorGUILayout.Space(8);
            Prop(root, "CaptureOffIntensity", "꺼짐 밝기 배율"); Prop(root, "CaptureOffAmbient", "꺼짐 환경광 배율(방 안)");
            Prop(root, "CaptureDarkenSpill", "주변 조명 새는 빛도 끄기"); Prop(root, "CaptureScreenDarkness", "꺼짐 화면 어둡게 (방 안)");
            Prop(root, "CaptureOnBrightness", "켜짐 밝기 배율 (원래 대비)"); Prop(root, "CaptureOnAmbient", "켜짐 환경광 배율(방 안)");
            Prop(root, "CaptureFlickerRate", "초당 전환 횟수"); Prop(root, "CaptureDarkRatio", "꺼짐 비율");
            EditorGUILayout.HelpBox("포획 연출 동안 그 방은 '완전 암전 ↔ 원래 밝기(현재 단계 색)'를 반복하고, 끝나면 현재 단계 조명으로 돌아갑니다. 꺼진 순간이 잘 보이면 '꺼짐 화면 어둡게'를 1에 가깝게 올리세요(반사·발광 재질까지 가립니다).", MessageType.None);

            EditorGUILayout.Space(8);
            Prop(root, "SparkMaterial", "재질 (비우면 SawSpark)"); Prop(root, "SparkColor", "색"); Prop(root, "SparkSize", "크기"); Prop(root, "SparkSpeed", "속도");
            EditorGUILayout.Space(8);
            if (GUILayout.Button("전등 연출 기본값으로 되돌리기 (Undo 가능)")) {
                Undo.RecordObject(asset, "전등 연출 기본값"); asset.Pursuit.Lighting = new PursuitLightSettings(); EditorUtility.SetDirty(asset); GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndScrollView();
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);
        }

        static string StageLabel(int i, SerializedProperty pursuitStages)
            => i < pursuitStages.arraySize ? $"{i + 1}단계 · {pursuitStages.GetArrayElementAtIndex(i).FindPropertyRelative("StartsAt").floatValue:0}s" : $"{i + 1}단계 · (추격 단계 없음)";
        static void Header(string text) { EditorGUILayout.Space(8); GUILayout.Label(text, EditorStyles.boldLabel); }
        static void Prop(SerializedProperty parent, string name, string label) => EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));

        void DrawOverview(SerializedProperty stages, SerializedProperty pursuitStages)
        {
            if (stages.arraySize != pursuitStages.arraySize)
                EditorGUILayout.HelpBox($"추격 단계 {pursuitStages.arraySize}개 / 전등 단계 {stages.arraySize}개. 개수가 다르면 마지막 전등 단계를 이어서 사용합니다.", MessageType.Warning);
            GUILayout.Label("칩을 누르면 그 단계의 효과를 켜고 끕니다(Ctrl+Z 가능). 단계 이름을 누르면 아래 상세 설정으로 이동합니다.", EditorStyles.wordWrappedMiniLabel);
            var previous = new bool[Effects.Length];
            for (int i = 0; i < stages.arraySize; i++) {
                var s = stages.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope()) {
                    var oldColor = GUI.backgroundColor;
                    if (i == stageIndex) GUI.backgroundColor = new Color(.55f, .75f, 1f);
                    if (GUILayout.Button($"{i + 1}단계", chip, GUILayout.Width(48))) { stageIndex = i; GUI.FocusControl(null); }
                    GUI.backgroundColor = oldColor;
                    for (int f = 0; f < Effects.Length; f++) {
                        bool on = IsOn(s, f);
                        // 시작 깜빡임은 해당 단계 진입 1회 연출이므로 누락 경고 대상이 아니다.
                        bool missing = f != EntryEffect && previous[f] && !on;
                        GUI.backgroundColor = on ? new Color(.45f, .85f, .55f) : missing ? new Color(1f, .55f, .35f) : new Color(.55f, .55f, .55f);
                        string tip = (on ? "켜짐 · 눌러서 끄기" : "꺼짐 · 눌러서 켜기") + (missing ? "\n앞 단계에 있던 효과가 빠졌습니다(누적 적용 확인)." : "");
                        bool next = GUILayout.Toggle(on, new GUIContent(Effects[f], tip), chip);
                        GUI.backgroundColor = oldColor;
                        if (next != on) { SetEffect(s, f, next); stageIndex = i; GUI.FocusControl(null); on = next; }
                        previous[f] |= on;
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        static readonly string[] Effects = { "밝기↓", "지직", "스파크", "시작 깜빡", "방 깜빡", "붉은 색" };
        const int EntryEffect = 3;
        static bool IsOn(SerializedProperty s, int effect) => effect switch {
            0 => s.FindPropertyRelative("Brightness").floatValue < .99f || s.FindPropertyRelative("AmbientBrightness").floatValue < .99f,
            1 => s.FindPropertyRelative("CracklePerMinute").floatValue > 0,
            2 => s.FindPropertyRelative("Sparks").intValue > 0 && s.FindPropertyRelative("CracklePerMinute").floatValue > 0,
            3 => s.FindPropertyRelative("EntryFlickers").intValue > 0,
            4 => s.FindPropertyRelative("RoomFlickerInterval").floatValue > 0,
            _ => s.FindPropertyRelative("TintAmount").floatValue > .01f
        };
        /// <summary>켤 때는 기본 4단계 값(가장 강한 단계)을, 끌 때는 효과가 없는 값을 넣는다. 다른 세부값은 유지한다.</summary>
        static void SetEffect(SerializedProperty s, int effect, bool on)
        {
            var d = new PursuitLightSettings().Stages;
            var full = d[d.Length - 1];
            switch (effect) {
                case 0:
                    s.FindPropertyRelative("Brightness").floatValue = on ? full.Brightness : 1;
                    s.FindPropertyRelative("AmbientBrightness").floatValue = on ? full.AmbientBrightness : 1;
                    break;
                case 1:
                    s.FindPropertyRelative("CracklePerMinute").floatValue = on ? full.CracklePerMinute : 0;
                    break;
                case 2:
                    // 스파크는 지직일 때만 튀므로 켜면 지직임도 함께 켠다.
                    s.FindPropertyRelative("Sparks").intValue = on ? full.Sparks : 0;
                    if (on && s.FindPropertyRelative("CracklePerMinute").floatValue <= 0)
                        s.FindPropertyRelative("CracklePerMinute").floatValue = full.CracklePerMinute;
                    break;
                case 3:
                    s.FindPropertyRelative("EntryFlickers").intValue = on ? d[2].EntryFlickers : 0;
                    break;
                case 4:
                    s.FindPropertyRelative("RoomFlickerInterval").floatValue = on ? full.RoomFlickerInterval : 0;
                    break;
                default:
                    if (on && s.FindPropertyRelative("Tint").colorValue == Color.white) s.FindPropertyRelative("Tint").colorValue = full.Tint;
                    s.FindPropertyRelative("TintAmount").floatValue = on ? full.TintAmount : 0;
                    break;
            }
        }
        static void DrawSwatch(SerializedProperty stage)
        {
            var rect = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
            float b = Mathf.Clamp01(stage.FindPropertyRelative("Brightness").floatValue);
            var tint = stage.FindPropertyRelative("Tint").colorValue;
            float amount = stage.FindPropertyRelative("TintAmount").floatValue;
            var warm = new Color(1f, .93f, .82f);
            var left = new Rect(rect.x, rect.y, rect.width * .5f - 2, rect.height);
            var right = new Rect(rect.center.x + 2, rect.y, rect.width * .5f - 2, rect.height);
            EditorGUI.DrawRect(left, warm); GUI.Label(left, " 원래 조명", EditorStyles.miniBoldLabel);
            var result = Color.Lerp(warm, tint, amount) * Mathf.Lerp(.25f, 1, b); result.a = 1;
            EditorGUI.DrawRect(right, result); GUI.Label(right, " 이 단계 (근사)", EditorStyles.miniBoldLabel);
        }

        void CopyFromPrevious(WaitingRoomSettings asset)
        {
            var list = asset.Pursuit.Lighting.Stages;
            Undo.RecordObject(asset, "이전 전등 단계 복사");
            var copy = list[stageIndex - 1].Clone(); copy.EntryFlickers = 0;
            list[stageIndex] = copy; EditorUtility.SetDirty(asset);
        }
        void AddStage(WaitingRoomSettings asset)
        {
            var lighting = asset.Pursuit.Lighting;
            Undo.RecordObject(asset, "전등 단계 추가");
            var list = (lighting.Stages ?? new PursuitLightStage[0]).ToList();
            var copy = list.Count > 0 ? list[list.Count - 1].Clone() : new PursuitLightStage();
            copy.EntryFlickers = 0; list.Add(copy);
            lighting.Stages = list.ToArray(); stageIndex = list.Count - 1; EditorUtility.SetDirty(asset);
        }

        void DrawLive(int stageCount)
        {
            var director = PursuitDirector.Current;
            var lighting = director != null ? director.GetComponent<PursuitLighting>() : null;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                GUILayout.Label("● 플레이 미리보기", EditorStyles.boldLabel);
                if (director == null || lighting == null) { GUILayout.Label("추격 대기실 씬이 실행 중이 아닙니다.", EditorStyles.miniLabel); return; }
                int stage = director.CurrentStageIndex;
                GUILayout.Label($"단계 {(stage < 0 ? "대기" : (stage + 1).ToString())}{(director.StageIsForced ? " (강제)" : "")} · 경과 {Mathf.Max(0, lighting.Clock):0.0}s · 조명 {lighting.LightCount}개", EditorStyles.miniLabel);
                GUILayout.Label($"밝기 ×{lighting.Brightness:0.00} · 환경광 ×{lighting.AmbientMultiplier:0.00} · 색조 {lighting.TintAmount:0.00} · 스파크 {lighting.SparkBursts}회", EditorStyles.miniLabel);
                if (director.RoomFlickerIndex >= 0 && director.Rooms[director.RoomFlickerIndex] != null)
                    GUILayout.Label($"최근 방 깜빡임: {director.Rooms[director.RoomFlickerIndex].name} @ {director.RoomFlickerAt:0.0}s", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!director.CanDebug)) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        for (int i = 0; i < stageCount; i++) if (GUILayout.Button($"{i + 1}단계")) director.DebugSetStage(i);
                        if (GUILayout.Button("강제 해제")) director.DebugSetStage(-1);
                    }
                    using (new EditorGUILayout.HorizontalScope()) {
                        if (GUILayout.Button("방 깜빡임 (자동 선택)")) director.DebugRoomFlicker();
                        if (GUILayout.Button("시작 깜빡임 재생")) {
                            var rule = GameSettings.WaitingRoom.Pursuit.Lighting.StageFor(Mathf.Max(0, stage));
                            lighting.PreviewEntryFlicker(rule != null && rule.EntryFlickers > 0 ? rule.EntryFlickers : 3, rule?.EntryFlickerCycle ?? .42f);
                        }
                    }
                    var rooms = director.Rooms ?? new PursuitRoom[0];
                    if (rooms.Length > 0) using (new EditorGUILayout.HorizontalScope()) {
                        previewRoom = EditorGUILayout.Popup(Mathf.Clamp(previewRoom, 0, rooms.Length - 1), rooms.Select((r, i) => $"{i + 1:00} {(r != null ? r.name : "-")}").ToArray());
                        if (GUILayout.Button("카메라 방", GUILayout.Width(70))) {
                            var cam = Camera.main;
                            for (int i = 0; cam != null && i < rooms.Length; i++) if (rooms[i] != null && rooms[i].ContainsView(cam.transform.position)) { previewRoom = i; break; }
                        }
                        if (GUILayout.Button("포획 암전 4초", GUILayout.Width(100))) director.DebugCaptureBlackout(previewRoom, 4);
                    }
                }
                if (!director.CanDebug) GUILayout.Label("단계 강제·미리보기는 Dev 모드의 호스트에서 대기실 휴식 시간에만 가능합니다.", EditorStyles.miniLabel);
                if (GUILayout.Button("조명 목록 다시 수집")) lighting.Rebuild();
                GUILayout.Label("플레이 중 바꾼 값은 설정 에셋에 그대로 남습니다.", EditorStyles.miniLabel);
            }
        }
    }
}
