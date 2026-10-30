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
        float baseHeight, titleHeight, subtitleHeight, messageHeight;
        string previousTitle, previousSubtitle, previousMessage;
        float previousWidth;
        bool initialized, interaction, banner;
        CommonWaitingRoomUI waitingRoom;

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
            }
            baseHeight = panel.rect.height;
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
                || previousMessage != message?.text || !Mathf.Approximately(previousWidth, panel.rect.width))
                RefreshLayout();
        }

        public void RefreshLayout()
        {
            Initialize();
            float extra = 0;
            if (message != null)
                extra = Resize(message, messageHeight) - messageHeight;
            else if (title != null && subtitle != null)
            {
                float titleExtra = Resize(title, titleHeight) - titleHeight;
                bool hasSubtitle = subtitle.gameObject.activeSelf && !string.IsNullOrWhiteSpace(subtitle.text);
                float subtitleExtra = Resize(subtitle, subtitleHeight) - subtitleHeight;
                title.rectTransform.anchoredPosition = interaction
                    ? new Vector2(92, hasSubtitle ? -12 : -24)
                    : new Vector2(72, hasSubtitle ? -18 : -32);
                subtitle.rectTransform.anchoredPosition = interaction
                    ? new Vector2(92, -44 - titleExtra)
                    : new Vector2(72, -62 - titleExtra);
                extra = titleExtra + (hasSubtitle ? subtitleExtra : 0);
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, baseHeight + extra);
            previousTitle = title?.text;
            previousSubtitle = subtitle?.text;
            previousMessage = message?.text;
            previousWidth = panel.rect.width;
        }

        static float Resize(TMP_Text label, float minimum)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = false;
            label.maxVisibleLines = 2;
            label.overflowMode = TextOverflowModes.Ellipsis;
            float width = Mathf.Max(1, label.rectTransform.rect.width);
            float twoLines = label.GetPreferredValues("가\n가", width, Mathf.Infinity).y;
            float preferred = label.GetPreferredValues(label.text, width, Mathf.Infinity).y;
            float height = Mathf.Clamp(Mathf.Ceil(preferred), minimum, Mathf.Max(minimum, Mathf.Ceil(twoLines)));
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return height;
        }
    }
}
