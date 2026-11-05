using CurtainCall.UI.SceneIntegration;
using CurtainCall.UI.TooltipsV1;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 화면 오른쪽에 띄우는 툴팁 정본 패널 하나(2026-10-31). 오늘 할 일([tutorial])과 기물 파괴 알림이 같이 쓴다.
    /// 왼쪽 위 기준 <see cref="Top"/> 높이에, 화면 오른쪽 끝에서 여백만큼 띄워 놓는다. 화면 비율에 따라 UI 너비가 1920보다 좁을 수 있어 실제 캔버스 너비를 쓴다.
    /// </summary>
    sealed class WaitingRoomRightPanel
    {
        const float Margin = 36f;
        const float StackGap = 8f;
        static readonly System.Collections.Generic.List<WaitingRoomRightPanel> shown = new();

        readonly Transform owner;
        readonly string name;
        float width;
        readonly string settingsPrefix;
        float margin = Margin, titleSize, bodySize;
        bool configured;
        float minimumTop;
        public void SetMinimumTop(float value)
        { if (Mathf.Approximately(minimumTop, value)) return; minimumTop = value; Layout(); }
        GameObject root;
        TooltipView view;
        UiRuntimeFontFallback fontOwner;
        TMPro.TMP_Text titleLabel;
        Color normalTitleColor;

        public bool GuideStyle { get; set; }
        public float Top { get; private set; }
        public bool Visible { get; private set; }
        public string Title { get; private set; } = "";
        public string Body { get; private set; } = "";

        public WaitingRoomRightPanel(Transform owner, string name, float top, float width, string settingsPrefix = null)
        { this.owner = owner; this.name = name; Top = top; this.width = width; this.settingsPrefix = settingsPrefix; }

        /// <summary>띄운다(같은 내용이면 다시 그리지 않는다). 아직 그릴 수 없으면 false.</summary>
        public bool Show(string title, string body)
        {
            Configure();
            // Reusing a panel for a different quest is a new entry; progress-only updates retain its order.
            bool newQuest = !Visible || title != Title;
            // 공용 UI 씬이 다시 올라오면 앞 폰트 보완본은 지워진다(공연 다녀옴). 새 보완본으로 다시 만든다.
            var current = FontOwner();
            // Unity의 파괴된 오브젝트 == null은 이전 소유자와 새 null을 같게 취급한다.
            // 씬 해제 때 삭제된 임시 폰트를 재사용하지 않도록 관리 참조 자체를 비교한다.
            if (root != null && !ReferenceEquals(current, fontOwner)) { Object.Destroy(root); root = null; view = null; Visible = false; }
            if (Visible && title == Title && body == Body) { Layout(); return true; }
            if (view == null) Build();
            if (view == null) return false;
            float canvasWidth = root.transform is RectTransform rect && rect.rect.width > 0f ? rect.rect.width : 1920f;
            if (!view.ShowPanel(title, body, new Vector2(Mathf.Max(0f, canvasWidth - margin - width), Mathf.Max(Top, minimumTop)), width)) return false;
            Title = title; Body = body; Visible = true;
            if (newQuest) shown.Remove(this);
            if (!shown.Contains(this)) shown.Add(this);
            Layout();
            return true;
        }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
            if (view != null) view.Hide();
            shown.Remove(this);
            Layout();
        }

        // 신규로 쌓인 마지막 패널에만 느낌표. 내용/위치 갱신은 진입 애니메이션을 재시작하지 않는다.
        static void Layout()
        {
            shown.RemoveAll(p => !p.Visible || p.root == null || p.view == null);
            float start = float.PositiveInfinity;
            foreach (var panel in shown) start = Mathf.Min(start, panel.Top);
            float bottom = 0f;
            for (int i = 0; i < shown.Count; i++)
            {
                var panel = shown[i];
                float top = Mathf.Max(panel.minimumTop, i == 0 ? start : bottom + StackGap);
                float canvasWidth = ((RectTransform)panel.root.transform).rect.width;
                if (canvasWidth <= 0f) canvasWidth = 1920f;
                panel.view.SetPosition(new Vector2(Mathf.Max(0f, canvasWidth - panel.margin - panel.width), top));
                bottom = top + panel.view.PresentationSizeUi.y;
                if (panel.titleLabel == null) continue;
                panel.titleLabel.text = shown.Count > 1 && i == shown.Count - 1 ? "! " + panel.Title : panel.Title;
                panel.titleLabel.color = i == 0 && ColorUtility.TryParseHtmlString(WaitingRoomText.Get("guide.title-color", "#FFD5A0"), out var accent)
                    ? accent : panel.normalTitleColor;
            }
        }

        // 기존 패널은 기본값을 유지하고, 요청한 패널만 문구 표의 수치를 읽는다(플레이 재시작 시 반영).
        void Configure()
        {
            if (configured) return;
            configured = true;
            if (string.IsNullOrEmpty(settingsPrefix)) return;
            float Read(string key, float fallback, float min, float max) =>
                WaitingRoomTutorialPanelSettings.TryNumber(WaitingRoomText.Get(settingsPrefix + key, ""), out float value)
                    ? Mathf.Clamp(value, min, max) : fallback;
            Top = Read("top", Top, 0f, 900f);
            width = Read("width", width, 256f, 480f);
            margin = Read("right", Margin, 0f, 1700f);
            titleSize = Read("title-size", 0f, 0f, 32f);
            bodySize = Read("body-size", 0f, 0f, 30f);
        }

        void Build()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Tooltips/Tooltips");
            if (prefab == null) { Debug.LogError("[RightPanel] Tooltips 프리팹이 없습니다."); return; }
            root = Object.Instantiate(prefab, owner, false);
            root.name = name;
            var canvas = root.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = 340;
            ScheduleScreenHold.Track(canvas);
            view = root.GetComponentInChildren<TooltipView>(true);
            if (view == null) { Debug.LogError("[RightPanel] Tooltips 프리팹에 TooltipView가 없습니다."); return; }
            view.ResetForRuntime();
            fontOwner = FontOwner();
            if (fontOwner != null) fontOwner.ApplyTo(root.transform);
            foreach (var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (label.name == "60_TitleTMP") { titleLabel = label; normalTitleColor = label.color; }
                if (label.name == "60_TitleTMP" && titleSize > 0f) label.fontSize = Mathf.Max(18f, titleSize);
                else if (label.name == "70_DescriptionTMP" && bodySize > 0f) label.fontSize = Mathf.Max(16f, bodySize);
            }
            if (titleLabel != null) { titleLabel.enableAutoSizing = true; titleLabel.fontSizeMin = 18f; titleLabel.fontSizeMax = titleLabel.fontSize; }
            Canvas.ForceUpdateCanvases();
        }

        static UiRuntimeFontFallback FontOwner()
        {
            var ui = Object.FindFirstObjectByType<CommonWaitingRoomUI>();
            return ui != null ? ui.GetComponent<UiRuntimeFontFallback>() : null;
        }
    }
}
