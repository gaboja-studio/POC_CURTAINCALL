using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using CurtainCall.Tightrope.UI;
using UnityEngine;
namespace CurtainCall.UI.SceneIntegration
{
    public sealed class TightropeControlsPanel : MonoBehaviour
    {
        readonly List<HudRoleCard> guides = new();
        float nextGuide;
        [SerializeField, Min(.1f)] float guideInterval = 1.5f;
        [SerializeField] float sawDistance = 8f;
        [SerializeField] bool escapeTraversal;
        [Header("탈출로 · 외줄에 올라섰을 때 차례로 표시")]
        [SerializeField] PerformanceHudProfile.Cue escapeBalance = new("외줄에서 균형 잡기", "{forward}로 이동하고 {balance}로 기울어지는 반대 방향을 누르세요.", "{balance}");
        [SerializeField] PerformanceHudProfile.Cue escapeJump = new("외줄에서 점프하기", "{jump}로 점프하세요. 동료 가까이에서 {interact}로 목마를 타거나, 길게 눌러 매달린 동료를 구조하세요.", "{jump}");
        public void ConfigureEscape() { escapeTraversal = true; }
        readonly TightropeGuideProgress progress = new();
        void OnEnable() => TightropeControlHints.ExternalPresentation = true;
        void LateUpdate()
        {
            var local = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            var campaign = CampaignRun.Instance;
            var run = TightropeRun.Current;
            bool context = escapeTraversal ? CurtainCall.EscapeRoute.EscapeCampaignContent.IsTraversalActive
                : campaign != null && TightropeGuideProgress.IsFirstDay(campaign.State);
            if (local == null || course == null || !context
                || !course.IsGameplayActive || run == null || run.State != TightropeRunState.Running
                || local.State != PlayerState.Normal || InputGate.IsBlocked || SceneCurtainTransition.IsActive)
            { ClearGuides(); return; }
            var state = campaign != null ? campaign.State : default;
            if (progress.Begin(state.Generation, state.PerformanceIndex, run.Round, local.GetInstanceID())) ClearGuides();
            if (Time.unscaledTime < nextGuide) return;
            if (escapeTraversal)
            {
                if (!course.UsesRopeRules(local)) return;
                if (Lesson("escape-balance", escapeBalance)) return;
                Lesson("escape-jump", escapeJump);
                return;
            }
            if (DeviceGuide(local, course)) return;
            var mover = local.GetComponent<PlayerMover>();
            if (mover == null || !mover.ControlEnabled) return;
            if (mover.FreeMovement)
            {
                Lesson("approach", Profile.approach);
                return;
            }
            if (Lesson("balance", Profile.balance)) return;
            if (progress.LaneChangePending && Lesson("lane-change", Profile.lane)) return;
            var saws = TightropeSaws.Current;
            if (saws == null) return;
            float distance = course.GetDistance(local.transform.position);
            if (saws.IsVerticalActive && !saws.IsPlatePressed && Mathf.Abs(distance - saws.VerticalDistance) <= sawDistance
                && Lesson("vertical:" + saws.VerticalSerial, Profile.sawGate, true)) return;
            if (saws.IsCooperativeGateActive && !saws.IsCooperativeGatePassable
                && Mathf.Abs(distance - saws.CooperativeGateDistance) <= sawDistance
                && Lesson("gate:" + saws.CooperativeGateIndex, Profile.sawGate, true)) return;
            for (int i = 0; i < saws.HorizontalCount; i++)
            {
                if (!saws.IsHorizontalMoving(i) || !saws.TryGetHorizontalPosition(i, out var position)) continue;
                if (Vector3.Distance(local.transform.position, position) > sawDistance) continue;
                if (Lesson("horizontal:" + i, Profile.sawJump, true)) return;
            }
        }
        PerformanceHudProfile Profile => PerformanceHudProfile.Current;
        bool Lesson(string key, PerformanceHudProfile.Cue cue, bool obstacle = false)
        {
            if (progress.HasShown(key)) return false;
            var reader=NetworkPlayer.Local ? NetworkPlayer.Local.GetComponent<PlayerInputReader>() : null;
            bool shown=Display(key, PerformanceHudProfile.Format(cue.title,reader), PerformanceHudProfile.Format(cue.body,reader), PerformanceHudProfile.Format(cue.badge,reader));
            if (shown && obstacle) progress.MarkShown("obstacle");
            return shown;
        }
        bool DeviceGuide(NetworkPlayer local, TightropeCourse course)
        {
            var relay = TightropeRelayGate.Current;
            if (relay != null && relay.IsActive)
            {
                if (relay.IsHeld(local))
                    return Lesson("relay-held", Profile.relayHeld);
                float distance = course.GetDistance(local.transform.position);
                if (Mathf.Abs(distance - relay.EntryDistance) <= sawDistance || Mathf.Abs(distance - relay.ExitDistance) <= sawDistance)
                    if (Lesson("relay-approach", Profile.relayNear)) return true;
            }
            var trapeze = TightropeTrapeze.Current;
            if (trapeze == null || !trapeze.IsActive) return false;
            if (trapeze.IsHeld(local))
                return trapeze.IsLower(local)
                    ? Lesson("trapeze-lower", Profile.lower)
                    : Lesson("trapeze-upper", Profile.upper);
            if (trapeze.IsInFlight(local)) return false;
            if (Mathf.Abs(course.GetDistance(local.transform.position) - trapeze.EntryDistance) > sawDistance) return false;
            var stack = PiggybackSystem.Current;
            bool top = stack != null && stack.GetBelow(local) != null && stack.GetAbove(local) == null;
            if (!top && trapeze.HolderAt(0) != ulong.MaxValue && trapeze.PassengerAt(0) == ulong.MaxValue)
                return Lesson("trapeze-attach", Profile.attach);
            return Lesson("trapeze-approach", Profile.pole);
        }
        bool Display(string key,string title,string subtitle,string badge)
        {
            guides.RemoveAll(c => c == null || c.Closing);
            guides.Add(string.IsNullOrEmpty(subtitle) ? HudUIManager.Current.ShowGuide(title,transform,key:badge)
                : HudUIManager.Current.ShowGuide(subtitle,transform,title,key:badge));
            nextGuide = Time.unscaledTime + guideInterval;
            progress.MarkShown(key);
            return true;
        }
        void ClearGuides()
        {
            foreach (var c in guides) if (c != null && !c.Closing) c.Close(true);
            guides.Clear(); nextGuide = 0;
        }
        void OnDisable()
        {
            TightropeControlHints.ExternalPresentation = false;
            ClearGuides();
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
