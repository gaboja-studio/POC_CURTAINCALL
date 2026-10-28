using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>프로젝트 설정 전용 5탭 뷰. 원본 C14 프리팹은 변경하지 않는다.</summary>
    public sealed class SettingsTabsView : MonoBehaviour
    {
        readonly string[] titles = { "게임", "그래픽", "조작", "사운드", "기타" };
        readonly List<GameObject>[] rows = { new(), new(), new(), new(), new() };
        readonly UnityEngine.UI.Button[] tabs = new UnityEngine.UI.Button[5];
        readonly RectTransform[] contents = new RectTransform[5];
        readonly UnityEngine.UI.ScrollRect[] scrolls = new UnityEngine.UI.ScrollRect[5];
        TMP_FontAsset font;
        TMP_Text message;
        CanvasGroup group;
        Action apply, cancel;
        GameObject previousSelected;
        public bool IsVisible => gameObject.activeSelf;
        public int CurrentPage { get; private set; }
        public UnityEngine.UI.Button ApplyButton { get; private set; }
        public UnityEngine.UI.Button CancelButton { get; private set; }
        public event Action<int> PageChanged;

        public void Initialize(TMP_FontAsset textFont)
        {
            font = textFont;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 315;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            var panel = Rect("Panel", transform, 0, 0, 1120, 820);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(26, 40, 36, 255);
            Text("Title", panel, "설정", 36, -24, 1048, 60, 36);
            for (int i = 0; i < tabs.Length; i++)
            {
                int page = i;
                tabs[i] = SettingsExtensionPanel.Button("Tab_" + i, panel, titles[i], 36 + i * 212, -100, 200, 56, font);
                tabs[i].onClick.AddListener(() => ShowPage(page));
                var scroll = Rect("Page_" + i, panel, 36, -178, 1048, 494);
                scrolls[i] = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                scrolls[i].horizontal = false; scrolls[i].scrollSensitivity = 40;
                scrolls[i].movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
                var viewport = Rect("Viewport", scroll, 0, 0, 1048, 494);
                viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(26, 40, 36, 255);
                viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
                contents[i] = Rect("Content", viewport, 0, 0, 1048, 0);
                var layout = contents[i].gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.spacing = 12; layout.padding = new RectOffset(4, 16, 6, 12);
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                contents[i].gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                scrolls[i].viewport = viewport; scrolls[i].content = contents[i];
                var barRect = Rect("Scrollbar", scroll, 1034, 0, 12, 494);
                barRect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(45, 61, 53, 255);
                var handle = Rect("Handle", barRect, 0, 0, 0, 0);
                handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
                var graphic = handle.gameObject.AddComponent<UnityEngine.UI.Image>(); graphic.color = new Color32(166, 174, 150, 255);
                var scrollbar = barRect.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
                scrollbar.handleRect = handle; scrollbar.targetGraphic = graphic;
                scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
                scrolls[i].verticalScrollbar = scrollbar;
                scrolls[i].verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            }
            message = Text("Message", panel, "", 36, -684, 1048, 38, 19);
            CancelButton = SettingsExtensionPanel.Button("50_Cancel", panel, "취소 / 뒤로", 36, -736, 300, 56, font);
            ApplyButton = SettingsExtensionPanel.Button("60_Apply", panel, "적용", 784, -736, 300, 56, font);
            CancelButton.onClick.AddListener(Cancel); ApplyButton.onClick.AddListener(RequestApply);
            gameObject.SetActive(false);
        }
        public void Open(bool gameplay, Action onApply, Action onCancel)
        {
            apply = onApply; cancel = onCancel;
            foreach (var page in rows) { foreach (var row in page) { row.SetActive(false); Destroy(row); } page.Clear(); }
            foreach (var scroll in scrolls) { scroll.StopMovement(); scroll.content.anchoredPosition = Vector2.zero; }
            previousSelected = null;
            tabs[0].interactable = gameplay;
            gameObject.SetActive(true); SetCovered(false); SetMessage("");
            ShowPage(gameplay ? 0 : 1);
        }
        public void ShowPage(int page)
        {
            if (page < 0 || page >= 5 || !tabs[page].interactable) return;
            CurrentPage = page;
            for (int i = 0; i < 5; i++)
            {
                scrolls[i].gameObject.SetActive(i == page);
                tabs[i].GetComponentInChildren<TMP_Text>().color = !tabs[i].interactable ? new Color32(128, 140, 132, 255)
                    : i == page ? new Color32(255, 214, 126, 255) : Color.white;
            }
            SetMessage(""); PageChanged?.Invoke(page);
        }
        public bool IsPageEnabled(int page) => tabs[page].interactable;
        public void SetCovered(bool covered)
        { group.alpha = covered ? 0 : 1; group.interactable = group.blocksRaycasts = !covered; }
        public void SetBusy(bool busy) { group.interactable = !busy; ApplyButton.interactable = !busy; }
        public void SetMessage(string text) => message.text = text;
        public void RequestApply() => apply?.Invoke();
        public void Cancel() => cancel?.Invoke();
        public void Hide()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            gameObject.SetActive(false); apply = cancel = null;
        }
        Transform Row(int page, string id, float height = 64)
        {
            var row = Rect(id, contents[page], 0, 0, 1020, height);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            rows[page].Add(row.gameObject); return row;
        }
        public UnityEngine.UI.Button ActionRow(int page, string id, string label, string actionLabel, Action action)
        {
            var row = Row(page, id); Text("Label", row, label, 0, 0, 510, 56, 24);
            var button = SettingsExtensionPanel.Button("Action", row, actionLabel, 520, 0, 490, 56, font);
            button.onClick.AddListener(() => action()); return button;
        }
        public void Paragraph(int page, string id, string text, float height = 110)
        {
            var row = Row(page, id, height);
            var label = Text("Text", row, text, 0, 0, 1010, height, 22);
            label.alignment = TextAlignmentOptions.TopLeft;
            float preferred = Mathf.Max(height, label.GetPreferredValues(text, 1010, 0).y + 12);
            row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = preferred;
            ((RectTransform)label.transform).sizeDelta = new Vector2(1010, preferred);
        }
        public UnityEngine.UI.Slider Slider(int page, string id, string label, float value, Action<float> change)
        {
            var row = Row(page, id); Text("Label", row, label, 0, 0, 490, 56, 24);
            var go = Rect("Slider", row, 520, -8, 390, 40);
            var background = go.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color32(65, 85, 75, 255);
            var slider = go.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.minValue = 0; slider.maxValue = 1;
            var area = Rect("HandleArea", go, 12, 0, 366, 40);
            var handle = Rect("Handle", area, 0, 0, 24, 0); handle.pivot = new Vector2(.5f, .5f);
            var graphic = handle.gameObject.AddComponent<UnityEngine.UI.Image>(); graphic.color = new Color32(240, 221, 165, 255);
            slider.handleRect = handle; slider.targetGraphic = graphic;
            var shown = Text("Value", row, "", 930, 0, 80, 56, 22);
            slider.SetValueWithoutNotify(value); shown.text = Mathf.RoundToInt(value * 100) + "%";
            slider.onValueChanged.AddListener(v => { shown.text = Mathf.RoundToInt(v * 100) + "%"; change(v); });
            return slider;
        }
        public UnityEngine.UI.Toggle Toggle(int page, string id, string label, bool value, Action<bool> change)
        {
            var row = Row(page, id); Text("Label", row, label, 0, 0, 730, 56, 24);
            var rect = Rect("Toggle", row, 790, -8, 48, 40);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color32(65, 85, 75, 255);
            var toggle = rect.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            var mark = Rect("Mark", rect, 7, -7, 34, 26).gameObject.AddComponent<UnityEngine.UI.Image>(); mark.color = new Color32(240, 221, 165, 255);
            toggle.targetGraphic = background; toggle.graphic = mark; toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v => change(v)); return toggle;
        }
        public Action<string, bool> Choice(int page, string id, string label, string value, Action<int> change, bool enabled = true)
        {
            var row = Row(page, id); Text("Label", row, label, 0, 0, 380, 56, 24);
            var shown = Text("Value", row, value, 468, 0, 460, 56, 22); shown.alignment = TextAlignmentOptions.Center;
            shown.overflowMode = TextOverflowModes.Ellipsis;
            var prev = SettingsExtensionPanel.Button("Previous", row, "<", 394, 0, 60, 56, font);
            var next = SettingsExtensionPanel.Button("Next", row, ">", 944, 0, 60, 56, font);
            prev.onClick.AddListener(() => change(-1)); next.onClick.AddListener(() => change(1));
            Action<string, bool> refresh = (text, active) => { shown.text = text; prev.interactable = next.interactable = active; };
            refresh(value, enabled); return refresh;
        }
        void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var content = contents[CurrentPage];
            if (selected == previousSelected) return;
            previousSelected = selected;
            if (selected == null || !selected.transform.IsChildOf(content)) return;
            var target = selected.GetComponent<RectTransform>(); if (target == null) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scrolls[CurrentPage].viewport, target);
            float top = 0, bottom = -scrolls[CurrentPage].viewport.rect.height;
            float delta = bounds.max.y > top ? top - bounds.max.y : bounds.min.y < bottom ? bottom - bounds.min.y : 0;
            if (Mathf.Abs(delta) > .1f) content.anchoredPosition += new Vector2(0, delta);
        }
        TMP_Text Text(string name, Transform parent, string text, float x, float y, float width, float height, float size)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSharedMaterial = font.material;
            label.fontSize = size; label.text = text; label.color = new Color32(240, 238, 220, 255);
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
    }
}
