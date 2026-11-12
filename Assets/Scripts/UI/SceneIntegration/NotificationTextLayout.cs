using CurtainCall.UI.NotificationsV1;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>알림 텍스트를 최대 두 줄로 배치하고 상단 배너는 HUD 배치에 맞춰 정렬한다.</summary>
    [DisallowMultipleComponent]
    public sealed class NotificationTextLayout : MonoBehaviour
    {
        RectTransform panel;
        TMP_Text title, subtitle, message;
        float baseHeight, baseWidth, titleHeight, subtitleHeight, messageHeight;
        // [F] 안내 박스: 글자 칸은 왼쪽 92(키 칸) + 오른쪽 여백 20. 긴 문구(도구 힌트 등)는 박스를 넓히고 3줄까지 보인다(2026-11-01 사용자).
        const float PromptTextLeft = 92, PromptTextRight = 20, PromptMaxWidth = 640;
        const int PromptMaxLines = 3;
        string previousTitle, previousSubtitle, previousMessage;
        float previousWidth;
        (string key, bool helperVisible, TMP_FontAsset keyFont, TMP_FontAsset actionFont, TMP_FontAsset helperFont,
            Vector2 parentSize) fittedDevice;
        (string, bool, TMP_FontAsset, TMP_FontAsset, TMP_FontAsset, Vector2) DeviceMetrics() =>
            (key.text, subtitle.gameObject.activeSelf, key.font, title.font, subtitle.font,
                panel.parent is RectTransform parent ? parent.rect.size : Vector2.zero);
        bool DevicePrompt => interaction && deviceText && key != null && title != null && subtitle != null;
        bool initialized, interaction, banner;
        CommonWaitingRoomUI waitingRoom;
        bool deviceText;
        TMP_Text key;
        float keyFontSize, keyBaseWidth;
        readonly System.Collections.Generic.Dictionary<RectTransform, float> keyWidths = new();
        TextOverflowModes keyOverflow;
        float TextLeft => PromptTextLeft + (deviceText && key != null ? key.rectTransform.rect.width - keyBaseWidth : 0);
        public void SetDeviceText(bool value) => deviceText = value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Install(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
        }

        static void Install(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var banner in root.GetComponentsInChildren<SystemBannerView>(true)) Attach(banner);
                foreach (var toast in root.GetComponentsInChildren<ToastView>(true)) Attach(toast);
                foreach (var guide in root.GetComponentsInChildren<CurtainCall.UI.GuidesV1.GuideView>(true)) HudText.ConfigureView(guide);
                foreach (var tooltip in root.GetComponentsInChildren<CurtainCall.UI.TooltipsV1.TooltipView>(true)) HudText.ConfigureView(tooltip);
                foreach (var spectator in root.GetComponentsInChildren<CurtainCall.UI.SpectatorV1.SpectatorView>(true)) HudText.ConfigureView(spectator);
                foreach (var feedback in root.GetComponentsInChildren<CurtainCall.UI.FeedbackView>(true)) HudText.ConfigureView(feedback);
                foreach (var hud in root.GetComponentsInChildren<CurtainCall.UI.BackstageHudV1.BackstageHUDView>(true)) HudText.ConfigureView(hud);
                foreach (var hud in root.GetComponentsInChildren<CurtainCall.UI.PerformanceHudV1.PerformanceHUDView>(true)) HudText.ConfigureView(hud);
                foreach (var caption in root.GetComponentsInChildren<CurtainCall.UI.CaptionsV1.CaptionView>(true)) HudText.ConfigureView(caption);
            }
        }

        static void Attach(Component view)
        {
            if (view.GetComponent<NotificationTextLayout>() == null)
                view.gameObject.AddComponent<NotificationTextLayout>();
        }

        void Awake() => Initialize();
        void OnEnable() => RefreshLayout();

        void Initialize()
        {
            if (initialized) return;
            panel = (RectTransform)transform;
            banner = GetComponent<SystemBannerView>() != null;
            if (banner && gameObject.scene.path == CommonWaitingRoomUI.ScenePath)
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    waitingRoom = root.GetComponentInChildren<CommonWaitingRoomUI>(true);
                    if (waitingRoom != null) break;
                }
            title = transform.Find("Motion/60_TitleTMP")?.GetComponent<TMP_Text>();
            subtitle = transform.Find("Motion/70_SubtitleTMP")?.GetComponent<TMP_Text>();
            message = transform.Find("Motion/60_MessageTMP")?.GetComponent<TMP_Text>();
            interaction = GetComponent<CurtainCall.UI.InteractionV1.InteractionPromptView>() != null;
            if (interaction)
            {
                title = transform.Find("Motion/70_ActionTMP")?.GetComponent<TMP_Text>();
                subtitle = transform.Find("Motion/80_HelperTMP")?.GetComponent<TMP_Text>();
                key = transform.Find("Motion/60_KeyTMP")?.GetComponent<TMP_Text>();
                if (key != null) { keyFontSize = key.fontSize; keyOverflow = key.overflowMode; keyBaseWidth = key.rectTransform.rect.width; }
                foreach (string path in new[] { "Motion/40_KeyBody", "Motion/41_KeyFrame", "Motion/50_HoldTrack", "Motion/51_HoldFill", "Motion/60_KeyTMP" })
                    if (transform.Find(path) is RectTransform rect) keyWidths[rect] = rect.rect.width;
            }
            HudText.Configure(title);
            HudText.Configure(subtitle);
            HudText.Configure(message);
            baseHeight = panel.rect.height;
            baseWidth = panel.rect.width;
            titleHeight = title != null ? title.rectTransform.rect.height : 0;
            subtitleHeight = subtitle != null ? subtitle.rectTransform.rect.height : 0;
            messageHeight = message != null ? message.rectTransform.rect.height : 0;
            // 기존 상단 위치를 보존하고 아래 방향으로만 늘린다.
            var position = panel.anchoredPosition;
            position.y += baseHeight * (1 - panel.pivot.y);
            panel.pivot = new Vector2(panel.pivot.x, 1);
            panel.anchoredPosition = position;
            initialized = true;
        }

        void LateUpdate()
        {
            if (banner)
            {
                var space = CommonUiSceneLoader.CurrentSpace;
                bool balanceAtTop = space != null && space.LocalBalance != null
                    && CurtainCall.Settings.GameSettings.Base.HudLayout.BalancePosition == CurtainCall.Settings.BaseGameSettings.BalanceHudPosition.TopCenter;
                HudLayout.PlaceBanner(panel, balanceAtTop, waitingRoom != null ? waitingRoom.TimerBannerTop : 0);
            }
            if (previousTitle != title?.text || previousSubtitle != subtitle?.text
                || previousMessage != message?.text || !Mathf.Approximately(previousWidth, panel.rect.width)
                || DevicePrompt && fittedDevice != DeviceMetrics())
                RefreshLayout();
        }

        public void RefreshLayout()
        {
            Initialize();
            float extra = 0;
            if (message != null)
                extra = Resize(message, messageHeight, deviceText ? 0 : 2) - messageHeight;
            else if (title != null && subtitle != null)
            {
                int lines = interaction ? deviceText ? 0 : PromptMaxLines : 2;
                if (interaction) FitPromptWidth();
                float titleExtra = Resize(title, titleHeight, lines) - titleHeight;
                bool hasSubtitle = subtitle.gameObject.activeSelf && !string.IsNullOrWhiteSpace(subtitle.text);
                float subtitleExtra = Resize(subtitle, subtitleHeight, lines) - subtitleHeight;
                title.rectTransform.anchoredPosition = interaction
                    ? new Vector2(TextLeft, hasSubtitle ? -12 : -24)
                    : new Vector2(72, hasSubtitle ? -18 : -32);
                subtitle.rectTransform.anchoredPosition = interaction
                    ? new Vector2(TextLeft, -44 - titleExtra)
                    : new Vector2(72, -62 - titleExtra);
                extra = titleExtra + (hasSubtitle ? subtitleExtra : 0);
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, baseHeight + extra);
            previousTitle = title?.text;
            previousSubtitle = subtitle?.text;
            previousMessage = message?.text;
            previousWidth = panel.rect.width;
            if (DevicePrompt) fittedDevice = DeviceMetrics();
        }

        /// <summary>최대 폭에서 줄바꿈한 뒤 가장 긴 줄에 맞춘다. 프리팹의 기본 폭보다 작아지지 않는다.</summary>
        void FitPromptWidth()
        {
            if (key != null)
            {
                key.enableAutoSizing = false; key.fontSize = keyFontSize;
                float extra = deviceText ? Mathf.Clamp(Mathf.Ceil(OneLineWidth(key)) + 8, keyBaseWidth, 160) - keyBaseWidth : 0;
                foreach (var rect in keyWidths) rect.Key.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rect.Value + extra);
                key.enableAutoSizing = deviceText; key.fontSizeMin = 14; key.fontSizeMax = keyFontSize;
                key.overflowMode = deviceText ? TextOverflowModes.Truncate : keyOverflow;
            }
            float available = Mathf.Max(1, PromptMaxWidth - TextLeft - PromptTextRight);
            float needed = Mathf.Max(HudText.Measure(title, available).x,
                subtitle.gameObject.activeSelf ? HudText.Measure(subtitle, available).x : 0);
            float width = Mathf.Clamp(Mathf.Ceil(needed) + TextLeft + PromptTextRight, baseWidth, PromptMaxWidth);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            float text = width - TextLeft - PromptTextRight;
            title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, text);
            subtitle.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, text);
        }

        static float OneLineWidth(TMP_Text label) =>
            string.IsNullOrEmpty(label.text) ? 0 : HudText.Measure(label).x;

        static float Resize(TMP_Text label, float minimum, int maxLines = 2)
        {
            // A progress row must stay visible even when the prose uses the usual line budget.
            if (HudText.HasProgress(label.text)) maxLines = 0;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = false;
            label.maxVisibleLines = maxLines > 0 ? maxLines : int.MaxValue;
            label.overflowMode = maxLines > 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            float width = Mathf.Max(1, label.rectTransform.rect.width);
            float preferred = HudText.Measure(label, width).y;
            float most = maxLines == 0 ? preferred : label.GetPreferredValues(maxLines == 2 ? "가\n가" : "가\n가\n가", width, Mathf.Infinity).y;
            float height = Mathf.Clamp(Mathf.Ceil(preferred), minimum, Mathf.Max(minimum, Mathf.Ceil(most)));
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return height;
        }
    }
}
