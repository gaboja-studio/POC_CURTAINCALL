using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>출발 쪽 봉으로 먼저 건너고 반대편 봉으로 남은 동료의 줄을 연결한다. 점유는 호스트가 판정한다.</summary>
    [DefaultExecutionOrder(-55)]
    public sealed partial class TightropeRelayGate : MonoBehaviour
    {
        const ulong Nobody = ulong.MaxValue;
        sealed class Station
        {
            public PoleGateState State = new();
            public HumanCannonPole Pole;
            public int Lane;
            public bool Far;
            // 잡을 때 호스트가 고른 연결 줄(0부터). -1이면 아직 연 줄이 없다.
            public int Opened = -1;
        }
        const int NoLane = -1;
        static readonly List<int> laneCandidates = new();
        readonly Station[] stations = new Station[4];
        TightropeCourse course;
        TightropeRun run;
        int round = -1, version, geometryMask = -1;
        double nextSnapshot, nextSync, pendingUntil, feedbackUntil;
        string feedback;
        public static TightropeRelayGate Current { get; private set; }
        public bool IsActive => isActiveAndEnabled && course != null && course.HasRelayGap && course.IsGameplayActive
            && run != null && round == run.Round && run.State == TightropeRunState.Running;
        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public int PoleCount => stations.Length;
        public int RequiredHolders => course != null ? course.Shape.RelayRequiredHolders : 1;
        public int TransitLane => course != null ? (course.LaneCount - 1) / 2 : 0;
        public float GapFrom => course.SwingGapFrom;
        public float GapTo => course.SwingGapTo;
        public float EntryDistance => GapFrom - 2.5f;
        public float ExitDistance => GapTo + 3f;
        public int PoleLane(int index) => stations[index].Lane;
        public bool IsFarPole(int index) => stations[index].Far;
        public HumanCannonPole PhysicalPole(int index) => stations[index].Pole;
        public Vector3 PoleFeet(int index) => course.RelayPoleGround(index);
        public ulong HolderAt(int index) => stations[index].State.Occupant;
        public float HoldRemaining(int index) => stations[index].State.IsOccupied ? Mathf.Max(0f, (float)(stations[index].State.HoldUntil - Clock)) : 0f;
        // 줄은 봉을 실제로 잡고 있는 동안에만 이어진다. 놓으면 그 봉이 연 줄은 바로 끊긴다.
        public bool OutboundOpen => IsActive && (GroupHeld(false) || GroupHeld(true));
        public bool ReturnOpen => IsActive && GroupHeld(true);
        // 경고: 줄을 이어 주는 봉들이 모두 유지 시간 마지막 예고 구간에 들어갔다.
        public bool OutboundWarning => OutboundOpen && (!GroupHeld(false) || GroupInWarning(false)) && (!GroupHeld(true) || GroupInWarning(true));
        public bool ReturnWarning => ReturnOpen && GroupInWarning(true);
        public float OutboundWarningRemaining => OutboundWarning ? Mathf.Max(GroupWarningRemaining(false), GroupWarningRemaining(true)) : 0f;
        public float ReturnWarningRemaining => ReturnWarning ? GroupWarningRemaining(true) : 0f;
        public float WarningRemaining => OutboundWarning && ReturnWarning
            ? Mathf.Min(OutboundWarningRemaining, ReturnWarningRemaining)
            : OutboundWarning ? OutboundWarningRemaining : ReturnWarningRemaining;

        float GroupWarningRemaining(bool far)
        {
            if (!GroupHeld(far)) return 0f;
            float first = float.PositiveInfinity, last = 0f;
            foreach (var station in stations)
                if (station != null && station.Far == far && station.State.IsOccupied && station.State.Phase == PoleSawPhase.Warning)
                {
                    float time = Mathf.Max(0f, (float)(station.State.RearmAt - Clock));
                    first = Mathf.Min(first, time); last = Mathf.Max(last, time);
                }
            // 두 명 유지 설정에서는 먼저 끝나는 봉의 유지 시간이 다하는 순간 연결 조건을 잃는다.
            return RequiredHolders > 1 && float.IsFinite(first) ? first : last;
        }
        public string LocalFeedback => Clock < feedbackUntil ? feedback : null;

        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            for (int i = 0; i < stations.Length; i++)
            {
                var root = new GameObject($"RelayPolePhysics{i}");
                root.transform.SetParent(transform, false);
                stations[i] = new Station { Far = i >= 2, Pole = root.AddComponent<HumanCannonPole>() };
            }
            PlacePoles();
            course.Rebuilt += OnRebuilt;
        }
        void OnEnable() => Current = this;
        void PlacePoles()
        {
            if (course == null) return;
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                // 6줄에서는 2·5번, 5줄에서는 2·4번. 가운데 통로와 겹치지 않는다.
                station.Lane = course.RelayPoleLane(i);
                station.Pole.transform.SetPositionAndRotation(PoleFeet(i) + Vector3.up * (station.Pole.HangLength + .7f),
                    Quaternion.LookRotation(course.Forward));
            }
        }
        bool GroupInWarning(bool far)
        {
            foreach (var station in stations)
                if (station != null && station.Far == far && station.State.IsOccupied && station.State.Phase == PoleSawPhase.Warning) return true;
            return false;
        }
        bool GroupHeld(bool far)
        {
            int count = 0;
            foreach (var station in stations)
                if (station != null && station.Far == far && station.State.IsOccupied) count++;
            return count >= RequiredHolders;
        }
        /// <summary>
        /// 봉마다 잡을 때 정한 줄 하나를 연다. 그 줄은 그 봉을 잡고 있는 동안에만 이어지고 놓으면 바로 끊긴다.
        /// 열린 줄끼리는 겹치지 않는다.
        /// </summary>
        public bool IsLaneConnected(int lane) => LaneOwner(lane) >= 0;
        /// <summary>이 줄을 지금 열고 있는 봉 번호. 없으면 -1.</summary>
        public int LaneOwner(int lane)
        {
            if (lane < 0) return -1;
            for (int i = 0; i < stations.Length; i++) if (stations[i].Opened == lane && StationConnected(i)) return i;
            return -1;
        }
        /// <summary>봉이 마지막으로 연 줄(0부터). 없으면 -1.</summary>
        public int OpenedLane(int index) => stations[index].Opened;
        /// <summary>지금 이어진 줄 중 fromLane에서 가장 가까운 줄. 이어진 줄이 없으면 가운데 줄에서 기다린다.</summary>
        public int CrossingLane(int fromLane)
        {
            int best = NoLane;
            for (int lane = 0; course != null && lane < course.LaneCount; lane++)
                if (IsLaneConnected(lane) && (best == NoLane || Mathf.Abs(lane - fromLane) < Mathf.Abs(best - fromLane))) best = lane;
            return best != NoLane ? best : TransitLane;
        }
        bool StationConnected(int index)
        {
            var station = stations[index];
            return IsActive && station.Opened >= 0 && station.State.IsOccupied && GroupHeld(station.Far);
        }
        /// <summary>
        /// 출발 봉은 봉이 있는 줄을 빼고, 교대 봉은 전체 줄에서 고른다. 다른 봉이 지금 쓰는 줄과는 겹치지 않는다.
        /// </summary>
        int PickLane(int index)
        {
            var station = stations[index];
            laneCandidates.Clear();
            for (int lane = 0; lane < course.LaneCount; lane++)
            {
                if (!station.Far && (lane == PoleLane(0) || lane == PoleLane(1))) continue;
                bool taken = false;
                for (int j = 0; j < stations.Length && !taken; j++)
                    taken = j != index && stations[j].Opened == lane && stations[j].State.IsOccupied;
                if (!taken) laneCandidates.Add(lane);
            }
            return laneCandidates.Count > 0 ? laneCandidates[UnityEngine.Random.Range(0, laneCandidates.Count)] : TransitLane;
        }
        public int HeldPole(NetworkPlayer player)
        {
            if (player == null || !IsActive) return -1;
            for (int i = 0; i < stations.Length; i++) if (HolderAt(i) == player.OwnerClientId) return i;
            return -1;
        }
        public bool IsHeld(NetworkPlayer player) => HeldPole(player) >= 0;
        public bool IsPoleAvailable(int index) => IsActive && index >= 0 && index < PoleCount
            && !stations[index].State.IsOccupied && course.IsLaneUsable(PoleLane(index), IsFarPole(index) ? ExitDistance : EntryDistance);
        public bool CanUsePole(int index, NetworkPlayer player)
        {
            if (!IsPoleAvailable(index) || player == null || !player.IsSpawned || player.State != PlayerState.Normal || !run.IsParticipantReady(player)
                || IsHeld(player) || !player.TryGetComponent(out PlayerMover mover) || !mover.ControlEnabled || mover.PassThrough
                || (player.IsLocal && !mover.Simulated) || (player.IsLocal ? mover.IsAirborne : player.IsAirborne)
                || !course.TryGetSupportingRope(mover, out int lane) || lane != PoleLane(index)
                || !course.IsLaneUsable(lane, course.GetDistance(player.transform.position))) return false;
            if (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return false;
            if (PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player)) return false;
            if (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(player)) return false;
            var pole = PhysicalPole(index);
            // 양팔 손실이어도 지상에서 2.2m 봉에 닿는 협동 역할은 유지한다.
            return PlayerInteraction.InRange(player, pole.BarPosition(0), pole.GrabRadius(player, .75f));
        }
        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            var local = NetworkPlayer.Local;
            if (!IsActive || local == null || context.Mover == null || context.Mover.gameObject != local.gameObject) return false;
            int held = HeldPole(local);
            if (held >= 0)
            {
                context.Mover.ClearPendingInput();
                if (!InputGate.IsBlocked && (input.InteractTapped || input.ActionPressed || input.InteractLongPressed)) Send(Operation.Release, held);
                return true;
            }
            if (Clock < pendingUntil) { context.Mover.ClearPendingInput(); return true; }
            if (!input.InteractTapped || InputGate.IsBlocked) return false;
            for (int i = 0; i < PoleCount; i++)
                if (CanUsePole(i, local))
                {
                    TightropeCatchSystem.Current?.CancelLocal();
                    context.Mover.ClearPendingInput(); pendingUntil = Clock + .5d;
                    Send(Operation.Grab, i); return true;
                }
            return false;
        }
        void Update()
        {
            run = TightropeRun.Current; Connect(); PlacePoles();
            int generation = run != null ? run.Round : -1;
            if (round != generation)
            {
                RestoreBinding(false); ResetState(); round = generation;
                if (session != null && session.IsHost) Publish(); else Send(Operation.Sync);
            }
            RetryLocalAbort();
            if (!IsActive)
            {
                ClearPendingGrabs();
                RestoreBinding(false);
                bool dirty = false;
                foreach (var station in stations)
                    if (station.State.Phase != PoleSawPhase.Active || station.Opened >= 0) { station.State.Reset(); station.Opened = NoLane; dirty = true; }
                if (dirty && session != null && session.IsHost) { version++; Publish(); }
                ApplyConnections(); return;
            }
            if (session != null && session.IsHost)
            {
                ProcessPendingGrabs();
                bool changed = false;
                foreach (var station in stations)
                {
                    if (station.State.IsOccupied)
                    {
                        var player = Find(station.State.Occupant);
                        bool lost = player == null || !player.IsSpawned || player.State != PlayerState.Normal
                            || !course.IsLaneUsable(station.Lane, station.Far ? ExitDistance : EntryDistance)
                            || (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen);
                        if (lost) changed |= station.State.TryRelease(station.State.Occupant, station.State.Revision, Clock, station.Pole.WarningSeconds);
                    }
                    changed |= station.State.Tick(Clock, station.Pole.WarningSeconds);
                }
                // 봉을 놓으면 그 봉이 연 줄을 비운다. 다음에 잡을 때 다시 고른다.
                for (int i = 0; i < stations.Length; i++)
                    if (stations[i].Opened >= 0 && !stations[i].State.IsOccupied)
                    { stations[i].Opened = NoLane; changed = true; }
                if (changed) version++;
                if (changed || Clock >= nextSnapshot) { Publish(); nextSnapshot = Clock + .5d; }
            }
            else if (Clock >= nextSync && round >= 0) { Send(Operation.Sync); nextSync = Clock + 2d; }
            ApplyConnections(); UpdateBinding();
        }
        void ApplyConnections()
        {
            int mask = ReturnOpen ? 1 : 0;
            for (int lane = 0; course != null && lane < course.LaneCount; lane++) if (IsLaneConnected(lane)) mask |= 2 << lane;
            if (geometryMask == mask) return;
            if ((mask & 1) != 0 && (geometryMask < 0 || (geometryMask & 1) == 0))
            { feedback = "반대편에서 잡았어요! 출발 쪽은 봉을 놓고 건너오세요"; feedbackUntil = Clock + 4d; }
            geometryMask = mask;
            course.RefreshRelayGeometry();
        }
        void ServerHandle(ulong sender, Operation operation, int index, uint revision)
        {
            if (operation == Operation.Sync) { Publish(sender); return; }
            if (operation == Operation.Grab) { SubmitGrab(sender, index, revision); return; }
            pendingGrabs.Remove(sender);
            if (!IsActive || index < 0 || index >= stations.Length) { Publish(sender); return; }
            var station = stations[index];
            bool changed = station.State.TryRelease(sender, revision, Clock, station.Pole.WarningSeconds);
            if (changed) { version++; ApplyConnections(); Publish(); }
            else Publish(sender);
        }
        static NetworkPlayer Find(ulong owner)
        {
            foreach (var player in NetworkPlayer.All) if (player != null && player.OwnerClientId == owner) return player;
            return null;
        }
        void ResetState()
        {
            ClearPendingGrabs();
            foreach (var station in stations) { station.State.Reset(); station.Opened = NoLane; }
            version = 0; geometryMask = -1; pendingUntil = feedbackUntil = nextSnapshot = 0d; feedback = null;
            localAborted = localAbortWaiting = false; nextAbortRetry = 0d;
        }
        void OnRebuilt()
        {
            if (session != null && !session.IsHost)
            {
                // 클라이언트 재빌드는 호스트 점유/리비전을 바꾸지 않는다. 해제 확인 전에는 새 위치에 다시 붙이지 않는다.
                if (binding != null || SnapshotHeldPole(NetworkPlayer.Local) >= 0) AbortLocalBinding();
                else RestoreBinding(false);
                PlacePoles(); geometryMask = -1; ApplyConnections(); Send(Operation.Sync);
                return;
            }
            int nextVersion = version + 1;
            RestoreBinding(false); ResetState(); PlacePoles(); version = nextVersion;
            if (session != null && session.IsHost) Publish();
        }
        void LateUpdate()
        {
            PlaceHeldPlayers(); UpdateViews();
            // 봉에 매달린 동안 쌓인 추락 위험을 천천히 회복한다.
            if (binding != null) binding.Balance?.RecoverWhileSupported(Time.deltaTime);
        }
        void OnDisable()
        {
            RestoreBinding(false); ResetState(); Disconnect();
            if (Current == this) Current = null;
            HideViews();
            if (course != null) course.RefreshRelayGeometry();
        }
        void OnDestroy()
        {
            if (course != null) course.Rebuilt -= OnRebuilt;
            DestroyViews();
        }
    }
}
