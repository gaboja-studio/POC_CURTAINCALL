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
        /// <summary>단말기 자판 화면이 떠 있어 실제 키보드 글자를 입력으로 받는 중(F 등 글자 키가 상호작용으로 쓰이지 않게).</summary>
        public bool Typing => terminal && codeLabel != null && IsVisible;
        int typingFrom;
        public string Entered => entered;

        public void Open(string heading, string text, bool keypad, Action<string> onSubmit, Action onClose, bool overview = false, Texture image = null, bool reading = false, int reserveLines = 0)
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
            // 살펴보기 판: 줄을 바꾼 설명이 잘리지 않게 줄 수만큼 판을 키운다(아래쪽 끝은 그대로 두고 위로 늘린다).
            float grow = 0f;
            if (overview && !reading)
            {
                int lines = reserveLines;
                foreach (var line in (text ?? "").Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(System.Text.RegularExpressions.Regex.Replace(line, "<.*?>", "").Length / 42f));
                grow = Mathf.Clamp(lines * 30f - 68f, 0f, 330f);
            }
            rect.anchoredPosition = overview ? new Vector2(0, reading ? -290 : -400 + grow * .5f) : Vector2.zero;
            rect.sizeDelta = new Vector2(overview ? 1000 : 640, overview ? (reading ? 460 : 240 + grow) : keypad ? 660 : image != null ? 580 : 440);
            var content = surface.transform.Find("50_Content");
            var cr = (RectTransform)content; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            // The panel's visual layers stay; menu-specific controls do not belong to exploration.
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            var width = rect.sizeDelta.x - 80;
            Text(content, "Title", heading, font, 32, 40, -24, width, 50);
            message = Text(content, "Message", text, font, 22, 40, -88, width, overview ? (reading ? 288 : 68 + grow) : 100);
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
        /// 2026-11-26 사용자: 암호 두 개(날짜·암구호)를 각각 따로 입력하고 저장한다(<see cref="TerminalEntry"/>, 단말기가 보관해 닫아도 유지).
        /// 입력란은 정답 글자 수만큼 칸만 보이고, 두 칸 모두 자판의 모든 글자를 받는다. 위·아래 방향키나 클릭으로 입력란을 바꾼다.
        /// 칸을 다 채우면 그 칸만 확인해 맞으면 줄을 긋고 다음 칸으로, 틀리면 붉은 Error 뒤 그 칸만 비운다. 둘 다 맞으면 판정을 요청한다.
        /// 꾸밈 없는 판·단색 버튼만 쓴다.
        /// </summary>
        public void OpenTerminal(string heading, TerminalEntry state, Action<string> onSubmit, Action onClose)
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            submit = onSubmit; close = onClose; entered = ""; word = ""; entry = state; terminal = true;
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
            // 입력란을 눌러 고른다.
            Clickable(codeLabel, 0); Clickable(wordLabel, 1);
            message = Line("Message", "", -198, 26, TerminalGreen);
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
            // 한 글자 지우기(2026-11-15 사용자: 모두 지우기만 있었다). 셋째 줄 끝, 모두 지우기 바로 위.
            Key(screen, "Back", WaitingRoomText.Get("gate.terminal-back", "DEL"), font, 789, -262 - 2 * (56 + gap), 83, 56, Backspace);
            Key(screen, "Clear", WaitingRoomText.Get("gate.terminal-clear", "CLR"), font, 700, -262 - 3 * (56 + gap), 172, 56, ClearCode);
            Key(screen, "Submit", WaitingRoomText.Get("gate.terminal-submit", "ENTER"), font, 28, -h + 76, 300, 56, SubmitCode);
            Key(screen, "Close", WaitingRoomText.Get("gate.terminal-close", "EXIT"), font, w - 228, -h + 76, 200, 56, () => close?.Invoke());
            RefreshCode(); ShowHelp();
            // 실제 키보드로도 친다(2026-11-16 버그 리포트). 창을 연 키 입력은 다음 프레임부터 무시한다.
            typingFrom = Time.frameCount + 1; ListenKeyboard(true);
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            if (ui != null) ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>()?.ApplyTo(canvasRoot.transform);
        }
        bool listening;
        void ListenKeyboard(bool on)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || listening == on) return;
            listening = on;
            if (on) keyboard.onTextInput += OnTextInput; else keyboard.onTextInput -= OnTextInput;
        }
        void OnTextInput(char c)
        {
            if (!Typing || Time.frameCount < typingFrom) return;
            if (c < 128 && char.IsLetterOrDigit(c)) PressKey(char.ToUpperInvariant(c).ToString());
        }
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (!Typing || keyboard == null || Time.frameCount < typingFrom) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) SelectField(entry.Active - 1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) SelectField(entry.Active + 1);
            else if (keyboard.backspaceKey.wasPressedThisFrame) Backspace();
            else if (keyboard.deleteKey.wasPressedThisFrame) ClearCode();
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) SubmitCode();
        }
        /// <summary>
        /// 단말기 화면에 감시 카메라 영상을 띄운다(2026-11-13 사용자: 암호가 맞으면 탈출로 문이 열리는 화면을 보여 준다).
        /// 자판은 없애고 영상과 한 줄 상태 문구, EXIT만 둔다.
        /// </summary>
        public void ShowFeed(string heading, Texture feed, string status, Action onClose)
        {
            if (canvasRoot != null) Destroy(canvasRoot);
            submit = null; close = onClose; terminal = true; codeLabel = null; wordLabel = null;
            canvasRoot = new GameObject("ExplorationPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 360;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            // 영상이 없으면(문이 아직 열리지 않는 상황) 상태 문구만 있는 낮은 창으로 띄운다.
            float w = 900, h = feed != null ? 640 : 230;
            var frame = Box(canvasRoot.transform, "TerminalFrame", TerminalGreen); var fr = (RectTransform)frame;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(.5f, .5f); fr.sizeDelta = new Vector2(w + 6, h + 6);
            var screen = Box(frame, "Terminal", new Color32(4, 12, 6, 250)); Place((RectTransform)screen, 3, -3, w, h);
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            var title = Text(screen, "Title", heading + " :: " + (feed != null ? WaitingRoomText.Get("gate.terminal-feed-title", "cam_exit.feed") : WaitingRoomText.Get("gate.terminal-title", "door_override.bat")), font, 24, 28, -16, w - 56, 38); title.color = TerminalGreen; title.textWrappingMode = TextWrappingModes.NoWrap;
            Place((RectTransform)Box(screen, "Rule", TerminalGreen), 28, -58, w - 56, 2);
            if (feed != null)
            {
                var picture = new GameObject("MonitorFeed", typeof(RectTransform), typeof(RawImage)); picture.transform.SetParent(screen, false);
                Place((RectTransform)picture.transform, 28, -72, w - 56, 430); var raw = picture.GetComponent<RawImage>(); raw.texture = feed; raw.raycastTarget = false;
            }
            message = Text(screen, "Message", status, font, 28, 28, feed != null ? -512 : -84, w - 56, 42); message.color = TerminalGreen; message.textWrappingMode = TextWrappingModes.NoWrap;
            Key(screen, "Close", WaitingRoomText.Get("gate.terminal-close", "EXIT"), font, w - 228, -h + 76, 200, 56, () => close?.Invoke());
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            if (ui != null) ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>()?.ApplyTo(canvasRoot.transform);
        }
        /// <summary>영상 화면의 상태 문구만 바꾼다(문이 열리는 순간 갱신).</summary>
        public void SetFeedStatus(string text) { if (message != null && message.text != text) message.text = text; }
        TerminalEntry entry;
        /// <summary>단말기 입력 상태(테스트·연출 확인용).</summary>
        public TerminalEntry Terminal => terminal ? entry : null;

        /// <summary>자판 한 글자. 단말기는 지금 입력란에 모든 글자(숫자·영문)를 넣고, 숫자 자판은 숫자만 받는다.</summary>
        public void PressKey(string key)
        {
            if (key == null || key.Length != 1) return;
            if (!terminal || entry == null) { if (key[0] >= '0' && key[0] <= '9') PressDigit(key); return; }
            Report(entry.Type(key[0]));
        }
        public void PressDigit(string digit)
        {
            if (terminal) { PressKey(digit); return; }
            if (digit == null || digit.Length != 1 || digit[0] < '0' || digit[0] > '9' || entered.Length >= codeLength) return;
            entered += digit; RefreshCode();
        }
        /// <summary>단말기: 지금 입력란만 비운다(맞힌 칸은 그대로). 판정 화면이 거절하면 <see cref="ResetTerminal"/>.</summary>
        public void ClearCode()
        {
            if (terminal && entry != null) entry.ClearActive(); else { entered = ""; word = ""; }
            RefreshCode();
        }
        public void ResetTerminal() { entry?.Reset(); RefreshCode(); }
        public void Backspace()
        {
            if (terminal && entry != null) { entry.Backspace(); ShowHelp(); }
            else if (entered.Length > 0) entered = entered.Substring(0, entered.Length - 1);
            RefreshCode();
        }
        public void SelectField(int index)
        {
            if (!terminal || entry == null) return;
            entry.Select(Mathf.Clamp(index, 0, TerminalEntry.Count - 1));
            ShowHelp(); RefreshCode();
        }
        public void SubmitCode()
        {
            if (!terminal) { if (ExplorationPuzzleRules.IsFourDigits(entered)) submit?.Invoke(entered); return; }
            if (entry == null) return;
            if (entry.AllSolved) { submit?.Invoke(entry.Combined); return; }
            Report(entry.Check());
        }
        void Report(TerminalCheck result)
        {
            if (result == TerminalCheck.Wrong) SetMessage(ExplorationState.WrongCode);
            else if (result == TerminalCheck.Incomplete || result == TerminalCheck.Correct) ShowHelp();
            RefreshCode();
            // 두 암호를 모두 맞히면 기존 판정 요청으로 문을 연다.
            if (result == TerminalCheck.Correct && entry.AllSolved) submit?.Invoke(entry.Combined);
        }
        void ShowHelp()
        {
            if (message == null || !terminal) return;
            message.color = TerminalGreen;
            message.text = WaitingRoomText.Get("gate.terminal-help", "암호 2개 · ↑↓ 또는 클릭으로 입력란 선택");
        }
        void Clickable(TMP_Text row, int index)
        {
            row.raycastTarget = true;
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectField(index));
        }
        string Row(int index, string label)
        {
            string value = entry.Value(index);
            string slots = "<mspace=.62em>[" + (entry.IsSolved(index) ? "<s>" + value + "</s>" : value.PadRight(entry.Length(index), '_')) + "]</mspace>";
            return (entry.Active == index && !entry.IsSolved(index) ? "> " : "  ") + label + " " + slots + (entry.IsSolved(index) ? "  OK" : "");
        }
        void RefreshCode()
        {
            if (codeLabel == null) return;
            if (!terminal) { codeLabel.text = entered.PadRight(4, '_'); return; }
            if (entry == null) return;
            codeLabel.text = Row(0, WaitingRoomText.Get("gate.terminal-code", "DATE"));
            codeLabel.alpha = entry.IsSolved(0) ? .6f : 1f;
            if (wordLabel != null)
            {
                wordLabel.text = Row(1, WaitingRoomText.Get("gate.terminal-word", "CMD") + " ");
                wordLabel.alpha = entry.IsSolved(1) ? .6f : 1f;
            }
        }
        public void SetMessage(string text)
        {
            if (message == null) return;
            if (terminal) message.color = string.IsNullOrEmpty(text) ? TerminalGreen : TerminalRed;
            message.text = terminal && text == ExplorationState.WrongCode ? WaitingRoomText.Get("gate.terminal-error", "Error") : text;
        }
        public void Hide() { ListenKeyboard(false); if (canvasRoot != null) canvasRoot.SetActive(false); submit = null; close = null; }
        /// <summary>창을 닫지 않고 잠시 가리거나 다시 보인다(연출이 화면을 쓰는 동안).</summary>
        public void SetVisible(bool visible) { if (canvasRoot != null && canvasRoot.activeSelf != visible) canvasRoot.SetActive(visible); }
        void OnDisable() => Hide();
        void OnDestroy() { ListenKeyboard(false); if (canvasRoot != null) Destroy(canvasRoot); }
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
