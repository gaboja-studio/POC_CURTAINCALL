using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CurtainCall.UI.PanelsV1;

namespace CurtainCall.WaitingRoom
{
    /// <summary>기존 BasicPanels 몸체와 CommonButtons를 사용하는 탐색 화면. 판정/내구도는 대상에 남긴다.</summary>
    public sealed class ExplorationPanel : MonoBehaviour
    {
        GameObject canvasRoot;
        CurtainCallPanelView panel;
        TMP_Text message, codeLabel;
        Action<string> submit;
        Action close;
        string entered = "";
        public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;
        public string Entered => entered;

        public void Open(string heading, string text, bool keypad, Action<string> onSubmit, Action onClose, bool overview = false, Texture image = null)
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            submit = onSubmit; close = onClose; entered = "";
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/BasicPanels/BasicPanels");
            if (prefab == null) throw new InvalidOperationException("BasicPanels prefab missing");
            canvasRoot = new GameObject("ExplorationPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 360;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var template = prefab.transform.Find("60_Panels/Menu_C13");
            var surface = Instantiate(template.gameObject, canvasRoot.transform, false); surface.name = "ExplorationPanel"; surface.SetActive(true);
            var rect = (RectTransform)surface.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = overview ? new Vector2(0, -400) : Vector2.zero;
            rect.sizeDelta = new Vector2(overview ? 1000 : 640, overview ? 240 : keypad ? 660 : image != null ? 580 : 440);
            var content = surface.transform.Find("50_Content");
            var cr = (RectTransform)content; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            // The panel's visual layers stay; menu-specific controls do not belong to exploration.
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            var width = rect.sizeDelta.x - 80;
            Text(content, "Title", heading, font, 32, 40, -24, width, 50);
            message = Text(content, "Message", text, font, 22, 40, -88, width, overview ? 68 : 100);
            if (image != null)
            {
                var picture = new GameObject("MonitorFeed", typeof(RectTransform), typeof(RawImage)); picture.transform.SetParent(content, false);
                Place((RectTransform)picture.transform, 40, -200, width, 280); picture.GetComponent<RawImage>().texture = image;
            }
            if (keypad)
            {
                codeLabel = Text(content, "Code", "_ _ _ _", font, 38, 40, -175, width, 52);
                codeLabel.alignment = TextAlignmentOptions.Center;
                for (int i = 0; i < 10; i++)
                {
                    var digit = i.ToString(); int row = i == 0 ? 3 : (i - 1) / 3, col = i == 0 ? 1 : (i - 1) % 3;
                    Button(content, "Digit" + i, digit, font, 115 + col * 140, -245 - row * 72, 130, 60, () => PressDigit(digit));
                }
                Button(content, "Clear", "지우기", font, 115, -461, 130, 60, ClearCode);
                Button(content, "Submit", "확인", font, 395, -461, 130, 60, SubmitCode);
            }
            Button(content, "Close", "닫기", font, (rect.sizeDelta.x - 200) * .5f, -(rect.sizeDelta.y - 76), 200, 56, () => close?.Invoke());
            panel = surface.GetComponent<CurtainCallPanelView>(); panel.Configure(rect, surface.GetComponent<CanvasGroup>(), true);
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            if (ui != null) ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>()?.ApplyTo(canvasRoot.transform);
        }
        public void PressDigit(string digit)
        {
            if (digit == null || digit.Length != 1 || digit[0] < '0' || digit[0] > '9' || entered.Length >= 4) return;
            entered += digit; RefreshCode();
        }
        public void ClearCode() { entered = ""; RefreshCode(); }
        public void SubmitCode() { if (ExplorationPuzzleRules.IsFourDigits(entered)) submit?.Invoke(entered); }
        void RefreshCode() { if (codeLabel != null) codeLabel.text = entered.PadRight(4, '_'); }
        public void SetMessage(string text) { if (message != null) message.text = text; }
        public void Hide() { if (canvasRoot != null) canvasRoot.SetActive(false); submit = null; close = null; }
        void OnDisable() => Hide();
        void OnDestroy() { if (canvasRoot != null) Destroy(canvasRoot); }
        static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, w, h);
            var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = font; text.fontSize = size;
            text.color = new Color32(238, 234, 215, 255); text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
        static void Button(Transform parent, string name, string value, TMP_FontAsset font, float x, float y, float w, float h, Action callback)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_General");
            var go = Instantiate(prefab, parent, false); go.name = name; Place((RectTransform)go.transform, x, y, w, h);
            var label = go.GetComponentInChildren<TMP_Text>(true); label.text = value; label.font = font;
            var button = go.GetComponent<UnityEngine.UI.Button>(); button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => callback());
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h); }
    }
}
