using System;
using CurtainCall.Cameras;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 화면 오른쪽 아래 투표 패널(리허설 넘기기·리허설 다시 하기, 사용자 2026-11-04). 기존 BasicPanels 몸체와 CommonButtons 버튼을 쓴다.
    /// 내용이 바뀔 때만 다시 그린다. 패널 위에서는 커서를 다시 잠그지 않는다(Esc로 커서를 풀고 누른다).
    /// </summary>
    public sealed class RehearsalVotePanel : MonoBehaviour
    {
        public readonly struct Choice
        {
            public readonly string Label;
            public readonly bool Interactable;
            public readonly Action OnClick;
            public Choice(string label, bool interactable, Action onClick) { Label = label; Interactable = interactable; OnClick = onClick; }
        }

        const float Width = 440f, Height = 230f, Margin = 36f;
        /// <summary>오른쪽 아래 소지품 칸(1·2) 위에 둔다(1920×1080 기준).</summary>
        const float BottomOffset = 120f;
        GameObject canvasRoot;
        RectTransform surface;
        TMP_Text title, body, hint;
        readonly Button[] buttons = new Button[2];
        readonly Action[] actions = new Action[2];
        CameraCursorExclusion cursorRegion;
        string shownKey;

        public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;
        public string Title => IsVisible ? title.text : "";
        public string Body => IsVisible ? body.text : "";
        public RectTransform VisibleSurface => IsVisible ? surface : null;

        public void Show(string heading, string text, params Choice[] choices)
        {
            if (canvasRoot == null && !Build()) return;
            string key = heading + "|" + text;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool used = i < choices.Length;
                buttons[i].gameObject.SetActive(used);
                if (!used) { actions[i] = null; continue; }
                actions[i] = choices[i].OnClick;
                buttons[i].interactable = choices[i].Interactable;
                key += "|" + choices[i].Label + choices[i].Interactable;
                var label = buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label.text != choices[i].Label) label.text = choices[i].Label;
                // 버튼이 하나면 가운데, 둘이면 나란히 둔다.
                float w = choices.Length == 1 ? 220f : 170f;
                float x = choices.Length == 1 ? (Width - w) * .5f : 40f + i * (w + 20f);
                Place((RectTransform)buttons[i].transform, x, -(Height - 78f), w, 54f);
            }
            if (key != shownKey)
            {
                title.text = heading; body.text = text; shownKey = key;
                // 패널 글꼴에 없는 한글은 게임 글꼴 보완본에 미리 넣는다(새 문구마다).
                var labels = new System.Text.StringBuilder(heading).Append(text).Append(hint.text);
                foreach (var choice in choices) labels.Append(choice.Label);
                FontOwner()?.WarmUpLocalText(canvasRoot.transform, labels.ToString());
            }
            if (!canvasRoot.activeSelf) canvasRoot.SetActive(true);
            ScheduleScreenHold.Track(canvasRoot.GetComponent<Canvas>());
            UpdateCursorRegion();
        }

        public void Hide()
        {
            if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false);
            if (cursorRegion != null) cursorRegion.enabled = false;
            shownKey = null;
        }

        bool Build()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/BasicPanels/BasicPanels");
            var buttonPrefab = Resources.Load<GameObject>("Prefabs/UIs/CommonButtons/Button_General");
            if (prefab == null || buttonPrefab == null) { Debug.LogError("[Rehearsal] 투표 패널 프리팹을 찾지 못했습니다."); return false; }
            canvasRoot = new GameObject("RehearsalVoteCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 345;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var panel = Instantiate(prefab.transform.Find("60_Panels/Menu_C13").gameObject, canvasRoot.transform, false);
            panel.name = "RehearsalVotePanel"; panel.SetActive(true);
            // 메뉴 판의 연출·입력 컴포넌트는 쓰지 않는다(그림자·몸체·프레임·모서리 이미지만 남긴다).
            foreach (var b in panel.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && b.GetType().Namespace == typeof(CurtainCall.UI.PanelsV1.CurtainCallPanelView).Namespace) DestroyImmediate(b);
            surface = (RectTransform)panel.transform;
            surface.anchorMin = surface.anchorMax = surface.pivot = new Vector2(1f, 0f);
            surface.anchoredPosition = new Vector2(-Margin, BottomOffset);
            surface.sizeDelta = new Vector2(Width, Height);
            if (!panel.TryGetComponent(out CanvasGroup group)) group = panel.AddComponent<CanvasGroup>();
            group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
            var content = panel.transform.Find("50_Content");
            var cr = (RectTransform)content; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            title = Text(content, "Title", font, 26, -22, 40, new Color32(242, 180, 91, 255));
            body = Text(content, "Body", font, 19, -64, 74, new Color32(238, 234, 215, 255));
            hint = Text(content, "Hint", font, 14, -(Height - 22f), 18, new Color32(190, 184, 170, 255));
            hint.text = WaitingRoomText.Get("rehearsal.vote-panel.hint", "Esc로 커서 해제 후 클릭");
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                var go = Instantiate(buttonPrefab, content, false); go.name = "Choice" + i;
                var label = go.GetComponentInChildren<TMP_Text>(true); label.font = font;
                buttons[i] = go.GetComponent<Button>();
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => actions[index]?.Invoke());
            }
            FontOwner()?.ApplyTo(canvasRoot.transform);
            cursorRegion = gameObject.AddComponent<CameraCursorExclusion>();
            cursorRegion.enabled = false;
            canvasRoot.SetActive(false);
            return true;
        }

        static CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback FontOwner()
        {
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            return ui != null ? ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>() : null;
        }

        void UpdateCursorRegion()
        {
            if (cursorRegion == null || surface == null) return;
            var corners = new Vector3[4];
            surface.GetWorldCorners(corners);
            // 화면 좌표(왼쪽 아래 원점)를 IMGUI 기준(왼쪽 위 원점)으로 바꾼다.
            float x = corners[0].x, y = Screen.height - corners[1].y;
            cursorRegion.SetScreenRect(new Rect(x, y, corners[2].x - corners[0].x, corners[1].y - corners[0].y));
            cursorRegion.enabled = true;
        }

        TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, float y, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, 32f, y, Width - 64f, h);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = color;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h); }

        void OnDisable() => Hide();
        void OnDestroy() { if (canvasRoot != null) Destroy(canvasRoot); }
    }
}
