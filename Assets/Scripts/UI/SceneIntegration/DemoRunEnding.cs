using CurtainCall.Campaign;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using CurtainCall.WaitingRoom;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>밤낮 전환·일차 표시와 0.1.0 종료 표시. 각 화면이 준비된 후 호스트에 완료를 알린다.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class DemoRunEnding : MonoBehaviour
    {
        public const string Message = "데모 플레이가 종료되었습니다. 플레이해주셔서 감사합니다.";
        GameObject overlay;
        UnityEngine.UI.Image black;
        TMP_FontAsset font;
        TMP_Text label;
        UnityEngine.UI.Button confirm;
        CampaignSnapshot previous;
        bool titlePending, titleRunning, confirmed;
        double began = -1, titleBegan;
        public const float DayFadeSeconds = 2f;
        public const float DayTitleSeconds = 3f;

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run == null || !run.State.IsRunning)
            {
                previous = default;
                titlePending = titleRunning = confirmed = false;
                began = -1;
                Hide();
                return;
            }
            var state = run.State;
            if (state.Generation != previous.Generation || state.Day != previous.Day || state.Phase != previous.Phase)
            {
                bool newMorning = previous.Phase == CampaignPhase.Dawn && state.Day == previous.Day + 1;
                bool firstPerformance = state.Phase == CampaignPhase.Performance && state.Day == 1
                    && (previous.Phase == CampaignPhase.Intro || (state.Generation > previous.Generation && state.LastEnd != RunEndReason.None));
                titlePending = newMorning || state.Phase == CampaignPhase.DemoEnding || firstPerformance;
                titleRunning = confirmed = false;
                began = -1;
                previous = state;
            }
            if (state.Phase == CampaignPhase.Dawn)
            {
                if (began < 0 && ScheduleIsOpen()) return;
                Show(true);
                label.gameObject.SetActive(false);
                confirm.gameObject.SetActive(false);
                if (began < 0) began = Time.realtimeSinceStartupAsDouble;
                float elapsed = (float)(Time.realtimeSinceStartupAsDouble - began);
                black.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / DayFadeSeconds));
                if (elapsed >= DayFadeSeconds && !run.IsLocalReady) run.SetReady(true);
                return;
            }
            if (titlePending)
            {
                if (ScheduleIsOpen()) return;
                if (state.Phase == CampaignPhase.Performance && !PerformanceLoaded())
                {
                    Show(true); black.color = Color.black;
                    label.gameObject.SetActive(false); confirm.gameObject.SetActive(false);
                    return;
                }
                Show(true);
                titleBegan = Time.realtimeSinceStartupAsDouble;
                titlePending = false; titleRunning = true;
                label.text = DayTitle(state.Day);
            }
            if (titleRunning)
            {
                float elapsed = (float)(Time.realtimeSinceStartupAsDouble - titleBegan);
                Show(elapsed < DayFadeSeconds);
                confirm.gameObject.SetActive(false);
                label.gameObject.SetActive(true);
                black.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(elapsed / DayFadeSeconds));
                label.color = new Color(1, 1, 1, 1f - Mathf.Clamp01((elapsed - DayTitleSeconds) / DayFadeSeconds));
                if (elapsed < DayTitleSeconds + DayFadeSeconds) return;
                titleRunning = false; began = -1;
            }
            if (state.Phase == CampaignPhase.DemoEnding)
            {
                if (began < 0 && ScheduleIsOpen()) return;
                Show(true);
                if (began < 0) began = Time.realtimeSinceStartupAsDouble;
                float elapsed = (float)(Time.realtimeSinceStartupAsDouble - began);
                bool covered = elapsed >= CampaignRun.DemoEndingSeconds;
                black.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / CampaignRun.DemoEndingSeconds));
                label.text = Message; label.color = Color.white;
                label.gameObject.SetActive(covered);
                confirm.gameObject.SetActive(covered);
                confirm.interactable = !confirmed;
                confirm.GetComponentInChildren<TMP_Text>().text = confirmed ? "확인 완료" : "확인";
                if (covered && EventSystem.current == null)
                {
                    var events = new GameObject("DemoEndingEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    events.transform.SetParent(overlay.transform, false);
                }
                return;
            }
            Hide();
        }

        internal static bool ScheduleIsOpen() => Object.FindObjectsByType<ScheduleBoardView>(FindObjectsSortMode.None).Any(v => v.IsOpen)
            || Object.FindObjectsByType<WaitingRoomScheduleUI>(FindObjectsSortMode.None).Any(v => v.View != null && v.View.IsVisible);

        static bool PerformanceLoaded()
        {
            var director = SpaceLinkDirector.Instance;
            return director == null || (!director.IsTransitioning && director.Current == SpaceId.Performance
                && !CommonUiSceneLoader.IsLoading && !SceneCurtainTransition.IsActive);
        }

        void Show(bool blocking)
        {
            if (overlay == null) CreateOverlay();
            overlay.SetActive(true);
            black.raycastTarget = blocking;
            InputGate.SetBlocking(this, blocking);
        }

        void Hide()
        {
            if (overlay != null) overlay.SetActive(false);
            InputGate.SetBlocking(this, false);
        }

        void CreateOverlay()
        {
            overlay = new GameObject("DemoRunEnding", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var background = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(overlay.transform, false);
            Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            black = background.GetComponent<UnityEngine.UI.Image>();
            var text = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(overlay.transform, false);
            Stretch(text.GetComponent<RectTransform>(), new Vector2(.08f, .35f), new Vector2(.92f, .65f));
            label = text.GetComponent<TextMeshProUGUI>();
            // 전용 런타임 폰트만 생성하므로 기존 정적 폰트/아틀라스를 수정하지 않는다.
            font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1"));
            font.hideFlags = HideFlags.DontSave;
            font.TryAddCharacters(Message + "1234일차확인완료");
            label.font = font;
            label.text = Message;
            label.fontSize = 36;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var button = new GameObject("Confirm", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(overlay.transform, false);
            Stretch(button.GetComponent<RectTransform>(), new Vector2(.41f, .23f), new Vector2(.59f, .31f));
            button.GetComponent<UnityEngine.UI.Image>().color = new Color(.16f, .16f, .16f, 1);
            confirm = button.GetComponent<UnityEngine.UI.Button>();
            confirm.onClick.AddListener(() => {
                var run = CampaignRun.Instance;
                if (!confirmed && confirm.gameObject.activeSelf && run != null && run.State.Phase == CampaignPhase.DemoEnding)
                    confirmed = run.SetReady(true);
            });
            var caption = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            caption.transform.SetParent(button.transform, false);
            Stretch(caption.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var captionText = caption.GetComponent<TextMeshProUGUI>();
            captionText.font = font; captionText.fontSize = 30; captionText.text = "확인";
            captionText.alignment = TextAlignmentOptions.Center; captionText.raycastTarget = false;
            confirm.gameObject.SetActive(false);
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void OnDisable()
        {
            if (overlay != null) overlay.SetActive(false);
            InputGate.SetBlocking(this, false);
        }

        void OnDestroy()
        {
            InputGate.SetBlocking(this, false);
            if (font != null) Destroy(font);
        }
        /// <summary>날짜 전환 제목(2026-10-30 사용자): Day 1·Day 2·Day 3, 마지막 탈출 아침(공연하는 날 수 다음 날)은 D-Day.</summary>
        public static string DayTitle(int day) => day > CurtainCall.Settings.GameSettings.Base.Run.DayCount ? "D-Day" : $"Day {day}";
    }
}
