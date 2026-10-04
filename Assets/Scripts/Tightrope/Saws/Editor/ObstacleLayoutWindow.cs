using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws.EditorTools
{
    /// <summary>
    /// 장애물 배치 창: 배치 파일 선택, 시간 슬라이더 미리보기(플레이 없이 그 시각의 톱날 위치를 씬 뷰에 표시). 2026-10-05 Task-021.
    /// 시간 칸 편집은 배치 파일 인스펙터(<see cref="ObstacleLayoutEditor"/>), 위치 편집은 씬 뷰(<see cref="ObstacleLayoutSceneView"/>).
    /// </summary>
    public sealed class ObstacleLayoutWindow : EditorWindow
    {
        [SerializeField] ObstacleLayout layout;

        bool playing;
        double lastTick;

        /// <summary>열려 있는 창. 없으면 null.</summary>
        public static ObstacleLayoutWindow Current { get; private set; }

        /// <summary>편집·미리보기할 배치 파일. 비어 있으면 외줄 세팅이 연결한 파일.</summary>
        public ObstacleLayout Layout => layout != null ? layout : layout = GameSettings.Tightrope.Obstacles;

        [MenuItem("Tools/CurtainCall/Obstacle Layout")]
        static void Open() => GetWindow<ObstacleLayoutWindow>("장애물 배치").Show();

        void OnEnable()
        {
            Current = this;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += HandleUndo;
            ObstacleLayoutPreview.Repaint();
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= HandleUndo;
            ObstacleLayoutPreview.Repaint();
        }

        void HandleUndo()
        {
            Repaint();
            ObstacleLayoutPreview.Repaint();
        }

        void Tick()
        {
            if (!playing) return;
            double now = EditorApplication.timeSinceStartup;
            float limit = GameSettings.Tightrope.Run.TimeLimit;
            ObstacleLayoutPreview.Time = Mathf.Min(limit, ObstacleLayoutPreview.Time + (float)(now - lastTick));
            lastTick = now;
            if (ObstacleLayoutPreview.Time >= limit) playing = false;
            Repaint();
            ObstacleLayoutPreview.Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "씬 뷰: 톱날 가운데의 노란 공을 끌면 위치가 바뀝니다(Ctrl+Z로 되돌리기). 이름표는 '#번호 · 거리 · 속도'.\n" +
                "시간 칸(움직이기 시작·멈춤·수직 등장)은 아래 '배치 파일 열기'로 인스펙터에서 고칩니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            layout = (ObstacleLayout)EditorGUILayout.ObjectField(new GUIContent("배치 파일", "비우면 외줄 세팅이 연결한 파일을 씁니다."), Layout, typeof(ObstacleLayout), false);
            if (EditorGUI.EndChangeCheck()) ObstacleLayoutPreview.Repaint();
            var current = Layout;
            if (current == null) return;
            if (GUILayout.Button("배치 파일 열기 (인스펙터에서 시간 칸 편집)")) Selection.activeObject = current;

            if (TightropeCourse.Current == null)
                EditorGUILayout.HelpBox("이 씬에 코스(TightropeCourse)가 없어 씬 뷰에 그릴 수 없습니다. 예: Assets/Scenes/Tests/TightropeSaws/TightropeSaws.unity", MessageType.Warning);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("플레이 중: 바꾼 값은 플레이를 멈춰도 남습니다. 위치는 바로, 톱날 개수·시간은 묘기 재시작 때 반영됩니다. 씬 뷰의 톱날은 실제 위치입니다.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("시간 미리보기 (플레이 없이)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            ObstacleLayoutPreview.Enabled = EditorGUILayout.Toggle(new GUIContent("미리보기 켜기", "끄면 톱날을 출발 쪽 끝(대기 위치)에 그립니다."), ObstacleLayoutPreview.Enabled);
            using (new EditorGUI.DisabledScope(!ObstacleLayoutPreview.Enabled))
            {
                ObstacleLayoutPreview.PerformanceStartedAt = EditorGUILayout.FloatField(
                    new GUIContent("묘기 시작 (가정, 초)", "게임 시작 후 처음 플레이어가 줄에 오르는 시각. 묘기 시작 기준 칸은 여기서부터 셉니다."),
                    ObstacleLayoutPreview.PerformanceStartedAt);
                float limit = GameSettings.Tightrope.Run.TimeLimit;
                ObstacleLayoutPreview.Time = EditorGUILayout.Slider(new GUIContent("게임 시각 (초)", $"게임 시작 후 지난 시간. 0~제한시간 {limit:0}초."), ObstacleLayoutPreview.Time, 0f, limit);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? "⏸ 멈춤" : "▶ 재생"))
                    {
                        playing = !playing;
                        lastTick = EditorApplication.timeSinceStartup;
                    }
                    if (GUILayout.Button("⏮ 처음으로"))
                    {
                        playing = false;
                        ObstacleLayoutPreview.Time = 0f;
                    }
                }
            }
            if (EditorGUI.EndChangeCheck()) ObstacleLayoutPreview.Repaint();

            if (!ObstacleLayoutPreview.Enabled) return;
            DrawStatus(current);
        }

        void DrawStatus(ObstacleLayout current)
        {
            float time = ObstacleLayoutPreview.Time;
            var saws = GameSettings.Tightrope.Saws;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"게임 {Format(time)}", EditorStyles.boldLabel);

            float leader = ObstacleLayoutPreview.GetVirtualLeader(time);
            EditorGUILayout.LabelField("가상 선두", float.IsNaN(leader) ? "묘기 시작 전" : $"{leader:0.0}m (줄 위 {ObstacleLayoutPreview.WalkSpeed:0.##}m/s로 계속 걷는다고 가정)");

            if (ObstacleLayoutPreview.TryGetVertical(current, time, out var appearance, out string note))
            {
                string contact = float.IsNaN(appearance.contactAt)
                    ? "가상 선두와 만나기 전에 사라짐"
                    : $"가상 선두와 만남 {Format(appearance.contactAt)} (등장 후 {appearance.contactAt - appearance.spawnedAt:0.0}초)";
                EditorGUILayout.HelpBox(
                    $"수직 톱날 #{appearance.serial}: {Format(appearance.spawnedAt)}에 {appearance.spawnDistance:0.0}m에서 등장 → {Format(appearance.removedAt)}에 사라짐\n{contact}",
                    MessageType.None);
            }
            else EditorGUILayout.HelpBox($"수직 톱날: {note}", MessageType.None);

            EditorGUILayout.LabelField("닿기까지 (배치 파일 기준)", $"약 {current.EstimateVerticalContactTime(saws, ObstacleLayoutPreview.WalkSpeed):0.0}초");
            EditorGUILayout.HelpBox(
                "가정: 수직 톱날의 줄이 랜덤이면 미리보기는 0번 줄에 그립니다. 실제 게임에서는 진행 중인 플레이어가 있는 줄 중 랜덤이고, 그 줄 선두 앞에 생깁니다.",
                MessageType.None);
        }

        static string Format(float seconds) => $"{Mathf.FloorToInt(seconds / 60f)}:{seconds % 60f:00.0} ({seconds:0.0}초)";
    }
}
