using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.UI.ScheduleV1;
using CurtainCall.WaitingRoom;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>기존 편집 UI를 판 위 월드 캔버스에 붙인다. 화면 팝업이나 별도 읽기 전용 사본은 만들지 않는다.</summary>
    public sealed partial class WaitingRoomScheduleUI
    {
        static readonly Vector2 PanelSize = new(1056f, 552f), StageSize = new(1920f, 1080f);
        const float PanelCenterOffset = 8f, BoardFill = 1f, FaceOffset = .04f;
        Transform boardDisplay;
        SchedulePanelView boardPanel;
        ScheduleBoardView displayedBoard;
        Canvas sourceCanvas, worldCanvas;
        GraphicRaycaster boardRaycaster;
        string lastBoardKey;
        SchedulePanelView templateView;
        Button[] templateEdits;
        Button templateConfirm, templateReady;
        Button[,] templateTiers;

        void TickBoardDisplay()
        {
            var schedule = ScheduleBoard.Instance;
            if (boardView == null || !boardView.isActiveAndEnabled || schedule == null) return;
            if (displayedBoard != boardView || boardDisplay == null) BuildBoardDisplay(boardView);
            if (boardDisplay == null) return;
            worldCanvas.worldCamera = Camera.main;
            bool editing = boardView.IsOpen;
            string key = $"{schedule.Book.Revision}|{schedule.HasAuthority}|{CampaignRun.Instance?.State.Day}|{CampaignRun.Instance?.State.Phase}|{CampaignRun.Instance?.State.PerformanceIndex}|{CampaignRun.Instance?.State.ReadyCount}|{CampaignRun.Instance?.State.RequiredCount}|{CampaignRun.Instance?.State.TrickId}|{CampaignRun.Instance?.State.Difficulty}|{schedule.MaxTier("Tightrope")}|{schedule.MaxTier("HumanCannon")}|{message}|{editing}|{session}";
            if (lastBoardKey != key || !view.IsVisible)
            {
                nextRefresh = Time.unscaledTime + .15f;
                lastBoardKey = key;
                var model = Build();
                var fallback = GetComponentInParent<UiRuntimeFontFallback>();
                if (fallback == null) fallback = FindFirstObjectByType<UiRuntimeFontFallback>();
                var text = new System.Text.StringBuilder(model.Title).Append(model.Context).Append(model.Footer);
                foreach (var day in model.Days) { text.Append(day.Label); foreach (var entry in day.Entries) text.Append(entry.Title).Append(entry.Metadata).Append(entry.ProgressLabel); }
                fallback?.WarmUpLocalText(boardDisplay, text.ToString());
                if (!view.IsVisible || !view.SetPresentation(model)) view.Open(model, false);
                HideConfirmationStamps();
                RefreshControls();
            }
            boardRaycaster.enabled = editing;
            foreach (var group in view.GetComponentsInChildren<CanvasGroup>(true))
            { group.interactable = editing; group.blocksRaycasts = editing; }
        }

        void HideConfirmationStamps()
        {
            foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (text.name == "30_UnconfirmedTMP" || text.name == "20_ConfirmedTMP")
                    text.transform.parent.gameObject.SetActive(false);
                if ((text.name == "00_TitleTMP" || text.name == "10_MetadataTMP") && text.GetComponentInParent<ScheduleCardVisual>() != null)
                {
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 272);
                    if (text.name == "10_MetadataTMP") { text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 18; }
                }
            }
        }

        void BuildBoardDisplay(ScheduleBoardView target)
        {
            ReleaseBoardDisplay();
            sourceCanvas = GetComponent<Canvas>();
            var face = target.VisualRoot;
            if (sourceCanvas == null || face == null) return;
            // 원본 캔버스에는 어댑터를 남겨 공용 UI의 수명·해제 처리를 그대로 따른다.
            sourceCanvas.enabled = false;
            var root = new GameObject("ScheduleBoardWorldUI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.layer = gameObject.layer;
            boardDisplay = root.transform;
            boardDisplay.SetParent(target.transform, false);
            worldCanvas = root.GetComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = Camera.main;
            boardRaycaster = root.GetComponent<GraphicRaycaster>();
            boardRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            boardRaycaster.enabled = false;
            root.SetActive(false);
            templateView = view; templateEdits = editButtons;
            templateConfirm = confirmButton; templateReady = readyButton; templateTiers = tierButtons;
            templateView.Closed -= Closed;
            templateView.Hide();
            var clone = Instantiate(templateView.gameObject, boardDisplay, false);
            clone.name = "BoardScheduleView";
            view = clone.GetComponent<SchedulePanelView>();
            Button Map(Button button) => button == null ? null : clone.transform.Find(RelativePath(templateView.transform, button.transform)).GetComponent<Button>();
            editButtons = templateEdits.Select(Map).ToArray();
            confirmButton = Map(templateConfirm); readyButton = Map(templateReady);
            tierButtons = templateTiers == null ? null : new Button[templateTiers.GetLength(0), templateTiers.GetLength(1)];
            if (tierButtons != null)
                for (int i = 0; i < tierButtons.GetLength(0); i++)
                    for (int t = 0; t < tierButtons.GetLength(1); t++) tierButtons[i, t] = Map(templateTiers[i, t]);
            foreach (var button in editButtons) button.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners(); readyButton.onClick.RemoveAllListeners();
            if (tierButtons != null) foreach (var button in tierButtons) button.onClick.RemoveAllListeners();
            root.SetActive(true);
            OnEnable();
            Vector3 normal = face.forward;
            if (target.CloseUpPose != null && Vector3.Dot(target.CloseUpPose.position - face.position, normal) < 0f) normal = -normal;
            var renderer = face.GetComponent<Renderer>();
            var bounds = renderer != null ? renderer.bounds : new Bounds(face.position, Vector3.one);
            float width = Mathf.Max(.1f, face.lossyScale.x), height = width * PanelSize.y / PanelSize.x;
            if (renderer != null)
            {
                var local = renderer.localBounds;
                width = Mathf.Max(.1f, local.size.x * renderer.transform.TransformVector(Vector3.right).magnitude);
                height = Mathf.Max(.1f, local.size.y * renderer.transform.TransformVector(Vector3.up).magnitude);
            }
            // 글자가 늘어나지 않도록 가로·세로 같은 배율로 키우고(더 좁은 축 기준),
            // 남는 축은 패널 배경을 판 끝까지 늘려 채운다(2026-11-04 사용자).
            float scale = Mathf.Min(width / PanelSize.x, height / PanelSize.y) * BoardFill;
            FitPanelToBoard(width / scale - PanelSize.x, height / scale - PanelSize.y);
            var rotation = Quaternion.LookRotation(-normal, Vector3.up);
            var rect = (RectTransform)boardDisplay;
            rect.sizeDelta = StageSize;
            rect.pivot = new Vector2(.5f, .5f);
            rect.SetPositionAndRotation(bounds.center + normal * FaceOffset - rotation * (Vector3.up * PanelCenterOffset * scale), rotation);
            var parentScale = target.transform.lossyScale;
            rect.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
            displayedBoard = target; boardPanel = view; lastBoardKey = null;
        }

        /// <summary>
        /// 판 사본의 패널만 판 비율로 넓힌다(공용 프리팹은 그대로). 배경·테두리는 늘어나고,
        /// 제목줄은 위, 버튼줄은 아래에 붙이며 종이는 남는 높이만큼 길어지고 일차 칸은 그 가운데에 둔다.
        /// </summary>
        void FitPanelToBoard(float extraX, float extraY)
        {
            extraX = Mathf.Max(0f, extraX); extraY = Mathf.Max(0f, extraY);
            if (extraX < .5f && extraY < .5f) return;
            var panel = view.transform.Find("00_Visuals/10_LayoutStage/00_SchedulePanel") as RectTransform;
            var content = panel != null ? panel.Find("40_Content") : null;
            if (content == null) { Debug.LogWarning("[ScheduleUI] 판 비율 맞춤 대상이 없어 기본 배치를 씁니다."); return; }
            // 패널 가운데는 그대로 두고 크기만 키운다(왼쪽 위 기준점).
            panel.anchoredPosition += new Vector2(-extraX * .5f, extraY * .5f);
            panel.sizeDelta += new Vector2(extraX, extraY);
            foreach (RectTransform item in content)
            {
                float down = item.name switch
                {
                    "10_Paper" => 0f,
                    "20_Day" or "21_Day" or "22_Day" => extraY * .5f,
                    "50_FooterDivider" or "60_Close" or "61_FooterTMP" => extraY,
                    _ => 0f,
                };
                item.anchoredPosition += new Vector2(extraX * .5f, -down);
                if (item.name == "10_Paper") item.sizeDelta += new Vector2(0f, extraY);
            }
            // 확정·준비 버튼은 설치기가 패널 밖(10_LayoutStage)에 붙여 둔 버튼이다. 버튼줄과 함께 내린다.
            foreach (RectTransform item in panel.parent)
                if (item != panel) item.anchoredPosition += new Vector2(extraX * .5f, -extraY * .5f);
        }

        static string RelativePath(Transform ancestor, Transform child)
        {
            var parts = new System.Collections.Generic.List<string>();
            while (child != null && child != ancestor) { parts.Add(child.name); child = child.parent; }
            if (child == null) throw new System.InvalidOperationException("Schedule controls must belong to their view.");
            parts.Reverse(); return string.Join("/", parts);
        }

        void ReleaseBoardDisplay()
        {
            // 비활성화/씬 해제 콜백에서는 부모를 바꾸지 않는다. 사본만 파괴하고 원본 참조를 복원한다.
            if (templateView != null)
            {
                if (view != null) view.Closed -= Closed;
                view = templateView; editButtons = templateEdits; confirmButton = templateConfirm; readyButton = templateReady; tierButtons = templateTiers;
                templateView = null;
            }
            if (boardDisplay != null) Destroy(boardDisplay.gameObject);
            boardDisplay = null; boardPanel = null; worldCanvas = null; boardRaycaster = null; displayedBoard = null;
        }
    }
}
