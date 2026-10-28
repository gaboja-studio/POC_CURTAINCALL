using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>높은 봉과 동료의 몸을 번갈아 잡는 2인 공중그네. 봉 물리는 인간대포를 사용한다.</summary>
    [DefaultExecutionOrder(-56)]
    public sealed partial class TightropeTrapeze : MonoBehaviour
    {
        const ulong Nobody = ulong.MaxValue;
        const int PoleTotal = 3;
        const float HoldSeconds = 30f, BodySeparation = 1.45f;
        sealed class PoleState
        {
            public HumanCannonPole Physics;
            public ulong Upper = Nobody, Lower = Nobody;
            public uint Revision;
            public float Angle, Speed, Drive;
            public double Until, LastInput, SampledAt;
        }
        sealed class FlightState
        {
            public uint Token;
            public int From;
            public Vector3 Position, Velocity, Target;
            public double Started, Until;
        }
        readonly PoleState[] poles = new PoleState[PoleTotal];
        readonly Dictionary<ulong, FlightState> flights = new();
        readonly List<ulong> staleFlights = new();
        TightropeCourse course;
        TightropeRun run;
        int round = -1;
        uint version, nextFlightToken;
        double nextSnapshot, nextDrive, pendingUntil, feedbackUntil;
        string feedback;
        bool localAborted, localAbortWaiting;
        int controlledInputFrame = -1;
        public static TightropeTrapeze Current { get; private set; }
        public float AirControlSpeed => poles[0]?.Physics != null ? poles[0].Physics.AirControlSpeed : 3f;
        public string LocalFeedback => Clock < feedbackUntil ? feedback : null;
        bool Active => isActiveAndEnabled && course != null && course.HasTrapezeGap && course.IsGameplayActive
            && run != null && round == run.Round && run.State == TightropeRunState.Running;
        public bool IsActive => Active;
        public int LaneIndex => Lane;
        public float EntryDistance => course.TrapezeGapFrom - 1f;
        public int PoleCount => PoleTotal;
        public int HeldPole(NetworkPlayer player) => FindHeld(player, out int index, out _) ? index : -1;
        public bool IsLower(NetworkPlayer player) => FindHeld(player, out _, out bool lower) && lower;
        public ulong HolderAt(int index) => index >= 0 && index < PoleTotal ? poles[index].Upper : Nobody;
        public ulong PassengerAt(int index) => index >= 0 && index < PoleTotal ? poles[index].Lower : Nobody;
        public Vector3 PoleFeet(int index, bool lower = false) => Feet(Mathf.Clamp(index, 0, PoleTotal - 1), lower);
        public float SwingAngle(int index) { Sample(Mathf.Clamp(index, 0, PoleTotal - 1), out float angle, out _); return angle; }
        public float SwingSpeed(int index) { Sample(Mathf.Clamp(index, 0, PoleTotal - 1), out _, out float speed); return speed; }
        public bool CanGrabPole(int index, NetworkPlayer player) => CanGrab(player, index);
        public bool CanAttachToPole(int index, NetworkPlayer player) => index >= 0 && index < PoleTotal && CanAttach(player, index);
        int Lane => Mathf.Clamp(course.Shape.TrapezeLane - 1, 0, course.LaneCount - 1);
        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            for (int i = 0; i < PoleTotal; i++)
            {
                var root = new GameObject($"Trapeze pole {i + 1} physics"); root.transform.SetParent(transform, false);
                poles[i] = new PoleState { Physics = root.AddComponent<HumanCannonPole>() };
            }
            PlacePoles();
        }
        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this; NetworkPlayer.Restarted += OnRestarted;
            if (course != null) course.Rebuilt += OnCourseRebuilt;
        }
        void PlacePoles()
        {
            if (course == null) return;
            for (int i = 0; i < PoleTotal; i++)
            {
                poles[i].Physics.transform.SetPositionAndRotation(course.TrapezeRestingFeet(i)
                    + Vector3.up * poles[i].Physics.HangLength, Quaternion.LookRotation(course.Forward));
            }
        }
        void Update()
        {
            run = TightropeRun.Current; Connect(); PlacePoles();
            int generation = run != null ? run.Round : -1;
            if (generation != round)
            {
                ResetLocal(); ClearState(); round = generation;
                if (session != null && session.IsHost) Publish(); else Send(Command.Sync);
            }
            if (!Active) { ResetLocal(); ClearState(); return; }
            if (session != null && session.IsHost) HostStep();
            ReconcileLocal();
            // 봉에 매달린 동안 쌓인 추락 위험을 천천히 회복한다. 네트워크 동기화로 ReconcileLocal이 더 불려도 한 프레임 한 번만.
            if (localControl != null && IsHeld(localControl.Player)) localControl.Balance?.RecoverWhileSupported(Time.deltaTime);
            // 봉 점유 중에는 외줄/플랫폼 조작 규칙 전환과 무관하게 입력을 받는다.
            var local = NetworkPlayer.Local;
            if (local != null && (IsHeld(local) || IsInFlight(local))
                && local.TryGetComponent(out PlayerInputReader reader) && reader.isActiveAndEnabled)
                ReadControlledInput(local, reader.Current);
        }
        void HostStep()
        {
            for (int i = 0; i < PoleTotal; i++)
            {
                var state = poles[i];
                if (state.Upper == Nobody) continue;
                if (!Alive(state.Upper)) { RemovePole(i, false); continue; }
                if (state.Lower != Nobody && !Alive(state.Lower)) { state.Lower = Nobody; state.Revision++; Publish(); }
                if (Clock >= state.Until) { RemovePole(i, true); continue; }
                if (Clock - state.LastInput > .3d) state.Drive = 0f;
                state.Physics.Step(ref state.Angle, ref state.Speed, state.Drive, Time.deltaTime, GameSettings.Base.Character.Gravity);
                state.SampledAt = Clock;
            }
            staleFlights.Clear();
            foreach (var pair in flights) if (!Alive(pair.Key) || Clock > pair.Value.Until + 1d) staleFlights.Add(pair.Key);
            foreach (ulong actor in staleFlights) flights.Remove(actor);
            ProcessPendingRequests();
            if (Clock >= nextSnapshot) { Publish(); nextSnapshot = Clock + .1d; }
        }
        public bool IsHeld(NetworkPlayer player) => Active && FindHeld(player, out _, out _);
        public bool IsInFlight(NetworkPlayer player) => Active && player != null
            && flights.TryGetValue(player.OwnerClientId, out var flight)
            && (!player.IsLocal || (!localAborted && flight.Token > finishedFlightToken));
        public bool IsCrossing(NetworkPlayer player) => IsHeld(player) || IsInFlight(player);
        bool FindHeld(NetworkPlayer player, out int pole, out bool lower)
        {
            pole = -1; lower = false;
            if (player == null) return false;
            for (int i = 0; i < PoleTotal; i++)
            {
                if (poles[i].Upper == player.OwnerClientId) { pole = i; return true; }
                if (poles[i].Lower == player.OwnerClientId) { pole = i; lower = true; return true; }
            }
            return false;
        }
        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            var local = NetworkPlayer.Local;
            if (!Active || local == null || context.Mover == null || context.Mover.gameObject != local.gameObject) return false;
            bool held = FindHeld(local, out int pole, out bool lower);
            if (held || IsInFlight(local))
            {
                context.Mover.ClearPendingInput();
                ReadControlledInput(local, input);
                return true;
            }
            if (Clock < pendingUntil) { context.Mover.ClearPendingInput(); return true; }
            return !InputGate.IsBlocked && input.InteractTapped && TryRequestCatch(local);
        }

        void ReadControlledInput(NetworkPlayer local, in PlayerInputFrame input)
        {
            if (controlledInputFrame == Time.frameCount) return;
            controlledInputFrame = Time.frameCount; // 조작 규칙 경로와 이중으로 Space/F를 보내지 않는다.
            if (!Active || localAborted || local.State != PlayerState.Normal || InputGate.IsBlocked) { air.Clear(); return; }
            bool held = FindHeld(local, out int pole, out bool lower);
            if (!held && !IsInFlight(local)) return;
            air.SetInput(new Vector2(input.Posture, input.Move), Time.frameCount);
            if (held && !lower && Clock >= nextDrive)
            { Send(Command.Swing, pole, input.Move); nextDrive = Clock + .05d; }
            if (held && input.ActionPressed)
            {
                if (!lower && poles[pole].Lower != Nobody) Tell("아래 동료가 먼저 Space로 날아가야 해요");
                else Send(Command.Release, pole);
            }
            if (!held && input.InteractTapped) TryRequestCatch(local);
        }
        bool TryRequestCatch(NetworkPlayer local)
        {
            if (Clock < pendingUntil) return false;
            for (int i = 0; i < PoleTotal; i++)
            {
                if (CanGrab(local, i)) { pendingUntil = Clock + .5d; Send(Command.Grab, i); return true; }
                if (CanAttach(local, i)) { pendingUntil = Clock + .5d; Send(Command.Attach, i); return true; }
            }
            return false;
        }
        bool Eligible(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || player.State != PlayerState.Normal || FindHeld(player, out _, out _)
                || !player.TryGetComponent(out PlayerMover mover) || mover.PassThrough || !mover.ControlEnabled) return false;
            if (player.TryGetComponent(out PlayerBalance balance) && balance.HasFallen) return false;
            var stack = PiggybackSystem.Current;
            return (stack == null || !stack.IsInStack(player) || stack.CanEnterTrapeze(player))
                && (TightropeSwingPole.Current == null || !TightropeSwingPole.Current.IsCrossing(player));
        }
        bool CanGrab(NetworkPlayer player, int index)
        {
            if (!Active || index < 0 || index >= PoleTotal || !Eligible(player) || poles[index].Upper != Nobody) return false;
            bool flying = flights.TryGetValue(player.OwnerClientId, out var flight) && Clock <= flight.Until;
            if (flying && index != flight.From + 1) return false;
            if (!flying && index == 0 && (PiggybackSystem.Current == null || !PiggybackSystem.Current.CanEnterTrapeze(player))) return false;
            return InCatchRange(player, index, false);
        }
        bool CanAttach(NetworkPlayer player, int index)
        {
            if (!Active || index < 0 || index >= PoleTotal || !Eligible(player)
                || poles[index].Upper == Nobody || poles[index].Lower != Nobody
                || !Alive(poles[index].Upper) || Clock >= poles[index].Until) return false;
            bool flying = flights.TryGetValue(player.OwnerClientId, out var flight) && Clock <= flight.Until;
            if (flying && index != flight.From + 1) return false;
            return InCatchRange(player, index, true);
        }

        bool InCatchRange(NetworkPlayer player, int index, bool lower)
        {
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? Mathf.Clamp01(interaction.ReachMultiplier) : 1f;
            // 팔 손실로 두 사람의 몸 간격보다 잡기 반경이 작아지지 않게 한다. 부상4 사망은 Eligible에서 제외된다.
            if (reach > 0f) reach = Mathf.Max(.75f, reach);
            Vector3 center = Feet(index, false);
            if (lower)
            {
                var holder = Find(poles[index].Upper);
                if (holder == null) return false;
                center += PlayerInteraction.Center(holder) - holder.transform.position;
            }
            else center += Vector3.up * poles[index].Physics.HangLength;
            return SwingPoleMath.InPoleReach(PlayerInteraction.Center(player), center, reach);
        }

        float FlightDistanceLimit(int from, Vector3 origin)
        {
            return Mathf.Max(.1f, Vector3.Dot(FlightTarget(from) - origin, course.Forward));
        }

        Vector3 FlightTarget(int from) => from + 1 < PoleTotal ? Feet(from + 1, poles[from + 1].Upper != Nobody)
            : course.GetLanePosition(Lane, course.TrapezeGapTo + 1f);

        Vector3 PlanFlightVelocity(int from, Vector3 origin, Vector3 target, Vector3 velocity, out float speedLimit)
        {
            float gravity = GameSettings.Base.Character.Gravity;
            if (from + 1 < PoleTotal) return SwingPoleMath.PoleFlightVelocity(origin, target, velocity, course.Forward,
                gravity, poles[from].Physics.HangLength, out speedLimit);
            speedLimit = SwingPoleMath.FlightSpeedLimit(origin.y - target.y, velocity.y, gravity,
                Mathf.Max(.1f, Vector3.Dot(target - origin, course.Forward)));
            return SwingPoleMath.LimitFlightVelocity(velocity, speedLimit);
        }

        void Sample(int index, out float angle, out float speed)
        {
            var state = poles[index]; angle = state.Angle; speed = state.Speed;
            if (state.Upper == Nobody) { angle = speed = 0f; return; }
            if (session == null || !session.IsHost)
                state.Physics.Step(ref angle, ref speed, state.Drive, Mathf.Clamp((float)(Clock - state.SampledAt), 0f, .2f), GameSettings.Base.Character.Gravity);
        }
        Vector3 Feet(int index, bool lower)
        { Sample(index, out float angle, out _); return poles[index].Physics.HangingPosition(angle) - (lower ? Vector3.up * BodySeparation : Vector3.zero); }
        static NetworkPlayer Find(ulong actor)
        { foreach (var player in NetworkPlayer.All) if (player != null && player.OwnerClientId == actor) return player; return null; }
        static bool Alive(ulong actor) { var player = Find(actor); return player != null && player.IsSpawned && player.State == PlayerState.Normal; }
        void Tell(string message) { feedback = message; feedbackUntil = Clock + 3d; }
        void ClearState()
        {
            ClearPendingRequests();
            foreach (var state in poles)
            { if (state == null) continue; state.Upper = state.Lower = Nobody; state.Revision = 0; state.Angle = state.Speed = state.Drive = 0f; state.Until = state.LastInput = state.SampledAt = 0d; }
            flights.Clear(); version = nextFlightToken = 0; nextSnapshot = 0d;
        }
        void OnRestarted(NetworkPlayer player)
        {
            if (player != null && player.IsLocal)
            {
                ResetLocal(false); finishedFlightToken = 0; localAborted = true;
                localAbortWaiting = session != null && !session.IsHost; Send(Command.Abort);
            }
            if (session != null && session.IsHost && player != null) AbortActor(player.OwnerClientId);
        }
        void OnCourseRebuilt()
        {
            uint savedVersion = version, savedToken = nextFlightToken;
            bool hadLocal = localControl != null;
            ResetLocal(); ClearState(); version = savedVersion; nextFlightToken = savedToken; PlacePoles();
            if (session != null && session.IsHost) Publish();
            else
            {
                localAborted = localAbortWaiting = hadLocal;
                if (hadLocal) Send(Command.Abort);
                Send(Command.Sync);
            }
        }
        void LateUpdate()
        {
            StepFlight(); UpdateViews();
            if (!Active) return;
            for (int i = 0; i < PoleTotal; i++)
                foreach (bool lower in new[] { false, true })
                {
                    var player = Find(lower ? poles[i].Lower : poles[i].Upper);
                    if (player != null && !player.IsLocal && player.State == PlayerState.Normal)
                        player.transform.SetPositionAndRotation(Feet(i, lower), Quaternion.LookRotation(course.Forward));
                }
        }
        void OnDisable()
        {
            NetworkPlayer.Restarted -= OnRestarted;
            if (course != null) course.Rebuilt -= OnCourseRebuilt;
            ResetLocal(); Disconnect(); ClearState();
            HideViews(); if (Current == this) Current = null;
        }
        void OnDestroy() { DestroyViews(); foreach (var state in poles) if (state?.Physics != null) Destroy(state.Physics.gameObject); }
    }
}
