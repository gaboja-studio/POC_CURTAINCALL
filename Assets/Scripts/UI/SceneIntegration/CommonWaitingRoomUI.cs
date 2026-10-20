using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.CommonIcons;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.CaptionsV1;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>대기실 공통 표시. 동기화된 공개 상태를 읽으며 게임 시간·자금·소지 판정을 바꾸지 않는다.</summary>
    public sealed class CommonWaitingRoomUI : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/UI/CommonWaitingRoomUI.unity";
        [SerializeField] BackstageHUDView hud;
        [SerializeField] CaptionView captions;
        [SerializeField] SystemNotificationsView notifications;
        [SerializeField] CommonIconCatalog icons;
        [SerializeField, Min(.5f)] float inventorySeconds = 4f;
        [SerializeField, Min(.5f)] float announcementSeconds = 4f;
        [SerializeField, Min(.1f)] float fundsAccentSeconds = 1f;
        ExplorationTimerAnnouncer announcer;
        PropCarry carry;
        bool oldLegacyHud, fundsReceived, wasActive;
        long lastFunds;
        float captionUntil, fundsUntil;
        CampaignPhase lastPhase;
        public BackstageHUDView Hud => hud;

        public void Configure(BackstageHUDView view, CaptionView caption, SystemNotificationsView messages, CommonIconCatalog catalog)
        { hud = view; captions = caption; notifications = messages; icons = catalog; }

        void LateUpdate()
        {
            var space = CommonUiSceneLoader.CurrentWaitingRoom;
            var run = CampaignRun.Instance;
            bool active = space != null && run != null && (run.State.Phase == CampaignPhase.RestTime || run.State.Phase == CampaignPhase.Night);
            AttachAnnouncer(active ? space.Announcer : null);
            AttachCarry(active ? PropCarry.Instance : null);
            if (hud == null) return;
            if (!active)
            {
                hud.SetContext(false, false, false, false);
                if (wasActive) { captions.HideImmediate(); notifications.HideAllImmediate(); }
                wasActive = false; fundsReceived = false;
                return;
            }
            var state = run.State;
            bool rest = state.Phase == CampaignPhase.RestTime;
            if ((!wasActive || state.Phase != lastPhase) && state.Phase == CampaignPhase.Night)
                notifications.Banner.Show("밤 정비", "대기실 문이 잠겼습니다 · 탐색 시간 없음", BannerState.Normal, announcementSeconds);
            wasActive = true; lastPhase = state.Phase;
            if (Time.unscaledTime >= captionUntil) captions.Hide();
            hud.SetContext(true, rest, rest && space.LocalInGatherArea, false);
            if (rest)
            {
                var rules = GameSettings.Base.Run;
                float total = state.Rest == RestKind.Guide ? rules.GuideRestDuration : rules.RestDuration;
                float remaining = Mathf.Max(0, state.TimeRemaining);
                var clockState = state.Timer != RestTimerState.Running ? BackstageClockState.Paused : state.AlarmRaised ? BackstageClockState.Alarm : BackstageClockState.Normal;
                hud.Clock.SetClock(remaining, total > 0 ? Mathf.Clamp01(remaining / total) : 0, clockState);
            }
            var rewards = PerformanceRewards.Instance;
            if (rewards == null || rewards.State == null) { hud.Funds.SetUnavailable(); fundsReceived = false; }
            else
            {
                long amount = rewards.Balance;
                if (fundsReceived && amount < lastFunds) fundsUntil = Time.unscaledTime + fundsAccentSeconds;
                hud.Funds.SetAmount(amount);
                hud.Funds.SetChangeAccent(Time.unscaledTime < fundsUntil ? FundsChangeAccent.Decrease : FundsChangeAccent.None);
                lastFunds = amount; fundsReceived = true;
            }
            if (space.Gather != null) hud.Gather.SetCounts(space.Gather.InsideCount, space.Gather.AliveCount);
            else hud.Gather.SetUnavailable();
            hud.Inventory.HideInventory(); // CommonInventoryUI owns C5 across gameplay spaces.
        }

        void AttachAnnouncer(ExplorationTimerAnnouncer next)
        {
            if (ReferenceEquals(announcer, next)) return;
            if (announcer != null) announcer.Announced -= PresentAnnouncement;
            announcer = next;
            if (announcer != null) announcer.Announced += PresentAnnouncement;
        }

        public void PresentAnnouncement(ExplorationAnnouncement message)
        {
            captions.TryShowBroadcast("방송", message.Message);
            captionUntil = Time.unscaledTime + announcementSeconds;
            if (message.IsRecallAlarm)
                notifications.Banner.Show("소집 경보!", "무대 입구로 모이세요", BannerState.Danger, announcementSeconds, BannerIcon.Megaphone);
        }

        void AttachCarry(PropCarry next)
        {
            if (ReferenceEquals(carry, next)) return;
            if (carry != null)
            {
                carry.ShowLegacyHud = oldLegacyHud;
                carry.RequestAnswered -= PresentCarryMessage;
            }
            carry = next;
            if (carry != null)
            {
                oldLegacyHud = carry.ShowLegacyHud;
                carry.ShowLegacyHud = false;
                carry.RequestAnswered += PresentCarryMessage;
            }
            if (hud != null) hud.Inventory.HideInventory();
        }

        void PresentCarryMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) notifications.Toast.Show(message, ToastState.Normal, announcementSeconds);
        }

        void OnDisable()
        {
            AttachAnnouncer(null); AttachCarry(null);
            if (hud != null) hud.ResetForRuntime();
            if (captions != null) captions.HideImmediate();
            if (notifications != null) notifications.HideAllImmediate();
            wasActive = false; fundsReceived = false;
        }
    }
}
