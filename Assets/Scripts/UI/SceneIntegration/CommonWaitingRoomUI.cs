using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.CommonIcons;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
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
        WaitingRoomShop shop;
        ExplorationState exploration;
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
        WaitingRoomUiBinding observedDeathSpace;
        readonly Dictionary<NetworkPlayer,bool> observedDeaths = new();
        readonly List<string> newDeaths = new();
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
            ObserveDeaths(space);
            var run = CampaignRun.Instance;
            bool active = space != null && run != null && !WaitingRoomCannonRehearsal.IsActive
                && (run.State.Phase == CampaignPhase.RestTime || run.State.Phase == CampaignPhase.Night);
            AttachAnnouncer(active ? space.Announcer : null);
            AttachCarry(active ? PropCarry.Instance : null);
            AttachOthers(active ? WaitingRoomShop.Instance : null, active ? ExplorationState.Instance : null);
            if (hud == null) return;
            if (dayClock != null) dayClock.gameObject.SetActive(false);
            if (!active)
            {
                hud.SetContext(false, false, false, false);
                if (dayClock != null) dayClock.Hide();
                if (restTimer != null) restTimer.Hide();
                if (wasActive) { DismissMessages(); captions.HideImmediate(); notifications.HideAllImmediate(); }
                wasActive = false; fundsReceived = false; clockInitialized = false;
                return;
            }
            var state = run.State;
            bool rest = state.Phase == CampaignPhase.RestTime;
            if ((!wasActive || state.Phase != lastPhase) && state.Phase == CampaignPhase.Night)
                WaitingRoomHudProfile.Emit(WaitingRoomHudProfile.Current.nightBegins,transform,WaitingRoomText.Get("banner.night-title","밤 정비"),
                    state.Timer==RestTimerState.Unlimited ? WaitingRoomText.Get("banner.night-unlimited","대기실 문이 잠겼습니다 · 제한 시간 없음") : WaitingRoomText.Get("banner.night-timed","대기실 문이 잠겼습니다 · 정비 시간 시작"),announcementSeconds);
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

        HudRoleCard currentBroadcast;
        public HudRoleCard CurrentBroadcast => currentBroadcast;
        void AttachAnnouncer(ExplorationTimerAnnouncer next)
        {
            if (ReferenceEquals(announcer, next)) return;
            if (announcer != null) announcer.Announced -= PresentAnnouncement;
            announcer = next;
            if (announcer != null) announcer.Announced += PresentAnnouncement;
        }

        public void PresentAnnouncement(ExplorationAnnouncement message)
        {
            var config=WaitingRoomHudProfile.Current;
            currentBroadcast=WaitingRoomHudProfile.Emit(message.IsRecallAlarm?config.recall:config.broadcast,transform,
                message.Title ?? (message.IsRecallAlarm ? WaitingRoomText.Get("banner.recall-title","소집 경보!") : WaitingRoomText.Get("banner.broadcast-title","방송")),
                message.Message,announcementSeconds);
            if(currentBroadcast.TryGetComponent<HudEventAppearance>(out var appearance))appearance.PrioritizeBroadcast();
            if(message.IsRecallAlarm)WaitingRoomTutorial.RecallPresented();
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

        // 배송구 수령·열쇠 줍기는 상점·탐색 채널로 답이 온다. 손이 가득 찬 것 같은 기물 거절만 같은 알림으로 띄운다(2026-11-01).
        void AttachOthers(WaitingRoomShop nextShop, ExplorationState nextExploration)
        {
            if (!ReferenceEquals(shop, nextShop))
            {
                if (shop != null) shop.RequestAnswered -= PresentShopRefusal;
                shop = nextShop;
                if (shop != null) shop.RequestAnswered += PresentShopRefusal;
            }
            if (!ReferenceEquals(exploration, nextExploration))
            {
                if (exploration != null) exploration.RequestAnswered -= PresentCarryRefusal;
                exploration = nextExploration;
                if (exploration != null) exploration.RequestAnswered += PresentCarryRefusal;
            }
        }

        void PresentShopRefusal(bool ok, string message) { if (!ok) PresentCarryRefusal(message); }
        void PresentCarryRefusal(string message) { if (PropCarry.IsRefusalText(message)) PresentCarryMessage(message); }

        void PresentCarryMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) WaitingRoomHudProfile.Emit(WaitingRoomHudProfile.Current.carryResult,transform,"",message,announcementSeconds);
        }

        void ObserveDeaths(WaitingRoomUiBinding space)
        {
            if (observedDeathSpace != space)
            {
                if (observedDeathSpace) HudUIManager.DismissOwned(transform,HudRole.SystemEvent);
                observedDeathSpace = space;
                observedDeaths.Clear();
            }
            if (!space) return;
            newDeaths.Clear();
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned) continue;
                bool dead = player.State is PlayerState.Fallen or PlayerState.Retired;
                if (dead && observedDeaths.TryGetValue(player,out bool wasDead) && !wasDead)
                    newDeaths.Add(player == NetworkPlayer.Local ? "나" : player.Slot >= 0
                        ? $"단원 {player.Slot + 1}" : WaitingRoomShop.PlayerLabel(player.OwnerClientId));
                observedDeaths[player] = dead;
            }
            if (newDeaths.Count > 0)
                HudUIManager.Current.ShowDeathWarning("",transform,string.Join(", ",newDeaths) + " 사망",announcementSeconds);
        }

        void OnDisable()
        {
            DismissMessages();
            observedDeathSpace = null;
            observedDeaths.Clear();
            AttachAnnouncer(null); AttachCarry(null); AttachOthers(null, null);
            if (hud != null) hud.ResetForRuntime();
            if (dayClock != null) dayClock.Hide();
            if (restTimer != null) restTimer.Hide();
            if (captions != null) captions.HideImmediate();
            if (notifications != null) notifications.HideAllImmediate();
            wasActive = false; fundsReceived = false; clockInitialized = false;
        }
        void DismissMessages()
        {
            foreach(var role in new[]{HudRole.SystemEvent,HudRole.Guide,HudRole.Feedback})HudUIManager.DismissOwned(transform,role);
        }
    }
}
