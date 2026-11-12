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
        string entered = "", word = "";
        int codeLength = 4;
        bool terminal;
        TMP_Text wordLabel;
        const int MaxWord = 8;
        static readonly Color32 TerminalGreen = new(120, 255, 140, 255), TerminalRed = new(255, 70, 60, 255);
        public string Word => word;
        public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;
        public string Entered => entered;

        public void Open(string heading, string text, bool keypad, Action<string> onSubmit, Action onClose, bool overview = false, Texture image = null, bool reading = false)
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            submit = onSubmit; close = onClose; entered = ""; word = ""; codeLength = 4; terminal = false; wordLabel = null;
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Roles/Retained/BasicPanels/BasicPanels");
            if (prefab == null) throw new InvalidOperationException("BasicPanels prefab missing");
            canvasRoot = new GameObject("ExplorationPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 360;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var template = prefab.transform.Find("60_Panels/Menu_C13");
            var surface = Instantiate(template.gameObject, canvasRoot.transform, false); surface.name = "ExplorationPanel"; surface.SetActive(true);
            var rect = (RectTransform)surface.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            // reading: 노트처럼 글귀가 긴 단서. 아래쪽 판을 키워 한 쪽 분량이 들어가게 한다.
            rect.anchoredPosition = overview ? new Vector2(0, reading ? -290 : -400) : Vector2.zero;
            rect.sizeDelta = new Vector2(overview ? 1000 : 640, overview ? (reading ? 460 : 240) : keypad ? 660 : image != null ? 580 : 440);
            var content = surface.transform.Find("50_Content");
            var cr = (RectTransform)content; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            // The panel's visual layers stay; menu-specific controls do not belong to exploration.
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            var width = rect.sizeDelta.x - 80;
            Text(content, "Title", heading, font, 32, 40, -24, width, 50);
            message = Text(content, "Message", text, font, 22, 40, -88, width, overview ? (reading ? 288 : 68) : 100);
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
                Button(content, "Clear", WaitingRoomText.Get("gate.panel-clear", "지우기"), font, 115, -461, 130, 60, ClearCode);
                Button(content, "Submit", WaitingRoomText.Get("gate.panel-submit", "확인"), font, 395, -461, 130, 60, SubmitCode);
            }
            Button(content, "Close", WaitingRoomText.Get("gate.panel-close", "닫기"), font, (rect.sizeDelta.x - 200) * .5f, -(rect.sizeDelta.y - 76), 200, 56, () => close?.Invoke());
            panel = surface.GetComponent<CurtainCallPanelView>(); panel.Configure(rect, surface.GetComponent<CanvasGroup>(), true);
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            if (ui != null) ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>()?.ApplyTo(canvasRoot.transform);
        }
        /// <summary>
        /// 관리실 컴퓨터(대기실 최종본, 2026-11-12 사용자): 과거 공연 준비자가 해킹해 급히 만든 프로그램처럼 보이는 화면.
        /// 위 칸은 숫자, 아래 칸은 글자(최대 8자). 둘을 "숫자\n글자"로 묶어 한 번에 판정을 요청한다.
        /// 틀리면 붉은 Error를 띄우고 입력을 모두 지운다. 꾸밈 없는 판·단색 버튼만 쓴다.
        /// </summary>
        public void OpenTerminal(string heading, int digits, Action<string> onSubmit, Action onClose)
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            submit = onSubmit; close = onClose; entered = ""; word = ""; codeLength = Mathf.Max(1, digits); terminal = true;
            canvasRoot = new GameObject("ExplorationPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 360;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            const float w = 900, h = 640;
            var frame = Box(canvasRoot.transform, "TerminalFrame", TerminalGreen); var fr = (RectTransform)frame;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(.5f, .5f); fr.sizeDelta = new Vector2(w + 6, h + 6);
            var screen = Box(frame, "Terminal", new Color32(4, 12, 6, 250)); Place((RectTransform)screen, 3, -3, w, h);
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            TMP_Text Line(string name, string value, float y, float size, Color32 color)
            { var t = Text(screen, name, value, font, size, 28, y, w - 56, size + 14); t.color = color; t.textWrappingMode = TextWrappingModes.NoWrap; return t; }
            Line("Title", heading + " :: " + WaitingRoomText.Get("gate.terminal-title", "door_override.bat"), -16, 24, TerminalGreen);
            Place((RectTransform)Box(screen, "Rule", TerminalGreen), 28, -58, w - 56, 2);
            codeLabel = Line("Code", "", -84, 34, TerminalGreen);
            wordLabel = Line("Word", "", -140, 34, TerminalGreen);
            message = Line("Message", "", -198, 30, TerminalRed);
            // 자판: 숫자 한 줄 + 글자 세 줄. 일부러 줄마다 조금씩 어긋나게 놓는다.
            const float key = 72, gap = 8;
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            float[] indent = { 28, 28, 61, 113 };
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    string ch = rows[r][c].ToString();
                    Key(screen, "Key" + ch, ch, font, indent[r] + c * (key + gap), -262 - r * (56 + gap), key, 56, () => PressKey(ch));
                }
            Key(screen, "Clear", WaitingRoomText.Get("gate.terminal-clear", "CLR"), font, 700, -262 - 3 * (56 + gap), 172, 56, ClearCode);
            Key(screen, "Submit", WaitingRoomText.Get("gate.terminal-submit", "ENTER"), font, 28, -h + 76, 300, 56, SubmitCode);
            Key(screen, "Close", WaitingRoomText.Get("gate.terminal-close", "EXIT"), font, w - 228, -h + 76, 200, 56, () => close?.Invoke());
            RefreshCode();
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            if (ui != null) ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>()?.ApplyTo(canvasRoot.transform);
        }
        /// <summary>단말기 자판. 숫자는 위 칸, 글자는 아래 칸으로 들어간다.</summary>
        public void PressKey(string key)
        {
            if (key == null || key.Length != 1) return;
            if (key[0] >= '0' && key[0] <= '9') PressDigit(key);
            else if (terminal && key[0] >= 'A' && key[0] <= 'Z' && word.Length < MaxWord) { word += key; SetMessage(""); RefreshCode(); }
        }
        public void PressDigit(string digit)
        {
            if (digit == null || digit.Length != 1 || digit[0] < '0' || digit[0] > '9' || entered.Length >= codeLength) return;
            entered += digit; if (terminal) SetMessage(""); RefreshCode();
        }
        public void ClearCode() { entered = ""; word = ""; RefreshCode(); }
        public void SubmitCode()
        {
            if (!terminal) { if (ExplorationPuzzleRules.IsFourDigits(entered)) submit?.Invoke(entered); return; }
            // 덜 채운 입력은 판정 화면에 묻지 않고 바로 틀린 것으로 처리한다.
            if (entered.Length != codeLength || word.Length == 0) { SetMessage(ExplorationState.WrongCode); ClearCode(); return; }
            submit?.Invoke(entered + "\n" + word);
        }
        void RefreshCode()
        {
            if (codeLabel == null) return;
            if (!terminal) { codeLabel.text = entered.PadRight(4, '_'); return; }
            codeLabel.text = "> " + WaitingRoomText.Get("gate.terminal-code", "DATE") + " <mspace=.62em>[" + entered.PadRight(codeLength, '_') + "]</mspace>";
            if (wordLabel != null) wordLabel.text = "> " + WaitingRoomText.Get("gate.terminal-word", "CMD") + "  <mspace=.62em>[" + word.PadRight(MaxWord, '_') + "]</mspace>";
        }
        public void SetMessage(string text)
        {
            if (message == null) return;
            message.text = terminal && text == ExplorationState.WrongCode ? WaitingRoomText.Get("gate.terminal-error", "Error") : text;
        }
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
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Roles/Retained/CommonButtons/Button_General");
            var go = Instantiate(prefab, parent, false); go.name = name; Place((RectTransform)go.transform, x, y, w, h);
            var label = go.GetComponentInChildren<TMP_Text>(true); label.text = value; label.font = font;
            var button = go.GetComponent<UnityEngine.UI.Button>(); button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => callback());
        }
        static Transform Box(Transform parent, string name, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.transform;
        }
        static void Key(Transform parent, string name, string value, TMP_FontAsset font, float x, float y, float w, float h, Action callback)
        {
            var box = Box(parent, name, new Color32(16, 52, 24, 255)); Place((RectTransform)box, x, y, w, h);
            var label = Text(box, "Label", value, font, 26, 0, 0, w, h); label.alignment = TextAlignmentOptions.Center; label.color = TerminalGreen;
            box.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => callback());
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h); }
    }
}
