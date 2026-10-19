using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>F로 안전한 줄에서 받기 준비. 호스트가 준비 상태를 공유하고 한 번의 착지를 확인한다.</summary>
    [DefaultExecutionOrder(-45)]
    public sealed partial class TightropeCatchSystem : MonoBehaviour
    {
        struct ReadyWindow { public double Until; public Vector3 Position; }
        readonly Dictionary<ulong, ReadyWindow> ready = new();
        readonly Dictionary<ulong, double> recentLaneJumps = new();
        readonly Dictionary<ulong, (ulong Target, double Until)> pendingLandings = new();
        readonly List<ulong> expired = new();
        int round = -1;
        bool wasActive;
        double nextRequestAt;
        string feedback;
        double feedbackUntil;
        GUIStyle labelStyle;
        public static TightropeCatchSystem Current { get; private set; }
        public float LocalReadyRemaining => Remaining(NetworkPlayer.Local);
        public string LocalFeedback => Now < feedbackUntil ? feedback : null;
        public float CatchReduction => GameSettings.Tightrope.Handhold.ActiveReduction;
        bool Active => isActiveAndEnabled && TightropeCourse.Current != null
            && TightropeCourse.Current.IsGameplayActive && TightropeRun.Current != null
            && TightropeRun.Current.State == TightropeRunState.Running;
        double Now => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        void OnEnable()
        {
            Current = this;
            NetworkPlayer.Restarted += OnRestarted;
        }

        void OnDisable()
        {
            NetworkPlayer.Restarted -= OnRestarted;
            Disconnect();
            Clear();
            wasActive = false;
            if (Current == this) Current = null;
        }

        void Clear()
        {
            ready.Clear();
            recentLaneJumps.Clear();
            pendingLandings.Clear();
            feedback = null;
            feedbackUntil = nextRequestAt = 0;
        }

        void OnRestarted(NetworkPlayer _) => Clear();

        void Update()
        {
            Connect();
            int currentRound = TightropeRun.Current != null ? TightropeRun.Current.Round : -1;
            if (round != currentRound)
            {
                Clear();
                round = currentRound;
                Send(Request.Sync);
            }
            bool active = Active;
            if (active && !wasActive) Send(Request.Sync);
            wasActive = active;
            if (!active)
            {
                if (ready.Count > 0) { Clear(); if (session != null && session.IsHost) Publish(); }
                return;
            }
            if (LocalReadyRemaining > 0f && InputGate.IsBlocked) CancelLocal();
            if (session == null || !session.IsHost) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.CurrentJump == JumpKind.Lane)
                    recentLaneJumps[player.OwnerClientId] = Now;
            expired.Clear();
            foreach (var item in ready)
            {
                var player = Find(item.Key);
                if (Now >= item.Value.Until || !CanPrepare(player)
                    || Vector3.Distance(player.transform.position, item.Value.Position) > .15f)
                    expired.Add(item.Key);
            }
            foreach (ulong id in expired) ready.Remove(id);
            if (expired.Count > 0) Publish();
            expired.Clear();
            foreach (var pending in pendingLandings)
                if (TryCompleteLanding(pending.Key, pending.Value.Target) || Now >= pending.Value.Until)
                    expired.Add(pending.Key);
            foreach (ulong id in expired) pendingLandings.Remove(id);
        }

        public bool IsReady(NetworkPlayer player) => Remaining(player) > 0f;
        float Remaining(NetworkPlayer player) => Active && player != null
            && ready.TryGetValue(player.OwnerClientId, out var state) && CanPrepare(player)
            && Vector3.Distance(player.transform.position, state.Position) <= .15f
                ? Mathf.Max(0f, (float)(state.Until - Now)) : 0f;

        bool CanPrepare(NetworkPlayer player)
        {
            if (!Active || player == null || !player.IsSpawned || player.State != PlayerState.Normal
                || player.IsAirborne || !player.TryGetComponent(out PlayerMover mover) || mover.PassThrough) return false;
            if (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return false;
            var stack = PiggybackSystem.Current;
            if (stack != null && stack.IsInStack(player)) return false;
            var course = TightropeCourse.Current;
            return course.TryGetSupportingRope(mover, out int supportLane)
                && course.TryGetRopePoint(player.transform.position, out int lane, out float distance) && supportLane == lane
                && course.IsLaneUsable(lane, distance)
                && Mathf.Abs(player.transform.position.y - course.GetLanePosition(lane, distance).y) <= .3f
                && Mathf.Abs(course.GetSide(player.transform.position) - course.GetSide(course.GetLanePosition(lane, distance))) <= .35f;
        }

        /// <summary>가까운 동료에게 F 목마 타기는 유지하고, 빈 자리에서 F는 받기 준비/취소다.</summary>
        public bool TryHandleLocalInteract()
        {
            var local = NetworkPlayer.Local;
            if (!CanPrepare(local)) return false;
            if (IsReady(local)) { CancelLocal(); return true; }
            if (HasMountTarget(local)) return false;
            Send(Request.Prepare);
            return true;
        }

        bool HasMountTarget(NetworkPlayer local)
        {
            float reach = GameSettings.Tightrope.Piggyback.LinkDistance;
            if (local.TryGetComponent(out PlayerInteraction interaction)) reach *= interaction.ReachMultiplier;
            foreach (var other in NetworkPlayer.All)
            {
                if (other == null || other == local || other.State != PlayerState.Normal) continue;
                if (PiggybackSystem.Current != null && PiggybackSystem.Current.GetBelow(other) != null) continue;
                Vector3 offset = other.transform.position - local.transform.position;
                offset.y = 0;
                if (offset.magnitude <= reach) return true;
            }
            return false;
        }

        public void CancelLocal()
        {
            if (NetworkPlayer.Local == null) return;
            bool existed = ready.ContainsKey(NetworkPlayer.Local.OwnerClientId);
            if (session == null || !session.IsHost) ready.Remove(NetworkPlayer.Local.OwnerClientId);
            if (existed) Send(Request.Cancel);
        }

        public bool TryGetCatcher(NetworkPlayer jumper, int lane, float distance, out NetworkPlayer catcher)
        {
            catcher = null;
            var course = TightropeCourse.Current;
            if (!Active || jumper == null || course == null || lane == TightropeCourse.NoLane
                || !course.IsLaneUsable(lane, distance)) return false;
            float best = GameSettings.Tightrope.Handhold.ActiveDistance;
            foreach (var other in NetworkPlayer.All)
            {
                if (other == jumper || !IsReady(other)
                    || !course.TryGetRopePoint(other.transform.position, out int otherLane, out float otherDistance)
                    || otherLane != lane) continue;
                float delta = Mathf.Abs(otherDistance - distance);
                if (delta > best) continue;
                best = delta;
                catcher = other;
            }
            return catcher != null;
        }

        public void ReportLanding(NetworkPlayer catcher)
        {
            if (catcher != null) Send(Request.Landed, catcher.OwnerClientId);
        }

        void ServerHandle(ulong sender, Request request, ulong target)
        {
            if (request == Request.Sync) { Publish(sender); return; }
            if (!Active) return;
            var player = Find(sender);
            if (request == Request.Cancel) { if (ready.Remove(sender)) Publish(); return; }
            if (request == Request.Prepare)
            {
                if (!CanPrepare(player) || ready.ContainsKey(sender)) return;
                ready[sender] = new ReadyWindow {
                    Until = Now + GameSettings.Tightrope.Handhold.ReadySeconds, Position = player.transform.position
                };
                Publish();
                return;
            }
            if (request == Request.Landed && player != null && player.State == PlayerState.Normal && IsReady(Find(target)))
            {
                // Jump state and owner transform can arrive after the reliable landing request.
                // The short host-side window retries validation, never trusting a claimed location.
                if (!TryCompleteLanding(sender, target) && !pendingLandings.ContainsKey(sender))
                    pendingLandings[sender] = (target, Now + .35);
            }
        }

        bool TryCompleteLanding(ulong sender, ulong target)
        {
            var player = Find(sender);
            var helper = Find(target);
            var course = TightropeCourse.Current;
            if (player == null || player == helper || player.State != PlayerState.Normal
                || !IsReady(helper) || !recentLaneJumps.TryGetValue(sender, out double jumpedAt) || Now - jumpedAt > 1.0
                || !player.TryGetComponent(out PlayerMover mover) || (player.IsLocal ? mover.IsAirborne : player.IsAirborne)
                || !course.TryGetSupportingRope(mover, out int supportingLane)
                || !course.TryGetRopePoint(player.transform.position, out int lane, out float distance)
                || supportingLane != lane
                || !course.TryGetRopePoint(helper.transform.position, out int helperLane, out float helperDistance)
                || helperLane != lane || Mathf.Abs(helperDistance - distance) > GameSettings.Tightrope.Handhold.ActiveDistance
                || Mathf.Abs(player.transform.position.y - course.GetLanePosition(lane, distance).y) > .15f
                || Mathf.Abs(course.GetSide(player.transform.position) - course.GetSide(course.GetLanePosition(lane, distance))) > .35f) return false;
            ready.Remove(target);
            recentLaneJumps.Remove(sender);
            Publish();
            PublishSuccess(sender, target);
            return true;
        }

        static NetworkPlayer Find(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.OwnerClientId == id) return player;
            return null;
        }

        void ShowSuccess(ulong jumper, ulong catcher)
        {
            var local = NetworkPlayer.Local;
            if (local == null || (local.OwnerClientId != jumper && local.OwnerClientId != catcher)) return;
            feedback = local.OwnerClientId == jumper ? "동료가 받아줬어요 — 착지 흔들림 감소" : "동료를 받아줬어요!";
            feedbackUntil = Now + 2.5;
        }

        void OnGUI()
        {
            if (!Active || Camera.main == null || InputGate.IsBlocked) return;
            labelStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            foreach (var other in NetworkPlayer.All)
            {
                float left = Remaining(other);
                if (left <= 0f) continue;
                Vector3 point = Camera.main.WorldToScreenPoint(other.transform.position + Vector3.up * 2.1f);
                if (point.z <= 0f) continue;
                GUI.Box(new Rect(point.x - 80, Screen.height - point.y - 15, 160, 30), $"받을 준비 {left:0.0}초", labelStyle);
            }
        }
    }
}
