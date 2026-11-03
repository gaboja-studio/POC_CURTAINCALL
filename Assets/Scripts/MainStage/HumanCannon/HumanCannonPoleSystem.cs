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
    [DefaultExecutionOrder(-20)]
    public sealed partial class HumanCannonPoleSystem : MonoBehaviour
    {
        [SerializeField] HumanCannonPole[] poles = Array.Empty<HumanCannonPole>();
        [Tooltip("봉을 잡을 때 바라본 쪽을 각 좌석의 앞쪽으로 사용한다."), SerializeField] bool useGrabFacing;
        // Legacy authoring fields. Grab/transfer rules are shared across all scenes.
        [SerializeField, HideInInspector] bool allowPlatformJumpGrab;
        [SerializeField, HideInInspector] bool independentInteraction;
        public bool IndependentInteraction => true;
        uint platformLaunch;
        const double SnapshotInterval = .05d;
        sealed class Rider
        {
            public float Input;
            public PoleSwingInput SwingInput;
            public double LastInput;
            public uint InputSequence;
            public uint JoinedRevision;
            public bool ReverseFacing;
            public int Station;
            public uint Launch, Segment;
        }
        /// <summary>호스트: 제한 시간이 있는 봉(톱날 연동 봉 등)을 잡은 사람의 끈끈이 장갑(Task-092).</summary>
        readonly CurtainCall.WaitingRoom.StickyGloveHolds stickyGloves = new();

        sealed class Runtime
        {
            public readonly PoleGateState State;
            public readonly Rider[] Riders = new Rider[PoleGateState.MaxRiders];
            public readonly Dictionary<ulong, uint> LastDeparture = new();
            public uint ResetRevision;
            public float Angle, Speed;
            double nextIdleSnapshot;
            double receivedAngleAt = double.NegativeInfinity;
            float displayedFrom;
            public Vector3 ReleasePosition, ReleaseVelocity;
            public Quaternion ReleaseRotation = Quaternion.identity;
            public Runtime(int capacity)
            {
                State = new PoleGateState(capacity);
                for (int i = 0; i < Riders.Length; i++) Riders[i] = new Rider();
            }

            public bool SnapshotDue(double now, double idlePhase)
            {
                if (State.Count != 0) return true;
                if (now < nextIdleSnapshot) return false;
                // Recover missed initial sync without sending every stationary pole in one burst.
                nextIdleSnapshot = Math.Floor(now - idlePhase) + 1d + idlePhase;
                return true;
            }

            public float DisplayAngle(double now)
            {
                if (now >= receivedAngleAt + SnapshotInterval) return Angle;
                return Mathf.Lerp(displayedFrom, Angle, (float)((now - receivedAngleAt) / SnapshotInterval));
            }

            public void ReceiveAngle(float angle, double now, bool continuous)
            {
                // Snap when ownership changes or a stalled connection catches up.
                displayedFrom = continuous && now - receivedAngleAt <= SnapshotInterval * 5
                    ? DisplayAngle(now) : angle;
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
        public bool CanGrab(int index, NetworkPlayer player) => Valid(index) && FindSeat(player, index) >= 0;
        public Vector3 InteractionPoint(int index, NetworkPlayer player)
        {
            int seat = FindSeat(player, index);
            return poles[index].BarPosition(SwingAngle(index)) + (seat >= 0 ? poles[index].SeatOffset(seat) : Vector3.zero);
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
                && (stack == null || !stack.IsInStack(player) || stack.CanReleaseForPole(player))
                && HumanCannonCarry.Held(player.OwnerClientId) == null;
        }

        public int FindGrabbable(NetworkPlayer player)
        {
            if (!CanApproach(player)) return -1;
            int closest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < poles.Length; i++)
            {
                if (!Valid(i)) continue;
                int seat = FindSeat(player, i);
                if (seat < 0) continue;
                float candidate = (poles[i].BarPosition(SwingAngle(i)) + poles[i].SeatOffset(seat) - PlayerInteraction.Center(player)).sqrMagnitude;
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
                if (state.OccupantAt(seat) != CannonSeatState.Nobody
                    || !poles[index].IsSeatInReach(player, SwingAngle(index), seat, observedElapsed)) continue;
                float candidate = (poles[index].BarPosition(SwingAngle(index)) + poles[index].SeatOffset(seat) - PlayerInteraction.Center(player)).sqrMagnitude;
                if (candidate < distance) { distance = candidate; closest = seat; }
            }
            return closest;
        }
        float SwingAngle(int index) => states[index].State.IsOccupied ? states[index].Angle : 0f;

        static bool ReversedAtGrab(Vector3 poleForward, Vector3 view, Vector3 body, Vector3 approach)
        {
            float Projection(Vector3 direction) => Vector3.Dot(Vector3.ProjectOnPlane(direction, Vector3.up).normalized, poleForward);
            float facing = Projection(view);
            if (Mathf.Abs(facing) < .05f) facing = Projection(body);
            if (Mathf.Abs(facing) < .05f) facing = Projection(approach);
            return facing < -.0001f;
        }

        public Quaternion RiderRotation(int index, ulong actor)
        {
            int seat = states[index].State.SeatOf(actor);
            return SeatRotation(index, seat);
        }

        Quaternion SeatRotation(int index, int seat)
        {
            Quaternion grip = poles[index].Grip.rotation;
            return seat >= 0 && states[index].Riders[seat].ReverseFacing ? grip * Quaternion.Euler(0, 180, 0) : grip;
        }

        internal void RequestGrab(int index, int station, uint launch, uint segment) =>
            SendRequest(PoleAction.Grab, index, station, launch, segment, 0f, 0);
        internal bool CanEnterFromPlatform(NetworkPlayer player) => player != null
            && player.IsSpawned && player.State == PlayerState.Normal;
        internal void RequestPlatformGrab(int index) => SendRequest(PoleAction.PlatformGrab, index, 0, 0, 0, 0, 0);
        internal void RequestRelease(int index) => SendRequest(PoleAction.Release, index, 0, 0, 0, 0f, 0);
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
            states = new Runtime[poles.Length];
            for (int i = 0; i < states.Length; i++) states[i] = NewRuntime(i);
            NetworkPlayer.Restarted += Restart;
        }
        Runtime NewRuntime(int index) => new Runtime(poles[index] != null ? poles[index].RiderCapacity : 1);

        void Update()
        {
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
                    float drive = 0f;
                    for (int seat = 0; seat < runtime.State.Capacity; seat++)
                    {
                        ulong actor = runtime.State.OccupantAt(seat);
                        if (actor == CannonSeatState.Nobody) continue;
                        var player = FindPlayer(actor);
                        if (!pole.isActiveAndEnabled || player == null || player.State != PlayerState.Normal)
                        {
                            if (runtime.State.TryRelease(actor, runtime.State.Revision, Clock, pole.WarningSeconds))
                                Publish(i, PoleEvent.Abandoned, actor, seat);
                            continue;
                        }
                        var route = pole.Loop;
                        if (route != null && route.AtUnloadingBay(route.Elapsed))
                        {
                            if (runtime.State.TryRelease(actor, runtime.State.Revision, Clock, pole.WarningSeconds))
                                Publish(i, PoleEvent.Released, actor, seat);
                            continue;
                        }
                        var rider = runtime.Riders[seat];
                        float input = Clock - rider.LastInput <= .3 ? rider.Input : 0f;
                        if (rider.ReverseFacing) input = -input;
                        drive += pole.SwingDrive(ref rider.SwingInput, input, Time.deltaTime);
                    }
                    // 같은 방향·같은 박자로 힘을 모아야 원래 반동이 난다. 반대 입력은 상쇄한다.
                    if (runtime.State.Count > 0)
                        pole.Step(ref runtime.Angle, ref runtime.Speed, SharedDrive(drive, runtime.State.Count),
                            Time.deltaTime, GameSettings.Base.Character.Gravity);
                }
                if (pole.LinkedGate != null)
                {
                    pole.LinkedGate.Present(runtime.State.Phase, pole.LinkedGate.CarrierPassed, Clock);
                    if (session != null && session.IsHost && pole.LinkedGate.HostTick(runtime.State.Phase, runtime.State.Occupant))
                        Publish(i, PoleEvent.Sync, CannonSeatState.Nobody);
                }
            }
            if (session != null && session.IsHost)
                stickyGloves.Tick(Clock, actor => { foreach (var s in states) if (s != null && s.State.SeatOf(actor) >= 0) return true; return false; });
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

        void LateUpdate()
        {
            if (states == null) return;
            for (int i = 0; i < poles.Length; i++)
            {
                if (poles[i] == null) continue;
                var runtime = states[i];
                float angle = runtime.State.IsOccupied ? runtime.DisplayAngle(Time.unscaledTimeAsDouble) : 0f;
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
                if (useGrabFacing && input != 1f && input != -1f) return;
                if (runtime.LastDeparture.TryGetValue(actor, out uint departure) && revision < departure) return;
                if (!CanApproach(player) || (fromPlatform ? !CanEnterFromPlatform(player) || cannon.IsFlying(actor)
                    : !cannon.MatchesFlight(actor, station, launch, segment))) return;
                seat = FindSeat(player, index);
                if (seat < 0 && observedElapsed >= 0) seat = FindSeat(player, index, observedElapsed);
                if (seat < 0) return;
                var stack = PiggybackSystem.Current;
                bool transferring = stack != null && stack.IsInStack(player);
                if (transferring && !stack.CanReleaseForPole(player)) return;
                // 제한 시간이 있는 봉이면 끈끈이 장갑으로 늘린다(Task-092). 무제한(0) 봉은 그대로.
                float hold = poles[index].HoldSeconds;
                if (hold > 0f) hold += (float)stickyGloves.Begin(actor, Clock, hold);
                if (!runtime.State.TryGrab(actor, runtime.State.Revision, Clock, hold, poles[index].LinkedGate != null, seat)) return;
                if (fromPlatform) { station = CannonFlightState.PoleEntrySource; launch = ++platformLaunch; segment = 0; }
                if (runtime.State.Count == 1) runtime.Angle = runtime.Speed = 0f;
                runtime.Riders[seat] = new Rider { LastInput = Clock, Station = station, Launch = launch, Segment = segment,
                    JoinedRevision = runtime.State.Revision, ReverseFacing = useGrabFacing && input < 0f };
                void CompleteCatch()
                {
                    stack?.ConsumeTrapezeEntry(player);
                    Publish(index, fromPlatform ? PoleEvent.PlatformGrabbed : PoleEvent.Grabbed, actor, seat);
                }
                if (transferring) stack.TryReleaseForPole(player, CompleteCatch);
                else CompleteCatch();
            }
            else if (action == PoleAction.Release && seat >= 0)
            {
                if (revision >= runtime.Riders[seat].JoinedRevision
                    && runtime.State.TryRelease(actor, runtime.State.Revision, Clock, poles[index].WarningSeconds))
                    Publish(index, PoleEvent.Released, actor, seat);
            }
            else if (action == PoleAction.Swing && seat >= 0)
            {
                var rider = runtime.Riders[seat];
                if (revision < rider.JoinedRevision || sequence <= rider.InputSequence || !float.IsFinite(input)) return;
                rider.Input = Mathf.Clamp(input, -1f, 1f);
                rider.SwingInput.Observe(rider.Input);
                rider.InputSequence = sequence; rider.LastInput = Clock;
            }
        }

        void Apply(int index, PoleEvent action, ulong actor, int station = 0, uint launch = 0, uint segment = 0)
        {
            var runtime = states[index]; var cannon = HumanCannonSystem.Current;
            if (cannon == null) return;
            if (action == PoleEvent.Grabbed || action == PoleEvent.PlatformGrabbed)
            {
                if (action == PoleEvent.PlatformGrabbed) cannon.BeginPoleFlight(actor, launch);
                cannon.ApplyPoleGrab(actor, station, launch, segment, index);
            }
            else if (action == PoleEvent.Released)
                cannon.ApplyPoleRelease(actor, station, launch, segment, runtime.ReleasePosition, runtime.ReleaseRotation,
                    runtime.ReleaseVelocity, poles[index].AirControlSpeed);
            else if (action == PoleEvent.Abandoned) cannon.AbandonPole(actor);
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
                stickyGloves.Clear();
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
            if (states[index].State.TryRelease(actor, states[index].State.Revision, Clock, poles[index].WarningSeconds))
                Publish(index, PoleEvent.Abandoned, actor, seat);
        }

        bool Valid(int index) => index >= 0 && states != null && index < states.Length && poles[index] != null && poles[index].isActiveAndEnabled;
        static NetworkPlayer FindPlayer(ulong id)
        {
            if (id != CannonSeatState.Nobody) foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }
        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restart;
            Unregister();
            if (session != null) session.ConnectionStateChanged -= Connection;
            for (int i = 0; states != null && i < states.Length; i++)
            {
                HumanCannonSystem.Current?.CancelLocalPole(i);
                if (poles[i] != null) poles[i].PresentSwing(0f);
            }
            session = null;
            if (Current == this) Current = null;
        }
    }
}
