using System;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>외줄 공중그네의 배치·수업·표시만 담당한다. 점유·조작·비행은 모든 봉의 공통 서비스가 처리한다.</summary>
    [DefaultExecutionOrder(-56)]
    public sealed partial class TightropeTrapeze : MonoBehaviour
    {
        const ulong Nobody = ulong.MaxValue;
        const int MaxPoleTotal = 3;
        int practicePoleCount = MaxPoleTotal;
        bool practiceLowPoles;
        int PoleTotal => practicePoleCount;
        sealed class PoleState
        {
            public HumanCannonPole Physics;
            public int Index = -1;
            public ulong Upper = Nobody, Lower = Nobody;
            public double Until;
        }
        readonly PoleState[] poles = new PoleState[MaxPoleTotal];
        TightropeCourse course;
        TightropeRun run;
        HumanCannonPoleSystem common;
        int round = -1;
        public static TightropeTrapeze Current { get; private set; }
        public static event Action<ulong, TightropeTrapeze> ServerCrossed;
        public event Action<string> RequestObserved;
        public float AirControlSpeed => GameSettings.Poles.AirControlSpeed;
        public string LocalFeedback => null;
        bool Active => isActiveAndEnabled && course != null && course.HasTrapezeGap && course.IsGameplayActive
            && run != null && run.State == TightropeRunState.Running;
        public bool IsActive => Active;
        bool HoldTimeLimited => true;
        int Lane => Mathf.Clamp(course.Shape.TrapezeLane - 1, 0, course.LaneCount - 1);
        double Clock => common != null ? common.Clock : Time.timeAsDouble;
        public int LaneIndex => Lane;
        public float EntryDistance => course.TrapezeGapFrom - 1f;
        public int PoleCount => PoleTotal;

        public void ConfigurePracticeLesson(int count = 2, bool lowPoles = false)
        {
            if (!gameObject.activeInHierarchy && GetComponent<TightropeRun>() is { PracticeMode: true })
            { practicePoleCount = Mathf.Clamp(count, 1, MaxPoleTotal); practiceLowPoles = lowPoles; }
        }
        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            run = GetComponent<TightropeRun>();
            common = HumanCannonPoleSystem.Ensure(gameObject);
            for (int i = 0; i < PoleTotal; i++)
            {
                var root = new GameObject($"Trapeze pole {i + 1} physics");
                root.transform.SetParent(transform, false);
                var physics = root.AddComponent<HumanCannonPole>();
                physics.InteractionAvailable = false;
                poles[i] = new PoleState { Physics = physics, Index = common.Register(physics) };
            }
            PlacePoles();
        }
        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            if (common != null) { common.Landed += OnLanded; common.OccupancyChanged += OnOccupancy; }
            if (course != null) course.Rebuilt += OnCourseRebuilt;
        }
        void PlacePoles()
        {
            if (course == null) return;
            for (int i = 0; i < PoleTotal; i++)
            {
                Vector3 feet = practiceLowPoles
                    ? course.GetLanePosition(Lane, course.TrapezeGapFrom - 1f + i * 8f) + Vector3.up * .6f
                    : course.TrapezeRestingFeet(i);
                poles[i].Physics.transform.SetPositionAndRotation(feet + Vector3.up * GameSettings.Poles.HangLength,
                    Quaternion.LookRotation(course.Forward));
            }
        }
        void Update()
        {
            if (run == null) run = GetComponent<TightropeRun>();
            if (common == null) return;
            int generation = run != null ? run.Round : -1;
            if (generation != round) { Deactivate(); round = generation; }
            PlacePoles();
            for (int i = 0; i < PoleTotal; i++)
            {
                var state = poles[i];
                if (!Active && state.Physics.InteractionAvailable) common.Deactivate(state.Index);
                state.Physics.InteractionAvailable = Active;
                state.Physics.PresentationVisible = isActiveAndEnabled && course.HasTrapezeGap && course.IsTraversalActive;
                state.Upper = common.Occupant(state.Index, 0);
                state.Lower = common.Occupant(state.Index, 1);
                state.Until = common.HoldUntil(state.Index);
            }
        }
        int LocalIndex(int shared)
        { for (int i = 0; i < PoleTotal; i++) if (poles[i] != null && poles[i].Index == shared) return i; return -1; }
        void OnOccupancy(int index, ulong actor, bool held)
        {
            int local = LocalIndex(index);
            if (local < 0) return;
            poles[local].Upper = common.Occupant(index, 0);
            poles[local].Lower = common.Occupant(index, 1);
            poles[local].Until = common.HoldUntil(index);
            RequestObserved?.Invoke(held ? "Grab" : "Release");
        }
        void OnLanded(int index, ulong actor, Vector3 position, bool safe)
        {
            if (!Active || LocalIndex(index) < 0 || !safe || course.GetDistance(position) < course.TrapezeGapTo) return;
            ServerCrossed?.Invoke(actor, this);
        }
        public int HeldPole(NetworkPlayer player) => player != null && common != null ? LocalIndex(common.HeldBy(player.OwnerClientId)) : -1;
        public bool IsLower(NetworkPlayer player) => HeldPole(player) >= 0 && common.IsLower(player.OwnerClientId);
        public ulong HolderAt(int index) => Valid(index) ? common.Occupant(poles[index].Index, 0) : Nobody;
        public ulong PassengerAt(int index) => Valid(index) ? common.Occupant(poles[index].Index, 1) : Nobody;
        bool Valid(int index) => common != null && index >= 0 && index < PoleTotal && poles[index] != null;
        public Vector3 PoleFeet(int index, bool lower = false) => Feet(Mathf.Clamp(index, 0, PoleTotal - 1), lower);
        public float SwingAngle(int index) => DisplayAngle(Mathf.Clamp(index, 0, PoleTotal - 1));
        public float SwingSpeed(int index) => Valid(index) ? common.SpeedAt(poles[index].Index) : 0;
        public bool CanGrabPole(int index, NetworkPlayer player) => CanGrab(player, index);
        public bool CanAttachToPole(int index, NetworkPlayer player) => CanAttach(player, index);
        bool CanGrab(NetworkPlayer player, int index) => Active && Valid(index) && HolderAt(index) == Nobody && common.CanApproachPole(poles[index].Index, player);
        bool CanAttach(NetworkPlayer player, int index) => Active && Valid(index) && HolderAt(index) != Nobody && PassengerAt(index) == Nobody && common.CanApproachPole(poles[index].Index, player);
        public bool IsHeld(NetworkPlayer player) => HeldPole(player) >= 0;
        public bool IsInFlight(NetworkPlayer player)
        {
            if (player == null || common == null) return false;
            for (int i = 0; i < PoleTotal; i++) if (common.IsFlyingFrom(poles[i].Index, player.OwnerClientId)) return true;
            return false;
        }
        public bool IsCrossing(NetworkPlayer player) => IsHeld(player) || IsInFlight(player);
        bool FindHeld(NetworkPlayer player, out int index, out bool lower)
        { index = HeldPole(player); lower = index >= 0 && common.IsLower(player.OwnerClientId); return index >= 0; }
        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input) => Active && common != null && common.TryHandleInput(context, input);
        Vector3 Feet(int index, bool lower) => poles[index].Physics.HangingPosition(DisplayAngle(index), lower ? 1 : 0);
        float DisplayAngle(int index) => Valid(index) ? common.AngleAt(poles[index].Index) : 0;
        public bool CanLaunchTowardNext(int index, bool lower)
        {
            if (!Active || !Valid(index) || HolderAt(index) == Nobody) return false;
            Vector3 origin = Feet(index, lower);
            Vector3 velocity = poles[index].Physics.ReleaseVelocity(SwingAngle(index), SwingSpeed(index));
            Vector3 target = index + 1 < PoleTotal ? poles[index + 1].Physics.BarPosition(SwingAngle(index + 1))
                : course.GetLanePosition(Lane, course.TrapezeGapTo + 1f);
            for (float time = .04f; time <= 3f; time += .04f)
            {
                Vector3 predicted = origin + (velocity + course.Forward * AirControlSpeed) * time
                    + Vector3.down * (.5f * GameSettings.Base.Character.Gravity * time * time);
                if (index + 1 < PoleTotal)
                { if ((predicted + Vector3.up * .8f - target).sqrMagnitude <= Mathf.Pow(GameSettings.Poles.GrabRadius, 2)) return true; }
                else if (velocity.y - GameSettings.Base.Character.Gravity * time <= 0 && predicted.y <= target.y + .15f)
                    return course.GetDistance(predicted) >= course.TrapezeGapTo + .35f;
            }
            return false;
        }
        void Deactivate() { if (common != null) foreach (var pole in poles) if (pole != null) common.Deactivate(pole.Index); }
        void OnCourseRebuilt() { Deactivate(); PlacePoles(); }
        void LateUpdate() => UpdateViews();
        void OnDisable()
        {
            Deactivate();
            if (common != null) { common.Landed -= OnLanded; common.OccupancyChanged -= OnOccupancy; }
            if (course != null) course.Rebuilt -= OnCourseRebuilt;
            if (Current == this) Current = null;
            HideViews();
        }
        void OnDestroy() => DestroyViews();
    }
}
