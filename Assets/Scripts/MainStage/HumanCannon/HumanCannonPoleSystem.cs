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
        [Tooltip("이 레벨에서는 지상·일반 점프·낙하 중 가까운 원본 봉을 F로 잡는다.")]
        [SerializeField] bool allowPlatformJumpGrab;
        [Tooltip("탈출로: 외줄 끝/점프/목마 역할과 무관하게 가까운 봉으로 전환한다.")]
        [SerializeField] bool independentInteraction;
        public bool IndependentInteraction => independentInteraction;
        uint platformLaunch;
        sealed class Rider
        {
            public float Input;
            public PoleSwingInput SwingInput;
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
            public float Angle, Speed;
            public Vector3 ReleasePosition, ReleaseVelocity;
            public Quaternion ReleaseRotation = Quaternion.identity;
            public Runtime(int capacity)
            {
                State = new PoleGateState(capacity);
                for (int i = 0; i < Riders.Length; i++) Riders[i] = new Rider();
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
                && (independentInteraction || stack == null || !stack.IsInStack(player) || stack.CanEnterTrapeze(player))
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

        internal void RequestGrab(int index, int station, uint launch, uint segment) =>
            SendRequest(PoleAction.Grab, index, station, launch, segment, 0f, 0);
        internal bool CanEnterFromPlatform(NetworkPlayer player) => allowPlatformJumpGrab && player != null
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
        public Vector3 HangingPosition(int index) => poles[index].HangingPosition(states[index].Angle);
        public Vector3 HangingPosition(int index, ulong actor)
        {
            int seat = states[index].State.SeatOf(actor);
            return seat >= 0 ? poles[index].HangingPosition(states[index].Angle, seat) : HangingPosition(index);
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
                        var rider = runtime.Riders[seat];
                        float input = Clock - rider.LastInput <= .3 ? rider.Input : 0f;
                        drive += rider.SwingInput.Step(input, Time.deltaTime);
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
            if (session != null && session.IsHost && Clock >= nextSnapshot)
            {
                nextSnapshot = Clock + .05;
                for (int i = 0; i < poles.Length; i++) if (poles[i] != null) Publish(i, PoleEvent.Sync, CannonSeatState.Nobody);
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
                poles[i].PresentSwing(runtime.State.IsOccupied ? runtime.Angle : 0f);
                for (int seat = 0; seat < runtime.State.Capacity; seat++)
                {
                    var player = FindPlayer(runtime.State.OccupantAt(seat));
                    if (player != null && player.State == PlayerState.Normal && !player.IsLocal)
                        player.transform.SetPositionAndRotation(poles[i].HangingPosition(runtime.Angle, seat), poles[i].Grip.rotation);
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
                if (!CanApproach(player) || (fromPlatform ? !CanEnterFromPlatform(player) || cannon.IsFlying(actor)
                    : !cannon.MatchesFlight(actor, station, launch, segment))) return;
                seat = FindSeat(player, index);
                if (seat < 0 && observedElapsed >= 0) seat = FindSeat(player, index, observedElapsed);
                if (seat < 0) return;
                var stack = PiggybackSystem.Current;
                bool transferring = stack != null && stack.IsInStack(player);
                if (transferring && !(independentInteraction ? stack.CanReleaseForPole(player) : stack.CanEnterTrapeze(player))) return;
                if (!runtime.State.TryGrab(actor, runtime.State.Revision, Clock, poles[index].HoldSeconds, poles[index].LinkedGate != null, seat)) return;
                if (fromPlatform) { station = CannonFlightState.PoleEntrySource; launch = ++platformLaunch; segment = 0; }
                if (runtime.State.Count == 1) runtime.Angle = runtime.Speed = 0f;
                runtime.Riders[seat] = new Rider { LastInput = Clock, Station = station, Launch = launch, Segment = segment, JoinedRevision = runtime.State.Revision };
                void CompleteCatch()
                {
                    stack?.ConsumeTrapezeEntry(player);
                    Publish(index, fromPlatform ? PoleEvent.PlatformGrabbed : PoleEvent.Grabbed, actor, seat);
                }
                if (transferring && independentInteraction) stack.TryReleaseForPole(player, CompleteCatch);
                else if (transferring) stack.TryReleaseTopForTrapeze(player, CompleteCatch);
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
