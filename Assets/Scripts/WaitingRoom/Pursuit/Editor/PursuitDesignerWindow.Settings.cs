using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        void DrawSettings()
        {
            var settings = GameSettings.WaitingRoom;
            var so = new SerializedObject(settings); so.Update();
            var root = so.FindProperty("pursuit");
            void Field(string name, string label) => EditorGUILayout.PropertyField(root.FindPropertyRelative(name), new GUIContent(label));
            GUILayout.Label("전역 단계 설정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("모든 방의 추격자가 공유합니다. 시작 시간은 추격 시작 후 누적 초입니다.", MessageType.None);
            Field("Enabled", "추격 시스템 사용");
            Field("LoseTargetSeconds", "공통 시야 상실 유예 (초)");
            var stages = root.FindPropertyRelative("Stages");
            if (stages.arraySize == 0) {
                if (GUILayout.Button("기본 단계 만들기")) { Undo.RecordObject(settings, "기본 추격 단계"); settings.Pursuit.Stages = new PursuitSettings().Stages; EditorUtility.SetDirty(settings); }
            } else {
                stageIndex = Mathf.Clamp(stageIndex, 0, stages.arraySize - 1);
                var labels = new string[stages.arraySize];
                for (int i = 0; i < labels.Length; i++) labels[i] = $"{i + 1}단계 · {stages.GetArrayElementAtIndex(i).FindPropertyRelative("StartsAt").floatValue:0}s";
                stageIndex = EditorGUILayout.Popup("편집 / 시야 미리보기", stageIndex, labels);
                var stage = stages.GetArrayElementAtIndex(stageIndex);
                void StageField(string name, string label) => EditorGUILayout.PropertyField(stage.FindPropertyRelative(name), new GUIContent(label));
                StageField("StartsAt", "시작 시간 (초)");
                StageField("PatrolSpeed", "기본 이동 (m/s)");
                var character = GameSettings.Base.Character;
                GUILayout.Label($"온전한 플레이어 걷기: {character.FreeMoveSpeed:0.##} m/s", EditorStyles.miniLabel);
                var speed = stage.FindPropertyRelative("ChaseSpeed");
                float current = speed.floatValue > 0 ? speed.floatValue : settings.Pursuit.Stages[stageIndex].ResolvedChaseSpeed;
                EditorGUI.BeginChangeCheck();
                float next = EditorGUILayout.FloatField("달리기 (m/s)", current);
                if (EditorGUI.EndChangeCheck()) speed.floatValue = Mathf.Max(.1f, next);
                GUILayout.Label($"온전한 플레이어 달리기: {character.FreeMoveSpeed * character.SprintSpeedMultiplier:0.##} m/s", EditorStyles.miniLabel);
                if (speed.floatValue <= 0) GUILayout.Label("현재는 기존 플레이어 달리기 배율을 환산한 값", EditorStyles.miniLabel);
                StageField("FieldOfView", "시야각 (°)"); StageField("SightRadius", "시야 범위 (m)");
                StageField("SeeThroughWalls", "벽 너머 탐지"); StageField("AlwaysNearest", "위치 무관 최인접 추격");
                StageField("WalkHearingRadius", "걷기 소리 거리 (m)"); StageField("RunHearingRadius", "달리기 소리 거리 (m)");
                StageField("InvestigateFraction", "직접 조사 거리 비율"); StageField("OccludedHearingMultiplier", "벽 뒤 청각 거리 배율");
                StageField("AlertSeconds", "공격 준비 (초)"); StageField("ListenSeconds", "두리번 확인 (초)");
                StageField("InvestigateSeconds", "소리 조사 제한 (초)"); StageField("SearchSeconds", "놓친 위치 수색 (초)");
                StageField("TrackingRadius", "추적 유지 거리 (m)");
                StageField("PatrolLeashDistance", "순찰 경로 이탈 허용 (m)");
                EditorGUILayout.HelpBox("이탈 거리 0 = 제한 없음. 순찰 경로(없으면 배치점)에서 추격자 또는 대상이 이 거리보다 멀어지면 추격을 끊고 복귀합니다. 복귀 중에는 시야·소리에 반응하지 않습니다. 거리에는 층 높이도 포함됩니다.", MessageType.None);
                if (!stage.FindPropertyRelative("AlwaysNearest").boolValue)
                    EditorGUILayout.HelpBox($"시야 상실 {root.FindPropertyRelative("LoseTargetSeconds").floatValue:0.##}초 또는 추격자↔대상 거리 {Mathf.Max(stage.FindPropertyRelative("SightRadius").floatValue, stage.FindPropertyRelative("TrackingRadius").floatValue):0.##}m 초과 시 마지막 위치를 수색합니다.", MessageType.None);
                if (stage.FindPropertyRelative("AlwaysNearest").boolValue) EditorGUILayout.HelpBox("이 단계는 시야각·탐지/유지 거리·벽 조건을 무시합니다. 순찰 경로 이탈 제한은 적용합니다.", MessageType.Info);
                DrawStageAnnouncement(stage.FindPropertyRelative("Announcement"));
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("단계 복제")) {
                        // 문구 표의 단계 번호도 같이 밀어 다음 단계 문구가 어긋나지 않게 한다.
                        var texts = PursuitTextTable.StageTexts(stages.arraySize);
                        texts.Insert(stageIndex + 1, texts[stageIndex]);
                        PursuitTextTable.RewriteStages(texts);
                        stages.InsertArrayElementAtIndex(stageIndex); stageIndex++;
                        stages.GetArrayElementAtIndex(stageIndex).FindPropertyRelative("StartsAt").floatValue += 60;
                    }
                    using (new EditorGUI.DisabledScope(stages.arraySize <= 1))
                        if (GUILayout.Button("단계 제거")) {
                            var texts = PursuitTextTable.StageTexts(stages.arraySize);
                            texts.RemoveAt(stageIndex);
                            PursuitTextTable.RewriteStages(texts);
                            stages.DeleteArrayElementAtIndex(stageIndex); stageIndex = Mathf.Max(0, stageIndex - 1);
                        }
                }
            }
            DrawScreenTexts();
            EditorGUILayout.Space(14); GUILayout.Label("단계별 전등 연출", EditorStyles.boldLabel);
            if (GUILayout.Button("전등 연출 창 열기")) PursuitLightingWindow.Open();
            EditorGUILayout.Space(14); GUILayout.Label("전방 상태 신호 / 외형", EditorStyles.boldLabel);
            Field("DirectionCue", "전방 바닥 신호 사용"); Field("DirectionCueLength", "표시 길이 (m)"); Field("DirectionCueOpacity", "표시 밝기");
            Field("DirectionCueSoftness", "가장자리 흐림");
            Field("PatrolCueColor", "미발견 / 복귀"); Field("AlertCueColor", "경계 / 조사 / 수색"); Field("ChaseCueColor", "발견 / 공격 준비 / 포획");
            Field("AdvancedAppearanceStage", "강화 외형 시작 단계"); Field("BloodPerCapture", "포획 1회당 혈흔 증가");
            EditorGUILayout.HelpBox("표시 각도는 현재 단계의 시야각을 자동으로 따릅니다. 길이는 탐지 거리와 별도로 설정합니다. 가장자리는 부드럽게 흐려지며 추격 시작 전에는 숨깁니다. '위치 무관 최인접 추격' 단계는 탐지 시 시야각을 무시하므로 빛 바깥에서도 추격합니다.", MessageType.None);
            EditorGUILayout.Space(10); GUILayout.Label("머리 위 인식 상태", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(root.FindPropertyRelative("Eyes"), new GUIContent("머리 위 눈 아이콘 (로컬 전용)"), true);
            EditorGUILayout.HelpBox("눈은 내 조작 캐릭터의 인식 범위 안에서만 표시합니다(시야/걷기/달리기 청각 중 최대 거리). 흰색=일반, 노랑=경계, 빨강=나를 추격. 다른 단원을 추격할 때는 빨강으로 표시하지 않습니다. 원본은 순백색이며 색/HDR 발광을 여기서 조정합니다.", MessageType.None);
            EditorGUILayout.Space(10); GUILayout.Label("추격 화면 테두리", EditorStyles.boldLabel);
            Field("ScreenEffect", "효과 사용"); Field("EdgeIntensity", "최대 강도"); Field("EdgeWidth", "테두리 폭"); Field("PulseAmount", "맥동 강도");
            EditorGUILayout.HelpBox("내 캐릭터가 실제 추격 대상일 때만 표시합니다. 맥동 강도를 0으로 두면 깜빡임 없이 부드러운 테두리만 표시합니다.", MessageType.None);
            EditorGUILayout.Space(10); GUILayout.Label("원거리 렌더 / 포획 유혈", EditorStyles.boldLabel);
            Field("CullDistantRenderers", "먼 추격자 렌더 숨김"); Field("EncounterSeconds", "접근 예측 (최소 5초)"); Field("RenderSafetyMargin", "렌더 여유 거리 (m)");
            Field("CaptureBloodInterval", "혈액 분출 간격 (초)"); Field("CaptureBloodAmount", "분출 양"); Field("CaptureScreenBlood", "화면 혈흔 강도");
            EditorGUILayout.HelpBox("유혈 연출은 게임 설정과 개인 혈흔 설정이 모두 켜져 있을 때만 표시합니다.", MessageType.None);
            so.ApplyModifiedProperties();
        }

        // 단계 알림: 입력하면 문구 표 pursuit.stage.<번호> 줄에 바로 저장한다. 비우면 설정 에셋의 기본 문구를 쓴다.
        void DrawStageAnnouncement(SerializedProperty fallback)
        {
            GUILayout.Label("시스템 알림 텍스트 (단계 시작 시 오른쪽 알림)");
            TextRow(PursuitTexts.StageKey(stageIndex), "알림 문구", fallback.stringValue);
        }

        // 추격 화면에 나오는 공통 문구. 모두 문구 표(WaitingRoomTexts.txt)에 바로 저장한다.
        void DrawScreenTexts()
        {
            EditorGUILayout.Space(14); GUILayout.Label("추격 화면 문구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("입력 후 Enter 또는 다른 칸을 누르면 WaitingRoomTexts.txt에 저장됩니다. 플레이 중이면 다음 표시부터 바로 반영됩니다.\n줄바꿈은 \\n 으로 씁니다. 칸을 비우면 코드 기본 문구로 돌아갑니다.", MessageType.None);
            TextRow(PursuitTexts.AlertTitleKey, "단계 알림 제목", "추격 경보");
            GUILayout.Label("추격 퀘스트 (추격 시간 동안 오른쪽)", EditorStyles.miniBoldLabel);
            TextRow("pursuit.quest.title", "퀘스트 제목", "추격자를 피해 달아나세요.");
            TextRow("pursuit.quest.stage", "공연장 입구 줄", "- 모든 플레이어가 공연장 입구로 가서 공연 진행");
            TextRow("pursuit.quest.escape", "탈출로 줄", "- 모든 플레이어가 탈출로로 진입해 탈출 시도");
            EditorGUILayout.HelpBox("공연장 입구 줄은 공연이 없는 마지막 아침에는 빠집니다. 탈출로 줄은 이번 런에서 누구든 탈출로 입구(B1)에 들어간 뒤부터 모두에게 붙습니다.", MessageType.None);
            GUILayout.Label("포획 알림 (잡힌 사람 화면)", EditorStyles.miniBoldLabel);
            TextRow("pursuit.captured-title", "포획 제목", "포획");
            TextRow("pursuit.captured", "포획 본문", "마네킹에게 잡혔습니다. 다음 장소로 이동할 때까지 관전합니다.");
            if (GUILayout.Button("문구 표 파일 열기 (WaitingRoomTexts.txt)")) {
                var file = AssetDatabase.LoadAssetAtPath<TextAsset>(PursuitTextTable.AssetPath);
                if (file != null) AssetDatabase.OpenAsset(file);
            }
        }

        static void TextRow(string key, string label, string fallback)
        {
            string raw = PursuitTextTable.Raw(key);
            string shown = raw ?? (fallback ?? "").Replace("\n", "\\n");
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.DelayedTextField(new GUIContent(label, $"문구 표 키: {key}"), shown);
            if (EditorGUI.EndChangeCheck() && next != shown) PursuitTextTable.Set(key, next);
            if (raw == null) EditorGUILayout.LabelField(" ", "기본 문구 사용 중 (입력하면 문구 표에 저장)", EditorStyles.miniLabel);
        }
    }
}
