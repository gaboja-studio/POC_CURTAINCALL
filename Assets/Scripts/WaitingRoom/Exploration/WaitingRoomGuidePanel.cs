using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 안내 패널(2026-10-30 사용자: 기존 패널 이미지로 가이드 UI). 탐색 화면(<see cref="ExplorationPanel"/>)과 같은 BasicPanels 판을 쓰고,
    /// 입력을 막지 않는 알림이라 버튼 없이 화면 위쪽에 제목·설명을 띄웠다가 정해진 시간 뒤 사라진다. 새 안내가 오면 앞 안내를 바꾼다.
    /// </summary>
    public sealed class WaitingRoomGuidePanel : MonoBehaviour
    {
        static WaitingRoomGuidePanel instance;
        GameObject canvasRoot;
        CanvasGroup group;
        TMP_Text title, body;
        float shownAt, hideAt;

        public static bool IsVisible => instance != null && instance.canvasRoot != null && instance.canvasRoot.activeSelf;
        public static string CurrentTitle => IsVisible ? instance.title.text : "";
        public static string CurrentBody => IsVisible ? instance.body.text : "";

        /// <summary>안내를 <paramref name="seconds"/>초 동안 띄운다.</summary>
        public static void Show(string heading, string text, float seconds = 7f)
        {
            if (instance == null)
            {
                var go = new GameObject("WaitingRoomGuidePanel");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<WaitingRoomGuidePanel>();
            }
            instance.Open(heading, text, seconds);
        }

        public static void Hide() { if (instance != null && instance.canvasRoot != null) instance.canvasRoot.SetActive(false); }

        void Open(string heading, string text, float seconds)
        {
            if (canvasRoot == null) Build();
            title.text = heading; body.text = text;
            canvasRoot.SetActive(true);
            shownAt = Time.unscaledTime; hideAt = shownAt + Mathf.Max(1f, seconds);
        }

        void Build()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/BasicPanels/BasicPanels");
            if (prefab == null) throw new System.InvalidOperationException("BasicPanels prefab missing");
            canvasRoot = new GameObject("GuidePanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 350;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var surface = Instantiate(prefab.transform.Find("60_Panels/Menu_C13").gameObject, canvasRoot.transform, false);
            surface.name = "GuidePanel"; surface.SetActive(true);
            // 메뉴 판의 입력·연출 컴포넌트는 쓰지 않는다(그림자·몸체·프레임·모서리 이미지만 남긴다).
            foreach (var b in surface.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && b.GetType().Namespace == typeof(CurtainCall.UI.PanelsV1.CurtainCallPanelView).Namespace) DestroyImmediate(b);
            var rect = (RectTransform)surface.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -90); rect.sizeDelta = new Vector2(760, 230);
            group = surface.GetComponent<CanvasGroup>() ?? surface.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false; group.alpha = 1f;
            var content = surface.transform.Find("50_Content");
            var cr = (RectTransform)content; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var font = Resources.Load<TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            title = Text(content, "Title", font, 30, -26, 46, new Color32(242, 180, 91, 255));
            body = Text(content, "Body", font, 21, -82, 130, new Color32(238, 234, 215, 255));
            var ui = FindFirstObjectByType<CurtainCall.UI.SceneIntegration.CommonWaitingRoomUI>();
            if (ui != null) ui.GetComponent<CurtainCall.UI.SceneIntegration.UiRuntimeFontFallback>()?.ApplyTo(canvasRoot.transform);
        }

        static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, float y, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(40, y); r.sizeDelta = new Vector2(680, h);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = color;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        void Update()
        {
            if (canvasRoot == null || !canvasRoot.activeSelf) return;
            float now = Time.unscaledTime;
            // 0.16초 나타남, 마지막 0.4초 사라짐.
            group.alpha = Mathf.Min(Mathf.Clamp01((now - shownAt) / .16f), Mathf.Clamp01((hideAt - now) / .4f));
            if (now >= hideAt) canvasRoot.SetActive(false);
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
