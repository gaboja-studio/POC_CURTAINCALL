using System.Collections.Generic;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws.EditorTools
{
    /// <summary>
    /// 장애물 배치 파일 인스펙터: 쉬운 말 칸(○초에 움직임·○초 뒤 멈춤·다음 등장까지 ○초)과 "닿기까지 약 ○초" 표시. 2026-10-05 Task-021.
    /// </summary>
    [CustomEditor(typeof(ObstacleLayout))]
    public sealed class ObstacleLayoutEditor : UnityEditor.Editor
    {
        static readonly GUIContent[] SideNames = { new("왼쪽에서 출발"), new("오른쪽에서 출발") };
        static readonly GUIContent[] BaseNames = { new("게임 시작"), new("묘기 시작") };

        readonly HashSet<int> selected = new();
        SerializedProperty horizontal, vertical;

        // 한 번에 적용 칸
        ObstacleTimeBase bulkBase;
        float bulkDelay, bulkStopAfter = 10f;
        bool bulkStops;

        void OnEnable()
        {
            horizontal = serializedObject.FindProperty(nameof(ObstacleLayout.horizontalSaws));
            vertical = serializedObject.FindProperty(nameof(ObstacleLayout.vertical));
        }

        public override void OnInspectorGUI()
        {
            var layout = (ObstacleLayout)target;
            serializedObject.Update();

            if (GUILayout.Button("장애물 배치 창 열기 (씬 뷰 편집·시간 미리보기)"))
                EditorApplication.ExecuteMenuItem("Tools/CurtainCall/Obstacle Layout");
            EditorGUILayout.Space();

            DrawHorizontal(layout);
            EditorGUILayout.Space(12f);
            DrawVertical(layout);

            if (serializedObject.ApplyModifiedProperties()) SceneView.RepaintAll();
        }

        // ── 가로 톱날 ───────────────────────────────────────

        void DrawHorizontal(ObstacleLayout layout)
        {
            var saws = GameSettings.Tightrope.Saws;
            EditorGUILayout.LabelField($"가로 톱날 ({horizontal.arraySize}개)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("속도는 첫·마지막 톱날 거리 사이에서 자동으로 정해집니다(외줄 세팅 '톱날 공통 규격'). 위치는 씬 뷰에서 끌어도 바뀝니다.", MessageType.None);

            for (int i = 0; i < horizontal.arraySize; i++)
            {
                var entry = horizontal.GetArrayElementAtIndex(i);
                var distance = entry.FindPropertyRelative(nameof(HorizontalSawEntry.distance));
                var side = entry.FindPropertyRelative(nameof(HorizontalSawEntry.startSide));
                var startBase = entry.FindPropertyRelative(nameof(HorizontalSawEntry.startBase));
                var startDelay = entry.FindPropertyRelative(nameof(HorizontalSawEntry.startDelay));
                var stops = entry.FindPropertyRelative(nameof(HorizontalSawEntry.stops));
                var stopAfter = entry.FindPropertyRelative(nameof(HorizontalSawEntry.stopAfter));

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isSelected = EditorGUILayout.Toggle(selected.Contains(i), GUILayout.Width(16f));
                        if (isSelected) selected.Add(i); else selected.Remove(i);
                        EditorGUILayout.LabelField($"#{i + 1}", EditorStyles.boldLabel, GUILayout.Width(32f));
                        EditorGUIUtility.labelWidth = 34f;
                        distance.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("거리", "중심 위치(m, 외줄 시작점 기준)"), distance.floatValue, GUILayout.Width(90f)));
                        EditorGUIUtility.labelWidth = 0f;
                        EditorGUILayout.LabelField("m", GUILayout.Width(14f));
                        side.enumValueIndex = EditorGUILayout.Popup(side.enumValueIndex, SideNames);
                        if (i < layout.HorizontalCount)
                            EditorGUILayout.LabelField($"{layout.GetHorizontalSpeed(i, saws):0.0}m/s", GUILayout.Width(56f));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(52f);
                        EditorGUILayout.LabelField("움직이기 시작", GUILayout.Width(78f));
                        startBase.enumValueIndex = EditorGUILayout.Popup(startBase.enumValueIndex, BaseNames, GUILayout.Width(80f));
                        startDelay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(startDelay.floatValue, GUILayout.Width(44f)));
                        EditorGUILayout.LabelField("초 뒤", GUILayout.Width(32f));
                        stops.boolValue = EditorGUILayout.ToggleLeft("멈춤", stops.boolValue, GUILayout.Width(46f));
                        using (new EditorGUI.DisabledScope(!stops.boolValue))
                        {
                            stopAfter.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(stopAfter.floatValue, GUILayout.Width(44f)));
                            EditorGUILayout.LabelField(stops.boolValue ? "초 뒤" : "(안 멈춤)", GUILayout.Width(60f));
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("톱날 추가")) AddSaw(layout);
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                    if (GUILayout.Button($"선택 삭제 ({selected.Count})")) DeleteSelected();
                if (GUILayout.Button("거리 순 정렬")) SortByDistance(layout);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체 선택")) for (int i = 0; i < horizontal.arraySize; i++) selected.Add(i);
                if (GUILayout.Button("선택 해제")) selected.Clear();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("선택한 톱날에 시간 한 번에 적용", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("움직이기 시작", GUILayout.Width(78f));
                    bulkBase = (ObstacleTimeBase)EditorGUILayout.Popup((int)bulkBase, BaseNames, GUILayout.Width(80f));
                    bulkDelay = Mathf.Max(0f, EditorGUILayout.FloatField(bulkDelay, GUILayout.Width(44f)));
                    EditorGUILayout.LabelField("초 뒤", GUILayout.Width(32f));
                    bulkStops = EditorGUILayout.ToggleLeft("멈춤", bulkStops, GUILayout.Width(46f));
                    using (new EditorGUI.DisabledScope(!bulkStops))
                        bulkStopAfter = Mathf.Max(0f, EditorGUILayout.FloatField(bulkStopAfter, GUILayout.Width(44f)));
                }
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                    if (GUILayout.Button($"선택한 {selected.Count}개에 적용")) ApplyBulk();
            }
        }

        void AddSaw(ObstacleLayout layout)
        {
            float finish = GameSettings.Tightrope.Course.FinishDistance;
            int count = horizontal.arraySize;
            float last = count > 0 ? horizontal.GetArrayElementAtIndex(count - 1).FindPropertyRelative(nameof(HorizontalSawEntry.distance)).floatValue : 0f;
            horizontal.InsertArrayElementAtIndex(count);
            var entry = horizontal.GetArrayElementAtIndex(count);
            entry.FindPropertyRelative(nameof(HorizontalSawEntry.distance)).floatValue = Mathf.Min(finish, last + 5f);
            if (count > 0)
            {
                var side = entry.FindPropertyRelative(nameof(HorizontalSawEntry.startSide));
                side.enumValueIndex = 1 - side.enumValueIndex; // 앞 톱날과 반대쪽
            }
        }

        void DeleteSelected()
        {
            foreach (int i in selected.OrderByDescending(i => i))
                if (i < horizontal.arraySize) horizontal.DeleteArrayElementAtIndex(i);
            selected.Clear();
        }

        void SortByDistance(ObstacleLayout layout)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(layout, "가로 톱날 거리 순 정렬");
            layout.horizontalSaws = layout.horizontalSaws.OrderBy(s => s.distance).ToArray();
            EditorUtility.SetDirty(layout);
            layout.NotifyChanged();
            serializedObject.Update();
            selected.Clear();
        }

        void ApplyBulk()
        {
            foreach (int i in selected)
            {
                if (i >= horizontal.arraySize) continue;
                var entry = horizontal.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(nameof(HorizontalSawEntry.startBase)).enumValueIndex = (int)bulkBase;
                entry.FindPropertyRelative(nameof(HorizontalSawEntry.startDelay)).floatValue = bulkDelay;
                entry.FindPropertyRelative(nameof(HorizontalSawEntry.stops)).boolValue = bulkStops;
                entry.FindPropertyRelative(nameof(HorizontalSawEntry.stopAfter)).floatValue = bulkStopAfter;
            }
        }

        // ── 수직 톱날 ───────────────────────────────────────

        void DrawVertical(ObstacleLayout layout)
        {
            var saws = GameSettings.Tightrope.Saws;
            EditorGUILayout.LabelField("수직 톱날", EditorStyles.boldLabel);
            var enabled = vertical.FindPropertyRelative(nameof(VerticalSawRule.enabled));
            var firstDelay = vertical.FindPropertyRelative(nameof(VerticalSawRule.firstDelay));
            var nextDelay = vertical.FindPropertyRelative(nameof(VerticalSawRule.nextDelay));
            var maxCount = vertical.FindPropertyRelative(nameof(VerticalSawRule.maxCount));
            var lane = vertical.FindPropertyRelative(nameof(VerticalSawRule.lane));
            var minLead = vertical.FindPropertyRelative(nameof(VerticalSawRule.minLeadDistance));

            EditorGUIUtility.labelWidth = 200f;
            enabled.boolValue = EditorGUILayout.Toggle("수직 톱날 내보내기", enabled.boolValue);
            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                firstDelay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("첫 등장: 묘기 시작 후 (초)", "묘기 시작(처음 줄에 오른 때)부터 첫 수직 톱날까지."), firstDelay.floatValue));
                nextDelay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("다음 등장까지: 사라진 뒤 (초)", "앞 톱날이 사라지고(끝 거리 도착 또는 발판으로 내려감) 다음 톱날까지."), nextDelay.floatValue));
                maxCount.intValue = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("최대 등장 횟수 (0 = 계속)"), maxCount.intValue));

                int laneCount = GameSettings.Tightrope.Course.LaneCount;
                var laneNames = new GUIContent[laneCount + 1];
                laneNames[0] = new GUIContent("랜덤 (진행 중인 플레이어가 있는 줄)");
                for (int i = 0; i < laneCount; i++) laneNames[i + 1] = new GUIContent($"{i}번 줄 (왼쪽부터)");
                lane.intValue = EditorGUILayout.Popup(new GUIContent("등장 줄"), Mathf.Clamp(lane.intValue + 1, 0, laneCount), laneNames) - 1;

                minLead.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("선두 앞 최소 거리 (m)", $"그 줄의 가장 앞선 플레이어보다 이만큼 앞에 생깁니다. 생성 한계 {saws.verticalSpawnDistance:0.#}m를 넘으면 그 줄에는 안 나옵니다."), minLead.floatValue));
            }
            EditorGUIUtility.labelWidth = 0f;

            if (enabled.boolValue)
            {
                serializedObject.ApplyModifiedProperties();
                float walk = GameSettings.Tightrope.RopeMovement.MoveSpeed;
                EditorGUILayout.HelpBox(
                    $"플레이어에게 닿기까지 약 {layout.EstimateVerticalContactTime(saws, walk):0.0}초\n" +
                    $"= 화면 위에서 내려오는 {saws.verticalDropDuration:0.0#}초 + {layout.vertical.minLeadDistance:0.#}m ÷ (톱날 {saws.verticalSpeed:0.0#} + 걷기 {walk:0.0#} m/s)\n" +
                    $"내려오는 높이·시간, 톱날 속도, 생성 한계는 외줄 세팅 '톱날 공통 규격'에서 바꿉니다.",
                    MessageType.Info);
            }
        }
    }
}
