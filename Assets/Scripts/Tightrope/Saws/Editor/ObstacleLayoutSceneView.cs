using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws.EditorTools
{
    /// <summary>
    /// 씬 뷰에 장애물 배치를 그리고 가로 톱날을 끌어 옮기게 한다(2026-10-05 Task-021).
    /// 장애물 배치 창이 열려 있거나 배치 파일·외줄 세팅을 선택했을 때, 코스(<see cref="TightropeCourse"/>)가 있는 씬에서만 그린다.
    /// 옮기면 배치 파일에 바로 기록하고(되돌리기 지원) 끌기를 놓을 때 저장한다.
    /// </summary>
    [InitializeOnLoad]
    static class ObstacleLayoutSceneView
    {
        static readonly Color TravelColor = new(1f, 0.6f, 0.1f, 0.9f);
        static readonly Color DiscColor = new(1f, 0.35f, 0.1f, 0.35f);
        static readonly Color HandleColor = new(1f, 0.85f, 0.2f, 1f);
        static readonly Color VerticalColor = new(0.9f, 0.15f, 0.15f, 0.9f);
        static readonly Color LeaderColor = new(0.2f, 0.85f, 1f, 1f);

        static bool savePending;
        static GUIStyle labelStyle;

        static ObstacleLayoutSceneView() => SceneView.duringSceneGui += OnSceneGUI;

        /// <summary>지금 그릴 배치 파일. 없으면 null(그리지 않음).</summary>
        static ObstacleLayout ActiveLayout
        {
            get
            {
                if (ObstacleLayoutWindow.Current != null) return ObstacleLayoutWindow.Current.Layout;
                if (Selection.activeObject is ObstacleLayout selected) return selected;
                if (Selection.activeObject is TightropeSettings) return GameSettings.Tightrope.Obstacles;
                return null;
            }
        }

        static void OnSceneGUI(SceneView view)
        {
            var layout = ActiveLayout;
            if (layout == null) return;
            var course = TightropeCourse.Current;
            if (course == null) return;

            labelStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, fontSize = 11 };
            var saws = GameSettings.Tightrope.Saws;
            var runtime = Application.isPlaying ? TightropeSaws.Current : null;

            DrawHorizontal(layout, course, saws, runtime);
            DrawVerticalMarkers(course, saws);
            if (runtime == null && ObstacleLayoutPreview.Enabled) DrawVerticalPreview(layout, course, saws);

            if (savePending && GUIUtility.hotControl == 0)
            {
                savePending = false;
                AssetDatabase.SaveAssetIfDirty(layout);
            }
        }

        static void DrawHorizontal(ObstacleLayout layout, TightropeCourse course, SawSettings saws, TightropeSaws runtime)
        {
            Vector3 origin = course.transform.position, forward = course.Forward, right = course.Right;
            float halfWidth = saws.horizontalTravelHalfWidth, radius = saws.horizontalVisualDiameter * 0.5f;
            float previewTime = ObstacleLayoutPreview.Enabled ? ObstacleLayoutPreview.Time : -1f;

            for (int i = 0; i < layout.HorizontalCount; i++)
            {
                float distance = layout.GetHorizontalDistance(i);
                Vector3 line = origin + forward * distance + Vector3.up * saws.horizontalAttackTop;

                // 왕복 범위·공격 높이
                Handles.color = TravelColor;
                Handles.DrawLine(line - right * halfWidth, line + right * halfWidth, 2f);

                // 톱날(플레이 중이면 실제 위치, 아니면 미리보기 시각의 위치)
                Vector3 center = runtime != null && i < runtime.HorizontalCount
                    ? runtime.GetHorizontalPosition(i)
                    : origin + forward * distance
                      + right * layout.GetHorizontalSideAt(i, saws, previewTime, ObstacleLayoutPreview.PerformanceStartedAt)
                      + Vector3.up * saws.HorizontalCenterHeight;
                Handles.color = DiscColor;
                Handles.DrawSolidDisc(center, Vector3.up, radius);
                Handles.color = TravelColor;
                Handles.DrawWireDisc(center, Vector3.up, radius, 2f);

                Handles.Label(line + Vector3.up * 0.7f, $"#{i + 1} · {distance:0.0}m · {layout.GetHorizontalSpeed(i, saws):0.0}m/s", labelStyle);

                // 끌어 옮기기(진행 방향만)
                Handles.color = HandleColor;
                float size = HandleUtility.GetHandleSize(line) * 0.12f;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(line, forward, size, Handles.SphereHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    float next = Mathf.Clamp(Mathf.Round(Vector3.Dot(moved - origin, forward) * 10f) / 10f, 0f, course.FinishDistance);
                    Undo.RecordObject(layout, "가로 톱날 옮기기");
                    layout.horizontalSaws[i].distance = next;
                    EditorUtility.SetDirty(layout);
                    layout.NotifyChanged();
                    savePending = true;
                }
            }
        }

        static void DrawVerticalMarkers(TightropeCourse course, SawSettings saws)
        {
            DrawCrossLine(course, saws.verticalSpawnDistance, $"수직 생성 한계 {saws.verticalSpawnDistance:0.#}m");
            DrawCrossLine(course, saws.verticalEndDistance, $"수직 제거 {saws.verticalEndDistance:0.#}m");
        }

        static void DrawCrossLine(TightropeCourse course, float distance, string label)
        {
            int lanes = course.LaneCount;
            if (lanes <= 0) return;
            Vector3 a = course.GetLanePosition(0, distance), b = course.GetLanePosition(lanes - 1, distance);
            Handles.color = VerticalColor;
            Handles.DrawDottedLine(a - course.Right, b + course.Right, 4f);
            Handles.Label(b + course.Right * 1.2f + Vector3.up * 0.3f, label, labelStyle);
        }

        static void DrawVerticalPreview(ObstacleLayout layout, TightropeCourse course, SawSettings saws)
        {
            float time = ObstacleLayoutPreview.Time;
            int lane = PreviewLane(layout, course);

            float leader = ObstacleLayoutPreview.GetVirtualLeader(time);
            if (!float.IsNaN(leader))
            {
                Vector3 at = course.GetLanePosition(lane, leader);
                Handles.color = LeaderColor;
                Handles.SphereHandleCap(0, at + Vector3.up * 0.75f, Quaternion.identity, 0.35f, EventType.Repaint);
                Handles.Label(at + Vector3.up * 1.3f, $"가상 선두 {leader:0.0}m", labelStyle);
            }

            if (!ObstacleLayoutPreview.TryGetVertical(layout, time, out var appearance, out _)) return;
            float sinceSpawn = time - appearance.spawnedAt;
            float distance = saws.GetVerticalDistance(appearance.spawnDistance, sinceSpawn);
            float drop = saws.GetVerticalDropOffset(sinceSpawn);
            Vector3 center = course.GetLanePosition(lane, distance) + Vector3.up * (saws.verticalCenterHeight + drop);
            float radius = saws.verticalVisualDiameter * 0.5f;
            Handles.color = new Color(VerticalColor.r, VerticalColor.g, VerticalColor.b, 0.35f);
            Handles.DrawSolidDisc(center, course.Right, radius);
            Handles.color = VerticalColor;
            Handles.DrawWireDisc(center, course.Right, radius, 2f);
            string state = drop > 0f ? "내려오는 중" : $"{distance:0.0}m";
            string contact = float.IsNaN(appearance.contactAt) ? "" : $" · 선두와 만남 {appearance.contactAt - appearance.spawnedAt:0.0}초 뒤";
            Handles.Label(center + Vector3.up * (radius + 0.4f), $"수직 #{appearance.serial} · {state}{contact}", labelStyle);
        }

        /// <summary>미리보기에 쓰는 줄: 정한 줄, 랜덤이면 0번 줄.</summary>
        public static int PreviewLane(ObstacleLayout layout, TightropeCourse course) =>
            Mathf.Clamp(layout.vertical != null && layout.vertical.lane >= 0 ? layout.vertical.lane : 0, 0, Mathf.Max(0, course.LaneCount - 1));
    }
}
