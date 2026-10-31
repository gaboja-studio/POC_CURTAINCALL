using CurtainCall.UI.SceneIntegration;
using CurtainCall.UI.TooltipsV1;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 튜토리얼 가이드 전용 패널(2026-10-31 사용자: 가이드와 나레이션은 다른 패널). 툴팁 정본(Tooltips 프리팹)의 패널 모드를
    /// 화면 왼쪽(탐색 시계 아래)에 띄운다. 나레이션(인간 대포 획득·신체 복구 예약 등)은 그대로 <see cref="WaitingRoomGuidePanel"/>을 쓴다.
    /// 입력을 막지 않고 정해진 시간 뒤 사라진다. 새 가이드가 오면 앞 가이드를 바꾼다.
    /// </summary>
    public sealed class WaitingRoomTutorialPanel : MonoBehaviour
    {
        /// <summary>패널 수치 파일: Assets/Resources/Settings/WaitingRoom/WaitingRoomTutorialPanel.txt (`이름 = 값`, 사용자가 직접 수정).</summary>
        const string SettingsPath = "Settings/WaitingRoom/WaitingRoomTutorialPanel";

        static WaitingRoomTutorialPanel instance;
        static WaitingRoomTutorialPanelSettings settings;

        /// <summary>위치·너비·표시 시간·간격·글자 크기. 플레이마다 한 번 읽는다.</summary>
        public static WaitingRoomTutorialPanelSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                var file = Resources.Load<TextAsset>(SettingsPath);
                settings = WaitingRoomTutorialPanelSettings.Parse(file != null ? file.text : null);
                return settings;
            }
        }
        GameObject root;
        TooltipView view;
        UiRuntimeFontFallback fontOwner;
        string title = "", body = "";
        float hideAt;

        public static bool IsVisible => instance != null && instance.view != null && instance.hideAt > Time.unscaledTime;
        public static string CurrentTitle => IsVisible ? instance.title : "";
        public static string CurrentBody => IsVisible ? instance.body : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; settings = null; }

        /// <param name="seconds">0 이하면 설정 파일의 기본 표시 시간.</param>
        public static bool Show(string heading, string text, float seconds)
        {
            if (instance == null)
            {
                var go = new GameObject(nameof(WaitingRoomTutorialPanel));
                DontDestroyOnLoad(go);
                instance = go.AddComponent<WaitingRoomTutorialPanel>();
            }
            return instance.Open(heading, text, seconds);
        }

        public static void Hide() { if (instance != null) instance.Close(); }

        bool Open(string heading, string text, float seconds)
        {
            // 공용 UI 씬이 다시 올라오면 앞 폰트 보완본은 지워진다(공연 다녀옴). 새 보완본으로 다시 만든다.
            var owner = FontOwner();
            if (root != null && owner != fontOwner) { Destroy(root); root = null; view = null; }
            if (view == null) Build();
            var s = Settings;
            if (view == null || !view.ShowPanel(heading, text, new Vector2(s.X, s.Y), s.Width)) return false;
            title = heading; body = text;
            hideAt = Time.unscaledTime + Mathf.Max(1f, seconds > 0f ? seconds : s.Seconds);
            return true;
        }

        void Build()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/UIs/Tooltips/Tooltips");
            if (prefab == null) { Debug.LogError("[Tutorial] Tooltips 프리팹이 없습니다."); return; }
            root = Instantiate(prefab, transform, false);
            root.name = "TutorialGuideCanvas";
            var canvas = root.GetComponent<Canvas>();
            // 나레이션 패널(350)보다 아래, 일반 HUD보다 위.
            if (canvas != null) canvas.sortingOrder = 340;
            view = root.GetComponentInChildren<TooltipView>(true);
            if (view == null) { Debug.LogError("[Tutorial] Tooltips 프리팹에 TooltipView가 없습니다."); return; }
            view.ResetForRuntime();
            fontOwner = FontOwner();
            if (fontOwner != null) fontOwner.ApplyTo(root.transform);
            // 글자 크기(0이면 프리팹 기본). 패널 높이는 본문 크기로 다시 계산된다.
            foreach (var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (label.name == "60_TitleTMP" && Settings.TitleSize > 0f) label.fontSize = Settings.TitleSize;
                else if (label.name == "70_DescriptionTMP" && Settings.BodySize > 0f) label.fontSize = Settings.BodySize;
            }
        }

        static UiRuntimeFontFallback FontOwner()
        {
            var ui = FindFirstObjectByType<CommonWaitingRoomUI>();
            return ui != null ? ui.GetComponent<UiRuntimeFontFallback>() : null;
        }

        void Close()
        {
            hideAt = 0f;
            if (view != null) view.Hide();
        }

        void Update() { if (view != null && hideAt > 0f && Time.unscaledTime >= hideAt) Close(); }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
