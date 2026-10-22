using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using CurtainCall.Tightrope.UI;
using CurtainCall.UI.NotificationsV1;
using UnityEngine;
namespace CurtainCall.UI.SceneIntegration
{
    public sealed class TightropeControlsPanel : MonoBehaviour
    {
        public SystemBannerView banner;
        [SerializeField] float holdSeconds = 4f;
        [SerializeField] float sawDistance = 8f;
        readonly TightropeGuideProgress progress = new();
        void OnEnable() => TightropeControlHints.ExternalPresentation = true;
        void LateUpdate()
        {
            if (banner == null) return;
            var local = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            var campaign = CampaignRun.Instance;
            var run = TightropeRun.Current;
            if (local == null || course == null || campaign == null || !TightropeGuideProgress.IsFirstDay(campaign.State)
                || !course.IsGameplayActive || run == null || run.State != TightropeRunState.Running
                || local.State != PlayerState.Normal || InputGate.IsBlocked || SceneCurtainTransition.IsActive)
            { if (banner.IsVisible) banner.HideImmediate(); return; }
            var state = campaign.State;
            if (progress.Begin(state.Generation, state.PerformanceIndex, run.Round, local.GetInstanceID())) banner.HideImmediate();
            // View에 큐가 없으므로 현재 알림이 끝난 뒤 다음 상황을 평가한다.
            if (banner.IsVisible) return;
            if (DeviceGuide(local, course)) return;
            var mover = local.GetComponent<PlayerMover>();
            if (mover == null || !mover.ControlEnabled) return;
            if (mover.FreeMovement)
            {
                ShowOnce("approach", "WASD로 줄에 올라가세요", "줄 위로 걸어가면 자동으로 올라탑니다");
                return;
            }
            if (ShowOnce("balance", "W/S로 이동하고 A/D로 중심을 잡아요", "기울어지는 반대 방향으로 균형을 잡으세요")) return;
            if (progress.LaneChangePending && ShowOnce("lane-change", "Q/E를 누른 채로 방향을 정하고 SPACE로 줄을 변경할 수 있습니다.")) return;
            var saws = TightropeSaws.Current;
            if (saws == null) return;
            float distance = course.GetDistance(local.transform.position);
            if (saws.IsVerticalActive && !saws.IsPlatePressed && Mathf.Abs(distance - saws.VerticalDistance) <= sawDistance
                && Obstacle("vertical:" + saws.VerticalSerial, "목마 혹은 옆으로 줄을 이동하여 톱날 뒤의 버튼을 누르세요")) return;
            if (saws.IsCooperativeGateActive && !saws.IsCooperativeGatePassable
                && Mathf.Abs(distance - saws.CooperativeGateDistance) <= sawDistance
                && Obstacle("gate:" + saws.CooperativeGateIndex, "목마 혹은 옆으로 줄을 이동하여 톱날 뒤의 버튼을 누르세요")) return;
            for (int i = 0; i < saws.HorizontalCount; i++)
            {
                if (!saws.IsHorizontalMoving(i) || !saws.TryGetHorizontalPosition(i, out var position)) continue;
                if (Vector3.Distance(local.transform.position, position) > sawDistance) continue;
                if (Obstacle("horizontal:" + i, "SPACE로 점프해서 톱날을 피해요", "다가오는 톱날에 닿지 않도록 주의하세요")) return;
            }
        }
        bool DeviceGuide(NetworkPlayer local, TightropeCourse course)
        {
            var relay = TightropeRelayGate.Current;
            if (relay != null && relay.IsActive)
            {
                if (relay.IsHeld(local))
                    return ShowOnce("relay-held", TightropeGuideProgress.HoldText(course.Shape.RelayHoldSeconds));
                float distance = course.GetDistance(local.transform.position);
                if (Mathf.Abs(distance - relay.EntryDistance) <= sawDistance || Mathf.Abs(distance - relay.ExitDistance) <= sawDistance)
                    if (ShowOnce("relay-approach", "F를 눌러 매달리고 길을 만드세요")) return true;
            }
            var trapeze = TightropeTrapeze.Current;
            if (trapeze == null || !trapeze.IsActive) return false;
            if (trapeze.IsHeld(local))
                return trapeze.IsLower(local)
                    ? ShowOnce("trapeze-lower", "타이밍에 맞춰 점프하여 다음 봉을 잡으세요")
                    : ShowOnce("trapeze-upper", "WS키를 통해 반동을 줍니다");
            if (trapeze.IsInFlight(local)) return false;
            if (Mathf.Abs(course.GetDistance(local.transform.position) - trapeze.EntryDistance) > sawDistance) return false;
            var stack = PiggybackSystem.Current;
            bool top = stack != null && stack.GetBelow(local) != null && stack.GetAbove(local) == null;
            if (!top && trapeze.HolderAt(0) != ulong.MaxValue && trapeze.PassengerAt(0) == ulong.MaxValue)
                return ShowOnce("trapeze-attach", "매달린 동료 가까이에서 F를 누르세요");
            return ShowOnce("trapeze-approach", "봉 가까이에서 F로 잡으세요 · 점프 중에도 가능합니다");
        }
        bool Obstacle(string key, string title, string subtitle = "")
        {
            if (!ShowOnce(key, title, subtitle, BannerState.Danger)) return false;
            progress.MarkShown("obstacle");
            return true;
        }
        bool ShowOnce(string key, string title, string subtitle = "", BannerState state = BannerState.Normal)
        {
            if (banner.IsVisible || progress.HasShown(key) || !banner.Show(title, subtitle, state, holdSeconds)) return false;
            progress.MarkShown(key);
            return true;
        }
        void OnDisable()
        {
            TightropeControlHints.ExternalPresentation = false;
            if (banner != null) banner.HideImmediate();
        }
    }

    public sealed class TightropeGuideProgress
    {
        readonly HashSet<string> shown = new();
        (int generation, int performance, int round, int player)? observed;
        public static bool IsFirstDay(CampaignSnapshot state) => state.Day == 1 && state.Phase == CampaignPhase.Performance
            && state.TrickId == "Tightrope";
        public static string HoldText(float seconds) => $"최대 {seconds:0.#}초 간 버틸 수 있습니다.";
        public bool LaneChangePending => HasShown("obstacle") && !HasShown("lane-change");
        public bool HasShown(string key) => shown.Contains(key);
        public void MarkShown(string key) => shown.Add(key);
        public bool Begin(int generation, int performance, int round, int player)
        {
            var next = (generation, performance, round, player);
            if (observed.HasValue && observed.Value == next) return false;
            observed = next;
            shown.Clear();
            return true;
        }
    }
}
