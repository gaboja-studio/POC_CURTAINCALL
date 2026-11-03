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
                GUILayout.Label("시스템 알림 텍스트");
                var message = stage.FindPropertyRelative("Announcement");
                message.stringValue = EditorGUILayout.TextArea(message.stringValue, GUILayout.MinHeight(60));
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("단계 복제")) {
                        stages.InsertArrayElementAtIndex(stageIndex); stageIndex++;
                        stages.GetArrayElementAtIndex(stageIndex).FindPropertyRelative("StartsAt").floatValue += 60;
                    }
                    using (new EditorGUI.DisabledScope(stages.arraySize <= 1))
                        if (GUILayout.Button("단계 제거")) { stages.DeleteArrayElementAtIndex(stageIndex); stageIndex = Mathf.Max(0, stageIndex - 1); }
                }
            }
            EditorGUILayout.Space(14); GUILayout.Label("단계별 전등 연출", EditorStyles.boldLabel);
            if (GUILayout.Button("전등 연출 창 열기")) PursuitLightingWindow.Open();
            EditorGUILayout.Space(10); GUILayout.Label("근접 빨간 아웃라인", EditorStyles.boldLabel);
            Field("OutlineDistance", "강조 거리 (m)"); Field("OutlinePixels", "외곽선 두께 (px)"); Field("OutlineColor", "외곽선 색상");
            EditorGUILayout.Space(10); GUILayout.Label("추격 화면 테두리", EditorStyles.boldLabel);
            Field("ScreenEffect", "효과 사용"); Field("EdgeIntensity", "최대 강도"); Field("EdgeWidth", "테두리 폭"); Field("PulseAmount", "맥동 강도");
            EditorGUILayout.HelpBox("내 캐릭터가 실제 추격 대상일 때만 표시합니다. 맥동 강도를 0으로 두면 깜빡임 없이 부드러운 테두리만 표시합니다.", MessageType.None);
            EditorGUILayout.Space(10); GUILayout.Label("원거리 렌더 / 포획 유혈", EditorStyles.boldLabel);
            Field("CullDistantRenderers", "먼 추격자 렌더 숨김"); Field("EncounterSeconds", "접근 예측 (최소 5초)"); Field("RenderSafetyMargin", "렌더 여유 거리 (m)");
            Field("CaptureBloodInterval", "혈액 분출 간격 (초)"); Field("CaptureBloodAmount", "분출 양"); Field("CaptureScreenBlood", "화면 혈흔 강도");
            EditorGUILayout.HelpBox("유혈 연출은 게임 설정과 개인 혈흔 설정이 모두 켜져 있을 때만 표시합니다.", MessageType.None);
            so.ApplyModifiedProperties();
        }
    }
}
