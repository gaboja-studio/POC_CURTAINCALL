using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 기물 파괴 알림(2026-10-31 사용자: 내구도가 다 닳아 기물이 파괴되면 화면 오른쪽 패널로 알린다).
    /// 내 탐색 도구 사용 결과(<see cref="ExplorationState.LastUsedUp"/>)를 보고 매번 띄운다. 오른쪽 위에는 오늘 할 일([tutorial])이 있어 그 아래에 띄운다.
    /// 문구: 대기실 문구 파일 `notice.item-broken-title`·`notice.item-broken`.
    /// </summary>
    public sealed class WaitingRoomItemBrokenNotice : MonoBehaviour
    {
        const float Width = 360f, Seconds = 4f, Top = 560f;

        static WaitingRoomItemBrokenNotice instance;
        ExplorationState watched;
        WaitingRoomRightPanel panel;
        float hideAt;

        public static bool IsVisible => instance != null && instance.panel != null && instance.panel.Visible;
        public static string CurrentTitle => IsVisible ? instance.panel.Title : "";
        public static string CurrentBody => IsVisible ? instance.panel.Body : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WaitingRoomItemBrokenNotice));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WaitingRoomItemBrokenNotice>();
        }

        void Awake() => panel = new WaitingRoomRightPanel(transform, "ItemBrokenNoticeCanvas", Top, Width);

        /// <summary><paramref name="item"/>이 파괴되었다고 알린다.</summary>
        public static void Show(string item)
        {
            if (instance == null) Boot();
            var title = WaitingRoomText.Format("notice.item-broken-title", "{item}", ("item", item));
            var body = WaitingRoomText.Format("notice.item-broken", "기물이 파괴되었습니다.", ("item", item));
            // 같은 문구로 연달아 와도 시간은 새로 센다.
            instance.panel.Hide();
            if (instance.panel.Show(title, body)) instance.hideAt = Time.unscaledTime + Seconds;
        }

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state != watched)
            {
                if (watched != null) watched.RequestAnswered -= OnAnswered;
                watched = state;
                if (watched != null) watched.RequestAnswered += OnAnswered;
            }
            if (hideAt > 0f && Time.unscaledTime >= hideAt) { hideAt = 0f; panel.Hide(); }
        }

        void OnDestroy()
        {
            if (watched != null) watched.RequestAnswered -= OnAnswered;
            if (instance == this) instance = null;
        }

        void OnAnswered(string _)
        {
            if (watched != null && watched.LastUsedUp) Show(watched.LastUsedUpItem);
        }
    }
}
