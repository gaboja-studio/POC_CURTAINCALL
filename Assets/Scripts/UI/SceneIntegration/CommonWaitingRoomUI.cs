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
        [SerializeField] DayClockView dayClock;
        [SerializeField] RestTimerView restTimer;
        bool oldLegacyHud, fundsReceived, wasActive;
        long lastFunds;
        float fundsUntil;
        CampaignPhase lastPhase, clockPhase;
        RestKind clockRest;
        int clockDay, clockPerformance;
        float clockTotal;
        bool clockInitialized;
        public BackstageHUDView Hud => hud;
        /// <summary>이전 하루 시계의 직렬화 참조. 호환용으로 보존하며 실제 표시에서는 비활성화한다.</summary>
        public DayClockView DayClockDisplay => dayClock;
        public RestTimerView RestTimerDisplay => restTimer;
        public float TimerBannerTop => restTimer != null && ((RectTransform)restTimer.transform).anchorMin.x == .5f
            ? -((RectTransform)restTimer.transform).anchoredPosition.y + ((RectTransform)restTimer.transform).rect.height + 16f : 0f;

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
            if (dayClock != null) dayClock.gameObject.SetActive(false);
            if (!active)
            {
                hud.SetContext(false, false, false, false);
                if (dayClock != null) dayClock.Hide();
                if (restTimer != null) restTimer.Hide();
                if (wasActive) { captions.HideImmediate(); notifications.HideAllImmediate(); }
                wasActive = false; fundsReceived = false; clockInitialized = false;
                return;
            }
            var state = run.State;
            bool rest = state.Phase == CampaignPhase.RestTime;
            if ((!wasActive || state.Phase != lastPhase) && state.Phase == CampaignPhase.Night)
                notifications.Banner.Show(WaitingRoomText.Get("banner.night-title", "밤 정비"), state.Timer == RestTimerState.Unlimited
                    ? WaitingRoomText.Get("banner.night-unlimited", "대기실 문이 잠겼습니다 · 제한 시간 없음") : WaitingRoomText.Get("banner.night-timed", "대기실 문이 잠겼습니다 · 정비 시간 시작"), BannerState.Normal, announcementSeconds);
            wasActive = true; lastPhase = state.Phase;
            // 기존 두 W1은 숨기고 v2.8 남은 시간/무한대 타이머만 표시한다.
            hud.SetContext(true, false, rest && space.LocalInGatherArea, true);
            var rules = GameSettings.Base.Run;
            if (!clockInitialized || clockPhase != state.Phase || clockRest != state.Rest
                || clockDay != state.Day || clockPerformance != state.PerformanceIndex)
            {
                clockTotal = (rest ? rules.TimeLimit(state.Rest) : rules.NightMaintenance).Seconds;
                clockPhase = state.Phase; clockRest = state.Rest; clockDay = state.Day; clockPerformance = state.PerformanceIndex;
                clockInitialized = true;
            }
            if (EnsureRestTimer()) { ArrangeTimerLayout(); restTimer.Show(state, clockTotal); }
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

        // 새 공통 씬은 직렬화된 인스턴스를 사용하고, 구형 구성만 같은 프리팹으로 보완한다.
        bool EnsureRestTimer()
        {
            if (restTimer != null) return true;
            if (hud == null || hud.Clock == null) return false;
            var old = (RectTransform)hud.Clock.transform;
            var prefab = Resources.Load<RestTimerView>(RestTimerView.PrefabPath);
            if (prefab == null) return false;
            restTimer = Instantiate(prefab, old.parent);
            return true;
        }

        /// <summary>중앙 W1과 방송 배너가 겹치지 않도록 같은 Canvas 단위로 간격을 둔다.</summary>
        public void ArrangeTimerLayout()
        {
            if (restTimer == null || notifications == null || notifications.Banner == null) return;
            var banner = (RectTransform)notifications.Banner.transform;
            HudLayout.PlaceBanner(banner, false, TimerBannerTop);
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
            var banner = notifications.Banner;
            var canvas = banner.GetComponent<Canvas>();
            if (canvas == null) canvas = banner.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            foreach (var layer in SortingLayer.layers)
                if (SortingLayer.GetLayerValueFromID(layer.id) > SortingLayer.GetLayerValueFromID(canvas.sortingLayerID))
                    canvas.sortingLayerID = layer.id;
            banner.Show(message.IsRecallAlarm ? WaitingRoomText.Get("banner.recall-title", "소집 경보!") : WaitingRoomText.Get("banner.broadcast-title", "방송"),
                message.IsRecallAlarm ? WaitingRoomText.Get("banner.recall-body", "무대 입구로 모이세요") : message.Message,
                message.IsRecallAlarm ? BannerState.Danger : BannerState.Normal,
                announcementSeconds, BannerIcon.Megaphone);
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
            if (dayClock != null) dayClock.Hide();
            if (restTimer != null) restTimer.Hide();
            if (captions != null) captions.HideImmediate();
            if (notifications != null) notifications.HideAllImmediate();
            wasActive = false; fundsReceived = false; clockInitialized = false;
        }
    }
}
