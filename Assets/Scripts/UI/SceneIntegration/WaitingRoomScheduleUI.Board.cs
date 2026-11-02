using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.UI.ScheduleV1;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 로비 스케줄표 판 앞면에 스케줄표 UI를 그대로 붙인다(2026-11-02 사용자: 종이 도형 대신 스케줄표 UI가 보이게, 확대 창과 별개).
    /// 클로즈업 창과 같은 <c>SchedulePanel</c> 프리팹을 월드 캔버스에 하나 더 두고, 같은 일정 자료로 계속 갱신한다. 판의 UI는 입력을 받지 않는다.
    /// </summary>
    public sealed partial class WaitingRoomScheduleUI
    {
        const string PanelPrefabPath = "Prefabs/UIs/ScheduleUi/SchedulePanel";
        /// <summary>패널 기준 크기(UI 단위, schedule-ui.md: 패널 1056×552).</summary>
        static readonly Vector2 PanelSize = new(1056f, 552f), StageSize = new(1920f, 1080f);
        /// <summary>1920×1080 무대 가운데에서 패널 가운데까지(UI 단위, 패널 위치 (432,272) 기준).</summary>
        const float PanelCenterOffset = 8f;
        /// <summary>판 가로에서 UI가 차지하는 비율과 판 앞면에서 띄우는 거리(m).</summary>
        const float BoardFill = .94f, FaceOffset = .04f;
        const string BoardSession = "lobby-board";

        Transform boardDisplay;
        SchedulePanelView boardPanel;
        ScheduleBoardView displayedBoard;
        float nextBoardRefresh;
        string lastBoardKey;

        void TickBoardDisplay()
        {
            var schedule = ScheduleBoard.Instance;
            if (boardView == null || !boardView.isActiveAndEnabled || schedule == null)
            {
                if (boardDisplay != null) boardDisplay.gameObject.SetActive(false);
                return;
            }
            if (displayedBoard != boardView || boardDisplay == null) BuildBoardDisplay(boardView);
            if (boardPanel == null) return;
            boardDisplay.gameObject.SetActive(true);
            if (Time.unscaledTime < nextBoardRefresh) return;
            nextBoardRefresh = Time.unscaledTime + .5f;
            var run = CampaignRun.Instance;
            string key = $"{schedule.Book.Revision}|{run?.State.Day}|{run?.State.Phase}|{run?.State.PerformanceIndex}|{run?.State.ReadyCount}|{message}";
            if (key == lastBoardKey && boardPanel.IsVisible) return;
            lastBoardKey = key;
            var source = Build();
            var shown = new SchedulePresentation(BoardSession, source.Revision, source.Title, source.Context, source.Footer, source.Days.ToArray());
            // 전용 정적 글꼴에 없는 글자(예: 제목의 "스케줄")는 게임 글꼴로 보완한다(확대 창과 같은 처리).
            var fallback = FindFirstObjectByType<UiRuntimeFontFallback>();
            if (fallback != null)
            {
                var text = new System.Text.StringBuilder(shown.Title).Append(shown.Context).Append(shown.Footer);
                foreach (var day in shown.Days) { text.Append(day.Label); foreach (var entry in day.Entries) text.Append(entry.Title).Append(entry.Metadata).Append(entry.ProgressLabel); }
                fallback.WarmUpLocalText(boardDisplay, text.ToString());
            }
            if (!boardPanel.IsVisible || !boardPanel.SetPresentation(shown)) boardPanel.Open(shown, false);
        }

        void BuildBoardDisplay(ScheduleBoardView target)
        {
            if (boardDisplay != null) Destroy(boardDisplay.gameObject);
            boardDisplay = null; boardPanel = null; displayedBoard = target; lastBoardKey = null;
            var prefab = Resources.Load<GameObject>(PanelPrefabPath);
            var face = target.VisualRoot;
            if (prefab == null || face == null) return;
            // 판 앞면 = 클로즈업 카메라가 있는 쪽.
            Vector3 normal = face.forward;
            var pose = target.CloseUpPose;
            if (pose != null && Vector3.Dot(pose.position - face.position, normal) < 0f) normal = -normal;
            var bounds = face.GetComponent<Renderer>() != null ? face.GetComponent<Renderer>().bounds : new Bounds(face.position, Vector3.one);
            float width = Vector3.Scale(face.lossyScale, Vector3.right).magnitude;
            if (width < .1f) width = 2f;
            float scale = width * BoardFill / PanelSize.x;

            // 프리팹 최상위가 화면 캔버스(1920×1080 기준)다. 그 캔버스를 그대로 월드 캔버스로 바꿔 판 앞면에 세운다.
            var root = Instantiate(prefab);
            root.name = "ScheduleBoardWorldUI";
            root.transform.SetParent(target.transform, true);
            if (root.TryGetComponent(out UnityEngine.UI.CanvasScaler scaler)) Destroy(scaler);
            if (root.TryGetComponent(out UnityEngine.UI.GraphicRaycaster raycaster)) Destroy(raycaster);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            var rect = (RectTransform)root.transform;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = StageSize;
            // 월드 UI는 캔버스 -Z 쪽에서 읽힌다. 앞면 바깥(normal)에서 보도록 캔버스 앞(+Z)을 판 안쪽으로 둔다.
            // 패널(1056×552)은 1920×1080 무대의 가운데에서 위로 8 UI 단위 떨어져 있다.
            var rotation = Quaternion.LookRotation(-normal, Vector3.up);
            rect.SetPositionAndRotation(bounds.center + normal * FaceOffset - rotation * (Vector3.up * PanelCenterOffset * scale), rotation);
            rect.localScale = Vector3.one * scale / Mathf.Max(.0001f, target.transform.lossyScale.x);
            boardPanel = root.GetComponentInChildren<SchedulePanelView>(true);
            // 판에는 닫기 버튼이 필요 없다.
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                if (button.name.ToLowerInvariant().Contains("close")) button.gameObject.SetActive(false);
            boardDisplay = root.transform;
        }
    }
}
