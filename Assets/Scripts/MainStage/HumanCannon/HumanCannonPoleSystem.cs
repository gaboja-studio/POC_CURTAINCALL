using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class HumanCannonPoleSystem : MonoBehaviour
    {
        [SerializeField] HumanCannonPole[] poles = Array.Empty<HumanCannonPole>();
        public bool IndependentInteraction => true;
        uint platformLaunch;
        const double SnapshotInterval = .05d;
        sealed class Rider
        {
            public float Input;
            public double LastInput;
            public uint InputSequence;
            public uint JoinedRevision;
            public int Station;
            public uint Launch, Segment;
        }

        sealed class Runtime
        {
            public readonly PoleGateState State;
            public readonly Rider[] Riders = new Rider[PoleGateState.MaxRiders];
            public readonly Dictionary<ulong, uint> LastDeparture = new();
            public uint ResetRevision;
            public float Angle, Speed, ReleaseDirection;
            int driveDirection;
            public bool Braking { get; private set; }
            double nextIdleSnapshot;
            double receivedAngleAt = double.NegativeInfinity;
            PoleAnglePresentation presentation;
            public float MaxDisplayAngle = 85f;
            public Vector3 ReleasePosition, ReleaseVelocity;
            public Quaternion ReleaseRotation = Quaternion.identity;
            public Runtime(int capacity)
            {
                State = new PoleGateState(capacity);
                for (int i = 0; i < Riders.Length; i++) Riders[i] = new Rider();
            }

            public void ObserveDrive(float drive)
            {
                int direction = PoleSwingInput.DirectionOf(drive);
                if (direction != driveDirection)
                {
                    driveDirection = direction;
                    int swingDirection = PoleSwingInput.DirectionOf(Speed);
                    if (swingDirection == 0) swingDirection = PoleSwingInput.DirectionOf(Angle);
                    Braking = direction * swingDirection < 0;
                }
                if (Braking) ReleaseDirection = 0f;
                else if (direction != 0) ReleaseDirection = direction;
            }

            public void ResetDrive()
            {
                driveDirection = 0;
                Braking = false;
                ReleaseDirection = 0f;
            }

            public bool SnapshotDue(double now, double idlePhase)
            {
                if (State.Count != 0 || Mathf.Abs(Angle) > .01f || Mathf.Abs(Speed) > .01f) return true;
                if (now < nextIdleSnapshot) return false;
                // Recover missed initial sync without sending every stationary pole in one burst.
                nextIdleSnapshot = Math.Floor(now - idlePhase) + 1d + idlePhase;
                return true;
            }

            public float DisplayAngle(double now)
            {
                if (double.IsNegativeInfinity(receivedAngleAt)) return Angle; // Host simulation.
                // Brief network jitter must not stop the rider at every packet boundary.
                // Prediction is visual only and stops after 100 ms without a fresh snapshot.
                float predicted = Angle + Speed * GameSettings.Poles.SwingMotionSpeed
                    * Mathf.Clamp((float)(now - receivedAngleAt), 0f, .1f);
                return Mathf.Clamp(presentation.Sample(predicted, now), -MaxDisplayAngle, MaxDisplayAngle);
            }

            public void ReceiveAngle(float angle, double now, bool continuous)
            {
                // Snap when ownership changes or a stalled connection catches up.
                presentation.Receive(DisplayAngle(now), angle, now, continuous, (float)SnapshotInterval);
                Angle = angle;
                receivedAngleAt = now;
            }
        }
        Runtime[] states;
        NetworkSessionManager session;
        double nextSnapshot;
        uint inputSequence;
        double nextInput;
        int lastInputDirection;
        public static HumanCannonPoleSystem Current { get; private set; }
        public int PoleCount => poles.Length;
        public HumanCannonPole Pole(int index) => Valid(index) ? poles[index] : null;
        public PoleGateState State(int index) => states[index].State;
        public int OccupantCount(int index) => Valid(index) ? states[index].State.Count : 0;
        public ulong Occupant(int index, int seat) => Valid(index) ? states[index].State.OccupantAt(seat) : CannonSeatState.Nobody;
        public bool HasFreeSeat(int index) => Valid(index) && states[index].State.HasFreeSeat;
        public bool CanGrab(int index, NetworkPlayer player) => CanApproachPole(index, player);
        public Vector3 InteractionPoint(int index, NetworkPlayer player)
        {
            int seat = FindSeat(player, index);
            return poles[index].GrabPoint(SwingAngle(index), seat);
        }
        public double Clock => session != null && session.NetworkManager != null
            && session.NetworkManager.IsListening ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        public int HeldBy(ulong actor)
        {
            if (actor == CannonSeatState.Nobody || states == null) return -1;
            for (int i = 0; i < states.Length; i++) if (states[i].State.SeatOf(actor) >= 0) return i;
            return -1;
        }

        bool CanApproach(NetworkPlayer player)
        {
            var cannon = HumanCannonSystem.Current;
            var stack = PiggybackSystem.Current;
            return player != null && cannon != null && (cannon.IsFlying(player.OwnerClientId) || CanEnterFromPlatform(player))
                && HeldBy(player.OwnerClientId) < 0 && !cannon.IsSeated(player.OwnerClientId)
                && (stack == null || !stack.IsInStack(player) || stack.CanReleaseForDevice(player))
                && HumanCannonCarry.Held(player.OwnerClientId) == null;
        }

        public int FindGrabbable(NetworkPlayer player)
        {
            if (!CanApproach(player)) return -1;
            int closest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < poles.Length; i++)
            {
                if (!Valid(i) || !poles[i].InteractionAvailable) continue;
                int seat = FindSeat(player, i);
                if (seat < 0) continue;
                float candidate = (poles[i].GrabPoint(SwingAngle(i), seat) - PlayerInteraction.Center(player)).sqrMagnitude;
                if (candidate < distance) { distance = candidate; closest = i; }
            }
            return closest;
        }

        int FindSeat(NetworkPlayer player, int index, double observedElapsed = -1d)
        {
            var state = states[index].State;
            if (player == null || !state.HasFreeSeat) return -1;
            int closest = -1;
            float distance = float.PositiveInfinity;
            for (int seat = 0; seat < state.Capacity; seat++)
            {
                if (seat > 0 && state.OccupantAt(seat - 1) == CannonSeatState.Nobody) continue;
                if (state.OccupantAt(seat) != CannonSeatState.Nobody
                    || !poles[index].IsSeatInReach(player, SwingAngle(index), seat, observedElapsed)) continue;
                float candidate = (poles[index].GrabPoint(SwingAngle(index), seat) - PlayerInteraction.Center(player)).sqrMagnitude;
                if (candidate < distance) { distance = candidate; closest = seat; }
            }
            return closest;
        }
        float SwingAngle(int index) => states[index].Angle;

        public Quaternion RiderRotation(int index, ulong actor)
        {
            int seat = states[index].State.SeatOf(actor);
            return SeatRotation(index, seat);
        }

        Quaternion SeatRotation(int index, int seat)
        {
            Quaternion grip = poles[index].Grip.rotation;
            return grip;
        }

        internal void RequestGrab(int index, int station, uint launch, uint segment) =>
            SendRequest(PoleAction.Grab, index, station, launch, segment, 0f, 0);
        internal bool CanEnterFromPlatform(NetworkPlayer player) => player != null
            && player.IsSpawned && player.State == PlayerState.Normal;
        internal void RequestPlatformGrab(int index) => SendRequest(PoleAction.PlatformGrab, index, 0, 0, 0, 0, 0);
        internal void RequestRelease(int index) => SendRequest(PoleAction.Release, index, 0, 0, 0, 0f, 0);
        internal void RequestDrop(int index) => SendRequest(PoleAction.Drop, index, 0, 0, 0, 0f, 0);
        internal void RequestSwing(int index, float input)
        {
            int direction = PoleSwingInput.DirectionOf(input);
            if (Clock < nextInput && direction == lastInputDirection) return;
            lastInputDirection = direction;
            nextInput = Clock + 0.05;
            SendRequest(PoleAction.Swing, index, 0, 0, 0, input, ++inputSequence);
        }
        public Vector3 HangingPosition(int index) => poles[index].HangingPosition(states[index].DisplayAngle(Time.unscaledTimeAsDouble));
        public Vector3 HangingPosition(int index, ulong actor)
        {
            int seat = states[index].State.SeatOf(actor);
            return seat >= 0 ? poles[index].HangingPosition(states[index].DisplayAngle(Time.unscaledTimeAsDouble), seat) : HangingPosition(index);
        }

        internal Vector3 ConstrainFlight(Vector3 from, Vector3 displacement, float radius)
        {
            foreach (var pole in poles)
                if (pole != null && pole.LinkedGate != null && pole.LinkedGate.isActiveAndEnabled)
                    displacement = pole.LinkedGate.ConstrainFlight(from, displacement, radius);
            return displacement;
        }

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            InitializeStates();
            NetworkPlayer.Restarted += Restart;
        }
        Runtime NewRuntime(int index) => new Runtime(poles[index] != null ? poles[index].RiderCapacity : 1)
        { MaxDisplayAngle = GameSettings.Poles.MaxSwingAngle };

        void InitializeStates()
        {
            if (states != null) return;
            states = new Runtime[poles.Length];
            for (int i = 0; i < states.Length; i++) states[i] = NewRuntime(i);
        }

        void Update()
        {
            ConnectFlightEvents();
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += Connection;
                Connection(session.ConnectionState);
            }
            if (states == null) return;
            for (int i = 0; i < poles.Length; i++)
            {
                var runtime = states[i]; var pole = poles[i];
                if (pole == null) continue;
                if (session != null && session.IsHost)
                {
                    TickState(i);
                    for (int seat = 0; seat < runtime.State.Capacity; seat++)
                    {
                        ulong actor = runtime.State.OccupantAt(seat);
                        if (actor == CannonSeatState.Nobody) continue;
                        var player = FindPlayer(actor);
                        if (!pole.isActiveAndEnabled || !pole.InteractionAvailable || player == null || player.State != PlayerState.Normal)
                        {
                            ReleasePassengers(i, seat);
                            if (runtime.State.TryRelease(actor, runtime.State.Revision, Clock, pole.WarningSeconds))
                                Publish(i, PoleEvent.Abandoned, actor, seat);
                        }
                    }
                    float sharedDrive = RefreshSwingDrive(runtime);
                    pole.Step(ref runtime.Angle, ref runtime.Speed, sharedDrive,
                        Time.deltaTime, GameSettings.Base.Character.Gravity, runtime.Braking);
                }
                if (pole.LinkedGate != null)
                {
                    pole.LinkedGate.Present(runtime.State.Phase, pole.LinkedGate.CarrierPassed,
                        runtime.State.OccupantAt(0) != CannonSeatState.Nobody, Clock);
                    if (session != null && session.IsHost && pole.LinkedGate.HostTick(runtime.State.Phase, runtime.State.Occupant))
                        Publish(i, PoleEvent.Sync, CannonSeatState.Nobody);
                }
            }
            if (session != null && session.IsHost && Clock >= nextSnapshot)
            {
                double now = Clock;
                nextSnapshot = now + SnapshotInterval;
                for (int i = 0; i < poles.Length; i++)
                    if (poles[i] != null && states[i].SnapshotDue(now, (double)i / poles.Length))
                        Publish(i, PoleEvent.Sync, CannonSeatState.Nobody);
            }
        }

        public static float SharedDrive(float sum, int riders) => riders > 0 && float.IsFinite(sum) ? Mathf.Clamp(sum / riders, -1f, 1f) : 0f;

        void TickState(int index)
        {
            var runtime = states[index];
            for (int supportingSeat = 0; supportingSeat < runtime.State.Capacity; supportingSeat++)
            {
                ulong supportingActor = runtime.State.OccupantAt(supportingSeat);
                if (supportingActor == CannonSeatState.Nobody) continue;
                bool unsupported = supportingSeat > 0 && runtime.State.OccupantAt(supportingSeat - 1) == CannonSeatState.Nobody;
                double deadline = runtime.State.DeadlineAt(supportingSeat);
                if (!unsupported && (deadline <= 0 || Clock < deadline)) continue;
                // 아래부터 각각 다른 revision으로 놓아 원격에서도 모든 사람의 비행 승인을 받게 한다.
                ReleasePassengers(index, supportingSeat);
                if (runtime.State.TryRelease(supportingActor, runtime.State.Revision, Clock, poles[index].WarningSeconds))
                    Publish(index, PoleEvent.Released, supportingActor, supportingSeat);
            }
            // Tick removes at most one timed rider per call, giving each release its own revision.
            for (int pass = 0; pass <= PoleGateState.MaxRiders; pass++)
            {
                ulong a = runtime.State.OccupantAt(0), b = runtime.State.OccupantAt(1);
                ulong c = runtime.State.OccupantAt(2), d = runtime.State.OccupantAt(3);
                if (!runtime.State.Tick(Clock, poles[index].WarningSeconds)) break;
                int seat = a != CannonSeatState.Nobody && runtime.State.OccupantAt(0) != a ? 0
                    : b != CannonSeatState.Nobody && runtime.State.OccupantAt(1) != b ? 1
                    : c != CannonSeatState.Nobody && runtime.State.OccupantAt(2) != c ? 2
                    : d != CannonSeatState.Nobody && runtime.State.OccupantAt(3) != d ? 3 : -1;
                ulong actor = seat == 0 ? a : seat == 1 ? b : seat == 2 ? c : seat == 3 ? d : CannonSeatState.Nobody;
                Publish(index, seat >= 0 ? PoleEvent.Released : PoleEvent.Sync, actor, seat);
            }
        }

        void ReleasePassengers(int index, int supportingSeat, bool drop = false)
        {
            var state = states[index].State;
            for (int seat = state.Capacity - 1; seat > supportingSeat; seat--)
            {
                ulong actor = state.OccupantAt(seat);
                if (actor != CannonSeatState.Nobody && state.TryRelease(actor, state.Revision, Clock, poles[index].WarningSeconds))
                    Publish(index, PoleEvent.Released, actor, seat, drop);
            }
        }

        void LateUpdate()
        {
            if (states == null) return;
            for (int i = 0; i < poles.Length; i++)
            {
                if (poles[i] == null) continue;
                var runtime = states[i];
                float angle = runtime.DisplayAngle(Time.unscaledTimeAsDouble);
                poles[i].PresentSwing(angle);
                for (int seat = 0; seat < runtime.State.Capacity; seat++)
                {
                    var player = FindPlayer(runtime.State.OccupantAt(seat));
                    if (player != null && player.State == PlayerState.Normal && !player.IsLocal)
                        player.transform.SetPositionAndRotation(poles[i].HangingPosition(angle, seat), SeatRotation(i, seat));
                }
            }
        }

        void HandleRequest(ulong actor, PoleAction action, int index, uint revision, int station,
            uint launch, uint segment, float input, uint sequence, double observedElapsed)
        {
            if (session == null || !session.IsHost) return;
            if (action == PoleAction.Sync)
            {
                for (int i = 0; i < poles.Length; i++) if (poles[i] != null) SendState(actor, i, PoleEvent.Sync, CannonSeatState.Nobody);
                return;
            }
            if (!Valid(index)) return;
            TickState(index);
            var player = FindPlayer(actor); var cannon = HumanCannonSystem.Current; var runtime = states[index];
            if (player == null || player.State != PlayerState.Normal || cannon == null
                || revision > runtime.State.Revision || revision < runtime.ResetRevision) return;
            int seat = runtime.State.SeatOf(actor);
            if (action == PoleAction.Grab || action == PoleAction.PlatformGrab)
            {
                bool fromPlatform = action == PoleAction.PlatformGrab;
                if (runtime.LastDeparture.TryGetValue(actor, out uint departure) && revision < departure) return;
                if (!poles[index].InteractionAvailable || !CanApproach(player) || (fromPlatform ? !CanEnterFromPlatform(player) || cannon.IsFlying(actor)
                    : !cannon.MatchesFlight(actor, station, launch, segment))) return;
                seat = FindSeat(player, index);
                if (seat < 0 && observedElapsed >= 0) seat = FindSeat(player, index, observedElapsed);
                if (seat < 0) return;
                var stack = PiggybackSystem.Current;
                bool transferring = stack != null && stack.IsInStack(player);
                if (transferring && !stack.CanReleaseForDevice(player)) return;
                float hold = seat == 0 ? poles[index].HoldSeconds : Mathf.Max(.001f, (float)(runtime.State.DeadlineAt(0) - Clock));
                if (!runtime.State.TryGrab(actor, runtime.State.Revision, Clock, hold, false, seat)) return;
                if (fromPlatform) { station = CannonFlightState.PoleEntrySource; launch = ++platformLaunch; segment = 0; }
                if (runtime.State.Count == 1)
                {
                    runtime.ResetDrive();
                    runtime.Speed = poles[index].AddCatchMomentum(runtime.Angle, runtime.Speed, input);
                }
                runtime.Riders[seat] = new Rider { LastInput = Clock, Station = station, Launch = launch, Segment = segment,
                    JoinedRevision = runtime.State.Revision };
                void CompleteCatch()
                {
                    stack?.ConsumeTrapezeEntry(player);
                    Publish(index, fromPlatform ? PoleEvent.PlatformGrabbed : PoleEvent.Grabbed, actor, seat);
                }
                if (transferring) stack.TryReleaseForDevice(player, CompleteCatch);
                else CompleteCatch();
            }
            else if ((action == PoleAction.Release || action == PoleAction.Drop) && seat >= 0)
            {
                if (revision < runtime.Riders[seat].JoinedRevision) return;
                if (IsReleaseBlockedByPassenger(actor)) return;
                // A final Swing and Release can arrive in the same host frame. Use the
                // accepted inputs now rather than the direction from the previous Update.
                RefreshSwingDrive(runtime);
                bool drop = action == PoleAction.Drop;
                ReleasePassengers(index, seat, drop);
                if (runtime.State.TryRelease(actor, runtime.State.Revision, Clock, poles[index].WarningSeconds))
                    Publish(index, PoleEvent.Released, actor, seat, drop);
            }
            else if (action == PoleAction.Swing && GameSettings.Poles.CanDrive(seat))
            {
                var rider = runtime.Riders[seat];
                if (revision < rider.JoinedRevision || sequence <= rider.InputSequence || !float.IsFinite(input)) return;
                rider.Input = Mathf.Clamp(input, -1f, 1f);
                rider.InputSequence = sequence; rider.LastInput = Clock;
                RefreshSwingDrive(runtime);
            }
        }

        float RefreshSwingDrive(Runtime runtime)
        {
            float sum = 0f;
            int drivers = 0;
            for (int seat = 0; seat < runtime.State.Capacity; seat++)
            {
                if (!GameSettings.Poles.CanDrive(seat) || runtime.State.OccupantAt(seat) == CannonSeatState.Nobody) continue;
                drivers++;
                var rider = runtime.Riders[seat];
                if (Clock - rider.LastInput <= .3) sum += rider.Input;
            }
            float drive = SharedDrive(sum, drivers);
            runtime.ObserveDrive(drive);
            return drive;
        }

        void Apply(int index, PoleEvent action, ulong actor, int station = 0, uint launch = 0, uint segment = 0)
        {
            var runtime = states[index]; var cannon = HumanCannonSystem.Current;
            if (cannon == null) return;
            if (action == PoleEvent.Grabbed || action == PoleEvent.PlatformGrabbed)
            {
                flightOrigins.Remove(actor);
                if (action == PoleEvent.PlatformGrabbed) cannon.BeginPoleFlight(actor, launch);
                cannon.ApplyPoleGrab(actor, station, launch, segment, index);
                OccupancyChanged?.Invoke(index, actor, true);
            }
            else if (action == PoleEvent.Released)
            {
                flightOrigins[actor] = index;
                cannon.ApplyPoleRelease(actor, station, launch, segment, runtime.ReleasePosition, runtime.ReleaseRotation,
                    runtime.ReleaseVelocity, poles[index].AirControlSpeed);
                OccupancyChanged?.Invoke(index, actor, false);
                Released?.Invoke(index, actor);
            }
            else if (action == PoleEvent.Abandoned)
            {
                flightOrigins.Remove(actor);
                cannon.AbandonPole(actor);
                OccupancyChanged?.Invoke(index, actor, false);
            }
            else if (action == PoleEvent.Reset)
            {
                if (poles[index].LinkedGate != null) poles[index].LinkedGate.ResetGate();
                cannon.CancelLocalPole(index);
            }
            for (int seat = 0; seat < runtime.State.Capacity; seat++)
            {
                ulong occupant = runtime.State.OccupantAt(seat);
                if (occupant == CannonSeatState.Nobody) continue;
                var rider = runtime.Riders[seat];
                cannon.SyncPoleHold(occupant, rider.Station, rider.Launch, rider.Segment, index);
            }
        }

        void Restart(NetworkPlayer player)
        {
            if (session == null || !session.IsHost || states == null) return;
            for (int i = 0; i < states.Length; i++)
            {
                var runtime = states[i];
                runtime.State.Reset(); runtime.Angle = runtime.Speed = 0f;
                runtime.ResetDrive();
                runtime.ResetRevision = runtime.State.Revision; runtime.LastDeparture.Clear();
                for (int seat = 0; seat < runtime.Riders.Length; seat++) runtime.Riders[seat] = new Rider();
                if (poles[i] != null) Publish(i, PoleEvent.Reset, CannonSeatState.Nobody);
            }
        }

        public void ServerReleaseForInspection(ulong actor)
        {
            if (session == null || !session.IsHost) return;
            int index = HeldBy(actor);
            if (index < 0) return;
            int seat = states[index].State.SeatOf(actor);
            ReleasePassengers(index, seat);
            if (states[index].State.TryRelease(actor, states[index].State.Revision, Clock, poles[index].WarningSeconds))
                Publish(index, PoleEvent.Abandoned, actor, seat);
        }

        bool Valid(int index) => index >= 0 && states != null && index < states.Length && poles[index] != null && poles[index].isActiveAndEnabled;
        static NetworkPlayer FindPlayer(ulong id)
        {
            if (id == CannonSeatState.Nobody) return null;
            var players = NetworkPlayer.All;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            }
            return null;
        }
        void OnDisable()
        {
            if (observedFlightSystem != null) observedFlightSystem.LandingConfirmed -= OnLandingConfirmed;
            observedFlightSystem = null;
            flightOrigins.Clear();
            NetworkPlayer.Restarted -= Restart;
            Unregister();
            if (session != null) session.ConnectionStateChanged -= Connection;
            for (int i = 0; states != null && i < states.Length; i++)
            {
                HumanCannonSystem.Current?.CancelLocalPole(i);
                if (poles[i] != null) poles[i].HidePresentation();
            }
            session = null;
            states = null;
            if (Current == this) Current = null;
        }
    }
}
