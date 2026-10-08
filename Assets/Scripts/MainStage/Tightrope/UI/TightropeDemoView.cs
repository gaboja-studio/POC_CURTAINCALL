using System;
using DG.Tweening;
using CurtainCall.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>데모 UI 배치와 표시. 결과 판정이나 기록 저장은 하지 않는다.</summary>
    public sealed class TightropeDemoView : MonoBehaviour
    {
        static readonly Color Navy = new(0.055f, 0.09f, 0.16f, 0.97f);
        static readonly Color Ivory = new(0.97f, 0.95f, 0.87f);
        static readonly Color Gold = new(0.89f, 0.73f, 0.37f);
        TMP_FontAsset font;
        CanvasGroup lobbyGroup, resultGroup;
        GameObject lobby, result, hud;
        public TMP_InputField JoinInput { get; private set; }
        public TMP_InputField LanInput { get; private set; }
        public TMP_Text Connection { get; private set; }
        public TMP_Text Error { get; private set; }
        public TMP_Text Room { get; private set; }
        public TMP_Text HudText { get; private set; }
        public TMP_Text FireText { get; private set; }
        public TMP_Text Storage { get; private set; }
        public TMP_Text StorageDetails { get; private set; }
        public TMP_Text ResultTitle { get; private set; }
        public TMP_Text ResultBody { get; private set; }
        public TMP_Text RetryText { get; private set; }
        public Button HostServices { get; private set; }
        public Button JoinServices { get; private set; }
        public Button HostLan { get; private set; }
        public Button JoinLan { get; private set; }
        public Button Start { get; private set; }
        public Button Copy { get; private set; }
        public Button Leave { get; private set; }
        public Button OpenRoom { get; private set; }
        public Button CloseRoom { get; private set; }
        public Button History { get; private set; }
        public Button Retry { get; private set; }
        public Button CloseResult { get; private set; }
        public Button PreviousResult { get; private set; }
        public Button NextResult { get; private set; }
        public bool LobbyVisible => lobby != null && lobby.activeSelf;
        public bool ResultVisible => result != null && result.activeSelf;
        public BalanceGaugeView Gauge { get; private set; }
        public bool Built { get; private set; }

        public void Build()
        {
            if (Built) return;
            font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            var bar = Box("상단 도구", transform, new Color(Navy.r, Navy.g, Navy.b, 0.9f));
            Anchor(bar, new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(-40, 74));
            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(20, 20, 10, 10);
            barLayout.spacing = 16;
            barLayout.childForceExpandWidth = false;
            Text("CURTAIN CALL  /  외줄 묘기", bar, 26, 390);
            Storage = Text("기록 준비 중", bar, 19, 750);
            OpenRoom = Button("방 정보", bar, 190);
            History = Button("지난 결과", bar, 190);

            hud = Box("진행 HUD", transform, new Color(Navy.r, Navy.g, Navy.b, 0.88f)).gameObject;
            var hudRect = (RectTransform)hud.transform;
            Anchor(hudRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -116), new Vector2(570, 285));
            Stack(hudRect, 20, 10);
            HudText = Text("대기 중", hudRect, 26, 0, 142);
            FireText = Text("화재 정보 대기", hudRect, 20, 0, 90);

            var help = Box("조작 안내", transform, new Color(Navy.r, Navy.g, Navy.b, 0.85f));
            Anchor(help, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(-40, 60));
            var helpText = Text("W/S 앞뒤  ·  A/D 균형  ·  Space 점프  ·  Q/E + Space 옆줄  ·  F 목마 연결 / 길게 해제", help, 23);
            Stretch(helpText.rectTransform, 16);

            lobby = Modal("방 접속", out lobbyGroup, out var lobbyPanel);
            lobbyPanel.sizeDelta = new Vector2(1040, 980);
            Stack(lobbyPanel, 24, 8);
            Text("외줄 묘기", lobbyPanel, 40, 0, 60).color = Gold;
            Connection = Text("접속하지 않음", lobbyPanel, 24, 0, 46);
            Room = Text("방 정보 없음", lobbyPanel, 23, 0, 62);
            HostServices = Button("Services 방 만들기", lobbyPanel);
            JoinInput = Input("방 코드", lobbyPanel);
            JoinServices = Button("Services 참가", lobbyPanel);
            Text("보조 접속 · 같은 네트워크 LAN", lobbyPanel, 20, 0, 36);
            var lanRow = Row("LAN", lobbyPanel);
            HostLan = Button("LAN 방 만들기", lanRow, 300);
            LanInput = Input("IPv4 또는 IPv4:포트", lanRow, 600);
            JoinLan = Button("LAN 참가", lobbyPanel);
            var actions = Row("방 동작", lobbyPanel);
            Copy = Button("코드 복사", actions, 290);
            Start = Button("게임 시작", actions, 290);
            Leave = Button("나가기", actions, 290);
            Error = Text("", lobbyPanel, 20, 0, 58);
            Error.color = new Color(1f, 0.67f, 0.59f);
            StorageDetails = Text("기록 상태 대기", lobbyPanel, 17, 0, 90);
            CloseRoom = Button("닫기", lobbyPanel);

            result = Modal("고정 결과", out resultGroup, out var resultPanel);
            Stack(resultPanel, 32, 16);
            ResultTitle = Text("묘기 결과", resultPanel, 40, 0, 64);
            ResultTitle.color = Gold;
            var scrollRoot = Box("참가자 결과", resultPanel, new Color(1, 1, 1, 0.035f));
            Size(scrollRoot.gameObject, 0, 410);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Box("Viewport", scrollRoot, Color.clear);
            Stretch(viewport, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.GetComponent<Image>().raycastTarget = true;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(-28, 0);
            ResultBody = content.AddComponent<TextMeshProUGUI>();
            ConfigureText(ResultBody, 24);
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = contentRect;
            RetryText = Text("", resultPanel, 24, 0, 64);
            var historyRow = Row("결과 탐색", resultPanel);
            PreviousResult = Button("이전 결과", historyRow);
            NextResult = Button("다음 결과", historyRow);
            Retry = Button("재도전", resultPanel);
            CloseResult = Button("닫기", resultPanel);
            result.SetActive(false);

            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Player/BalanceGauge");
            if (prefab != null)
            {
                var gauge = Instantiate(prefab, transform, false);
                gauge.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);
                // 화면 전체를 기준으로 기존 하단 중앙 배치를 유지한다.
                var gaugeRect = (RectTransform)gauge.transform;
                gaugeRect.pivot = new Vector2(0.5f, 0.5f);
                Stretch(gaugeRect, 0);
                gaugeRect.localScale = Vector3.one;
                gaugeRect.localRotation = Quaternion.identity;
                var gaugeScaler = gauge.GetComponent<CanvasScaler>();
                if (gaugeScaler != null) gaugeScaler.enabled = false;
                Gauge = gauge.GetComponentInChildren<BalanceGaugeView>(true);
                if (Gauge != null)
                {
                    Gauge.AutoFindTarget = false;
                    Gauge.Target = null;
                }
            }
            Built = true;
        }

        public void ShowLobby(bool value) => Show(lobby, lobbyGroup, value);
        public void ShowResult(bool value) => Show(result, resultGroup, value);
        public void ShowHud(bool value)
        {
            if (hud != null) hud.SetActive(value);
            if (Gauge != null) Gauge.gameObject.SetActive(value);
        }

        static void Show(GameObject panel, CanvasGroup group, bool value)
        {
            if (panel.activeSelf == value) return;
            group.DOKill();
            panel.SetActive(value);
            if (!value) return;
            group.alpha = 0f;
            group.DOFade(1f, 0.16f).SetUpdate(true);
        }

        void OnDestroy()
        {
            if (lobbyGroup != null) lobbyGroup.DOKill();
            if (resultGroup != null) resultGroup.DOKill();
        }

        GameObject Modal(string name, out CanvasGroup group, out RectTransform panel)
        {
            var backdrop = Box(name, transform, new Color(0.01f, 0.02f, 0.04f, 0.68f));
            Stretch(backdrop, 0);
            backdrop.GetComponent<Image>().raycastTarget = true;
            group = backdrop.gameObject.AddComponent<CanvasGroup>();
            panel = Box("패널", backdrop, Navy);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 880));
            return backdrop.gameObject;
        }

        static RectTransform Box(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = color;
            obj.GetComponent<Image>().raycastTarget = false;
            return (RectTransform)obj.transform;
        }

        TMP_Text Text(string value, Transform parent, float fontSize, float width = 0, float height = 54)
        {
            var obj = new GameObject("문구", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            ConfigureText(text, fontSize);
            text.text = value;
            Size(obj, width, height);
            return text;
        }

        void ConfigureText(TMP_Text text, float size)
        {
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = Ivory;
            text.richText = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        Button Button(string label, Transform parent, float width = 0)
        {
            var rect = Box(label, parent, new Color(0.17f, 0.22f, 0.31f));
            rect.GetComponent<Image>().raycastTarget = true;
            Size(rect.gameObject, width, 60);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var text = Text(label, rect, 25);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, 10);
            return button;
        }

        TMP_InputField Input(string placeholder, Transform parent, float width = 0)
        {
            var rect = Box(placeholder, parent, new Color(0.12f, 0.16f, 0.23f));
            rect.GetComponent<Image>().raycastTarget = true;
            Size(rect.gameObject, width, 60);
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            var area = new GameObject("입력 영역", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(rect, false);
            Stretch((RectTransform)area.transform, 14);
            var text = Text("", area.transform, 24);
            Stretch(text.rectTransform, 0);
            var hint = Text(placeholder, area.transform, 24);
            hint.color = new Color(Ivory.r, Ivory.g, Ivory.b, 0.55f);
            Stretch(hint.rectTransform, 0);
            field.textViewport = (RectTransform)area.transform;
            field.textComponent = (TextMeshProUGUI)text;
            field.placeholder = hint;
            field.targetGraphic = rect.GetComponent<Image>();
            field.characterLimit = 128;
            field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }

        static RectTransform Row(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            obj.transform.SetParent(parent, false);
            Size(obj, 0, 60);
            var layout = obj.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return (RectTransform)obj.transform;
        }

        static void Stack(RectTransform panel, int padding, float spacing)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
        }

        static void Size(GameObject obj, float width, float height)
        {
            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            if (width > 0) layout.preferredWidth = width;
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
