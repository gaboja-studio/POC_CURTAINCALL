using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>공통 스위치 봉이 잡혀 있는 동안 연결할 외줄만 관리한다. 점유·반동·비행은 공통 봉 서비스가 맡는다.</summary>
    [DefaultExecutionOrder(-55)]
    public sealed partial class TightropeRelayGate : MonoBehaviour
    {
        const ulong Nobody = ulong.MaxValue;
        const int NoLane = -1;
        sealed class Station
        {
            public HumanCannonPole Pole;
            public int Index = -1, Lane, Opened = NoLane;
            public bool Far;
            public ulong PreviousHolder = Nobody;
        }
        Station[] stations = new Station[4];
        readonly List<int> laneCandidates = new();
        TightropeCourse course;
        TightropeRun run;
        HumanCannonPoleSystem common;
        int round = -1, version, geometryMask = -1;
        double nextSnapshot, nextSync, feedbackUntil;
        string feedback;
        public static TightropeRelayGate Current { get; private set; }
        public bool IsActive => isActiveAndEnabled && HasGate && course.IsGameplayActive
            && run != null && run.State == TightropeRunState.Running;
        double Clock => common != null ? common.Clock : Time.timeAsDouble;
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
        public Vector3 PoleFeet(int index) => UsesSpots ? course.GetLanePosition(spotList[index].Lane, spotList[index].Distance) : course.RelayPoleGround(index);
        public ulong HolderAt(int index) => common != null && stations[index] != null ? common.Occupant(stations[index].Index, 0) : Nobody;
        public bool IsUnlimitedHold(int index) => false;
        public float HoldRemaining(int index) => HolderAt(index) != Nobody ? Mathf.Max(0f, (float)(common.HoldUntil(stations[index].Index) - Clock)) : 0f;
        public bool OutboundOpen => IsActive && (GroupHeld(false) || GroupHeld(true));
        public bool ReturnOpen => IsActive && GroupHeld(true);
        public bool OutboundWarning => OutboundOpen && (!GroupHeld(false) || GroupInWarning(false)) && (!GroupHeld(true) || GroupInWarning(true));
        public bool ReturnWarning => ReturnOpen && GroupInWarning(true);
        public float OutboundWarningRemaining => OutboundWarning ? Mathf.Max(GroupWarningRemaining(false), GroupWarningRemaining(true)) : 0;
        public float ReturnWarningRemaining => ReturnWarning ? GroupWarningRemaining(true) : 0;
        public float WarningRemaining => OutboundWarning && ReturnWarning ? Mathf.Min(OutboundWarningRemaining, ReturnWarningRemaining)
            : OutboundWarning ? OutboundWarningRemaining : ReturnWarningRemaining;
        public string LocalFeedback => Clock < feedbackUntil ? feedback : null;

        void Awake()
        {
            course = GetComponent<TightropeCourse>(); run = GetComponent<TightropeRun>();
            common = HumanCannonPoleSystem.Ensure(gameObject);
            if (stations.Length == 0 || stations[0] == null) CreateStations();
            PlacePoles();
        }
        void OnEnable()
        {
            Current = this;
            if (course != null) course.Rebuilt += OnRebuilt;
            if (common != null) common.OccupancyChanged += OnOccupancyChanged;
        }
        void PlacePoles()
        {
            if (course == null) return;
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i]; if (station == null) continue;
                station.Lane = UsesSpots ? spotList[i].Lane : course.RelayPoleLane(i);
                station.Pole.transform.SetPositionAndRotation(PoleFeet(i) + Vector3.up * (GameSettings.Poles.HangLength + .7f), Quaternion.LookRotation(course.Forward));
            }
        }
        bool GroupHeld(bool far)
        {
            int count = 0;
            for (int i = 0; i < stations.Length; i++) if (stations[i] != null && stations[i].Far == far && HolderAt(i) != Nobody) count++;
            return count >= RequiredHolders;
        }
        bool GroupInWarning(bool far)
        {
            for (int i = 0; i < stations.Length; i++) if (stations[i] != null && stations[i].Far == far && HolderAt(i) != Nobody && HoldRemaining(i) <= 5f) return true;
            return false;
        }
        float GroupWarningRemaining(bool far)
        {
            if (!GroupHeld(far)) return 0;
            float first = float.PositiveInfinity, last = 0;
            for (int i = 0; i < stations.Length; i++)
                if (stations[i] != null && stations[i].Far == far && HolderAt(i) != Nobody)
                { float left = HoldRemaining(i); first = Mathf.Min(first, left); last = Mathf.Max(last, left); }
            return RequiredHolders > 1 && float.IsFinite(first) ? first : last;
        }
        public bool IsLaneConnected(int lane) => LaneOwner(lane) >= 0;
        public int LaneOwner(int lane)
        {
            if (lane < 0) return -1;
            for (int i = 0; i < stations.Length; i++) if (stations[i] != null && stations[i].Opened == lane && StationConnected(i)) return i;
            return -1;
        }
        public int OpenedLane(int index) => stations[index].Opened;
        public int CrossingLane(int fromLane)
        {
            int best = NoLane;
            for (int lane = 0; course != null && lane < course.LaneCount; lane++)
                if (IsLaneConnected(lane) && (best == NoLane || Mathf.Abs(lane - fromLane) < Mathf.Abs(best - fromLane))) best = lane;
            return best != NoLane ? best : TransitLane;
        }
        bool StationConnected(int index) => IsActive && stations[index].Opened >= 0 && HolderAt(index) != Nobody && (UsesSpots || GroupHeld(stations[index].Far));
        int PickLane(int index)
        {
            if (UsesSpots) return spotList[index].TargetLane;
            laneCandidates.Clear();
            for (int lane = 0; lane < course.LaneCount; lane++)
            {
                if (!stations[index].Far && (lane == PoleLane(0) || lane == PoleLane(1))) continue;
                bool taken = false;
                for (int j = 0; j < stations.Length && !taken; j++) taken = j != index && stations[j].Opened == lane && HolderAt(j) != Nobody;
                if (!taken) laneCandidates.Add(lane);
            }
            return laneCandidates.Count > 0 ? laneCandidates[Random.Range(0, laneCandidates.Count)] : TransitLane;
        }
        public int HeldPole(NetworkPlayer player)
        {
            if (player == null || common == null) return -1;
            int index = common.HeldBy(player.OwnerClientId);
            for (int i = 0; i < stations.Length; i++) if (stations[i] != null && stations[i].Index == index) return i;
            return -1;
        }
        public bool IsHeld(NetworkPlayer player) => HeldPole(player) >= 0;
        public bool IsPoleAvailable(int index) => IsActive && index >= 0 && index < PoleCount && common != null && common.HasFreeSeat(stations[index].Index);
        public bool CanUsePole(int index, NetworkPlayer player) => IsPoleAvailable(index) && common.CanApproachPole(stations[index].Index, player);
        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input) => IsActive && common != null && common.TryHandleInput(context, input);
        void Update()
        {
            if (run == null) run = GetComponent<TightropeRun>();
            Connect(); PlacePoles();
            int generation = run != null ? run.Round : -1;
            if (round != generation) { Deactivate(); ResetState(); round = generation; }
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i]; if (station == null) continue;
                if (!IsActive && station.Pole.InteractionAvailable) common.Deactivate(station.Index);
                station.Pole.InteractionAvailable = IsActive;
                station.Pole.PresentationVisible = isActiveAndEnabled && HasGate && course.IsTraversalActive;
            }
            if (session != null && session.IsHost)
            {
                bool changed = RefreshOpenedLanes();
                if (changed || Clock >= nextSnapshot) { Publish(); nextSnapshot = Clock + .5d; }
            }
            else if (Clock >= nextSync) { RequestSync(); nextSync = Clock + 2d; }
            ApplyConnections();
        }
        bool RefreshOpenedLanes()
        {
            bool changed = false;
            for (int i = 0; i < stations.Length; i++)
            {
                var station = stations[i]; if (station == null) continue;
                ulong holder = IsActive ? HolderAt(i) : Nobody;
                if (holder == station.PreviousHolder) continue;
                station.PreviousHolder = holder;
                station.Opened = holder == Nobody ? NoLane : PickLane(i);
                changed = true;
            }
            if (changed) version++;
            return changed;
        }
        void OnOccupancyChanged(int index, ulong actor, bool held)
        {
            if (session != null && session.IsHost && RefreshOpenedLanes()) Publish();
            ApplyConnections();
        }
        void ApplyConnections()
        {
            int mask = ReturnOpen ? 1 : 0;
            for (int lane = 0; course != null && lane < course.LaneCount; lane++) if (IsLaneConnected(lane)) mask |= 2 << lane;
            if (geometryMask == mask) return;
            if ((mask & 1) != 0 && (geometryMask < 0 || (geometryMask & 1) == 0))
            { feedback = "반대편에서 잡았어요! 출발 쪽은 봉을 놓고 건너오세요"; feedbackUntil = Clock + 4d; }
            geometryMask = mask; course?.RefreshRelayGeometry();
        }
        void ResetState()
        {
            foreach (var station in stations) if (station != null) { station.Opened = NoLane; station.PreviousHolder = Nobody; }
            version = session != null && session.IsHost ? version + 1 : -1;
            geometryMask = -1; nextSnapshot = nextSync = feedbackUntil = 0; feedback = null;
        }
        void Deactivate() { if (common != null) foreach (var station in stations) if (station != null) common.Deactivate(station.Index); }
        void OnRebuilt() { Deactivate(); ResetState(); PlacePoles(); Publish(); }
        void LateUpdate() => UpdateViews();
        void OnDisable()
        {
            if (common != null) common.OccupancyChanged -= OnOccupancyChanged;
            if (course != null) course.Rebuilt -= OnRebuilt;
            Deactivate(); ResetState(); Disconnect(); HideViews();
            if (Current == this) Current = null;
            course?.RefreshRelayGeometry();
        }
        void OnDestroy() => DestroyViews();
    }
}
