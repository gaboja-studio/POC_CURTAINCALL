using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPoleSystem
    {
        enum PoleAction : byte { Sync, Grab, Release, Swing, PlatformGrab, Drop }
        enum PoleEvent : byte { Sync, Grabbed, Released, Abandoned, Reset, PlatformGrabbed }
        const string RequestMessage = "CurtainCall.HumanCannon.Pole.Request";
        const string StateMessage = "CurtainCall.HumanCannon.Pole.State";
        const int RequestSize = 37;
        const int StateSize = 164; // 52 byte header + four fixed 28 byte rider records.
        const int ReleaseStateSize = StateSize + 40;
        const byte GateOpenFlag = 1 << (PoleGateState.MaxRiders + 1);
        CustomMessagingManager messaging;
        struct ReceivedFlight
        {
            public int Station;
            public uint Launch, Segment;
        }
        // NGO dispatches on the main thread. Stage the entire packet before touching live seat data.
        readonly ulong[] receivedActors = new ulong[PoleGateState.MaxRiders];
        readonly double[] receivedDeadlines = new double[PoleGateState.MaxRiders];
        readonly ReceivedFlight[] receivedFlights = new ReceivedFlight[PoleGateState.MaxRiders];

        void SendRequest(PoleAction action, int index, int station, uint launch, uint segment, float input, uint sequence)
        {
            var player = NetworkPlayer.Local;
            if (player == null || player.State != PlayerState.Normal || InputGate.IsBlocked || !Valid(index)) return;
            if (action is PoleAction.Grab or PoleAction.PlatformGrab) CurtainCall.Telemetry.Pipeline.TelemetryRuntime.PerformanceInteractionAttempt(player,poles[index],"POLE");
            uint revision = states[index].State.Revision;
            double observedElapsed = poles[index].Loop != null ? poles[index].Loop.Elapsed : -1d;
            if (session != null && session.IsHost)
                HandleRequest(player.OwnerClientId, action, index, revision, station, launch, segment, input, sequence, observedElapsed);
            else if (messaging != null)
            {
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                WriteRequest(writer, action, WireIndex(index), revision, station, launch, segment, input, sequence, observedElapsed);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        static void WriteRequest(FastBufferWriter writer, PoleAction action, int index, uint revision,
            int station, uint launch, uint segment, float input, uint sequence, double observedElapsed)
        {
            writer.WriteValueSafe((byte)action); writer.WriteValueSafe(index); writer.WriteValueSafe(revision);
            writer.WriteValueSafe(station); writer.WriteValueSafe(launch); writer.WriteValueSafe(segment);
            writer.WriteValueSafe(input); writer.WriteValueSafe(sequence); writer.WriteValueSafe(observedElapsed);
        }

        void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out int index); reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out int station); reader.ReadValueSafe(out uint launch); reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out float input); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out double observedElapsed);
            if (action > (byte)PoleAction.Drop) return;
            HandleRequest(sender, (PoleAction)action, LocalIndex(index), revision, station, launch, segment, input, sequence, observedElapsed);
        }

        void Publish(int index, PoleEvent action, ulong actor, int seat = -1, bool drop = false)
        {
            var runtime = states[index];
            var rider = seat >= 0 ? runtime.Riders[seat] : null;
            int station = rider != null ? rider.Station : 0;
            uint launch = rider != null ? rider.Launch : 0, segment = rider != null ? rider.Segment : 0;
            if (action == PoleEvent.Released)
            {
                var pole = poles[index];
                pole.RefreshMovingPose();
                runtime.ReleasePosition = pole.HangingPosition(runtime.Angle, seat);
                // TryRelease has cleared the occupant, but this seat still holds its departure facing.
                runtime.ReleaseRotation = SeatRotation(index, seat);
                runtime.ReleaseVelocity = drop ? Vector3.zero : CannonAirControl.LimitPoleHorizontalSpeed(
                    pole.ReleaseVelocity(runtime.Angle, runtime.Speed, seat, runtime.ReleaseDirection),
                    pole.FlightSpeedLimit(GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit));
            }
            if (rider != null && (action == PoleEvent.Grabbed || action == PoleEvent.PlatformGrabbed || action == PoleEvent.Released))
                rider.Segment++;
            if (action == PoleEvent.Released || action == PoleEvent.Abandoned) runtime.LastDeparture[actor] = runtime.State.Revision;
            Apply(index, action, actor, station, launch, segment);
            if (messaging == null || session.NetworkManager.ConnectedClientsIds.Count <= 1) return;
            // All recipients receive identical authoritative bytes. Serialize once, including
            // four rider records, instead of rebuilding the same packet for each client.
            SendState(NetworkManager.ServerClientId, index, action, actor, seat, station, launch, segment, true);
        }

        void SendState(ulong client, int index, PoleEvent action, ulong actor, int seat = -1, int station = 0, uint launch = 0, uint segment = 0, bool broadcast = false)
        {
            var runtime = states[index];
            using var writer = new FastBufferWriter(action == PoleEvent.Released ? ReleaseStateSize : StateSize, Allocator.Temp);
            writer.WriteValueSafe((byte)action); writer.WriteValueSafe(WireIndex(index)); writer.WriteValueSafe(runtime.State.Revision);
            writer.WriteValueSafe((byte)runtime.State.Phase); writer.WriteValueSafe(runtime.State.RearmAt);
            writer.WriteValueSafe(runtime.Angle); writer.WriteValueSafe(runtime.Speed); writer.WriteValueSafe(actor);
            writer.WriteValueSafe(runtime.ReleaseDirection);
            writer.WriteValueSafe((byte)(seat < 0 ? 255 : seat));
            writer.WriteValueSafe(station); writer.WriteValueSafe(launch); writer.WriteValueSafe(segment);
            // 비트 0: 지상 담당 통과, 비트 1~4: 예약(항상 0), 비트 5: 상단 점유로 톱벽이 열려 있음.
            var gate = poles[index].LinkedGate;
            byte flags = (byte)(gate != null && gate.CarrierPassed ? 1 : 0);
            if (gate != null && gate.IsOpen) flags |= GateOpenFlag;
            writer.WriteValueSafe(flags);
            for (int slot = 0; slot < PoleGateState.MaxRiders; slot++)
            {
                bool occupied = runtime.State.OccupantAt(slot) != CannonSeatState.Nobody;
                var rider = runtime.Riders[slot];
                writer.WriteValueSafe(runtime.State.OccupantAt(slot)); writer.WriteValueSafe(runtime.State.DeadlineAt(slot));
                writer.WriteValueSafe(occupied ? rider.Station : 0); writer.WriteValueSafe(occupied ? rider.Launch : 0u);
                writer.WriteValueSafe(occupied ? rider.Segment : 0u);
            }
            if (action == PoleEvent.Released)
            {
                writer.WriteValueSafe(runtime.ReleasePosition); writer.WriteValueSafe(runtime.ReleaseRotation); writer.WriteValueSafe(runtime.ReleaseVelocity);
            }
            if (broadcast)
            {
                foreach (ulong recipient in session.NetworkManager.ConnectedClientsIds)
                    if (recipient != NetworkManager.ServerClientId)
                        messaging.SendNamedMessage(StateMessage, recipient, writer, NetworkDelivery.ReliableSequenced);
            }
            else messaging.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            int bytes = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || states == null || (bytes != StateSize && bytes != ReleaseStateSize)) return;
            reader.ReadValueSafe(out byte action);
            if (action > (byte)PoleEvent.PlatformGrabbed || bytes != (action == (byte)PoleEvent.Released ? ReleaseStateSize : StateSize)) return;
            reader.ReadValueSafe(out int index); reader.ReadValueSafe(out uint revision);
            index = LocalIndex(index);
            reader.ReadValueSafe(out byte phase); reader.ReadValueSafe(out double rearmAt);
            reader.ReadValueSafe(out float angle); reader.ReadValueSafe(out float speed); reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out float releaseDirection);
            reader.ReadValueSafe(out byte seat); reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch); reader.ReadValueSafe(out uint segment); reader.ReadValueSafe(out byte flags);
            int reversedSeats = (flags >> 1) & ((1 << PoleGateState.MaxRiders) - 1);
            if (index < 0 || index >= states.Length || poles[index] == null
                || (flags & ~(1 | GateOpenFlag | (((1 << PoleGateState.MaxRiders) - 1) << 1))) != 0
                || reversedSeats != 0
                || !float.IsFinite(angle) || !float.IsFinite(speed)
                || !float.IsFinite(releaseDirection) || Mathf.Abs(releaseDirection) > 1f) return;
            var runtime = states[index];
            bool riderEvent = action == (byte)PoleEvent.Grabbed || action == (byte)PoleEvent.PlatformGrabbed
                || action == (byte)PoleEvent.Released || action == (byte)PoleEvent.Abandoned;
            if (riderEvent ? actor == CannonSeatState.Nobody || seat >= runtime.State.Capacity
                : actor != CannonSeatState.Nobody || seat != 255) return;
            for (int slot = 0; slot < PoleGateState.MaxRiders; slot++)
            {
                reader.ReadValueSafe(out receivedActors[slot]); reader.ReadValueSafe(out receivedDeadlines[slot]);
                ref var flight = ref receivedFlights[slot];
                reader.ReadValueSafe(out flight.Station); reader.ReadValueSafe(out flight.Launch); reader.ReadValueSafe(out flight.Segment);
                if (receivedActors[slot] == CannonSeatState.Nobody && (flight.Station != 0 || flight.Launch != 0
                    || flight.Segment != 0 || (reversedSeats & (1 << slot)) != 0)) return;
            }
            if ((action == (byte)PoleEvent.Grabbed || action == (byte)PoleEvent.PlatformGrabbed)
                && (receivedActors[seat] != actor || receivedFlights[seat].Station != station || receivedFlights[seat].Launch != launch
                    || receivedFlights[seat].Segment != segment + 1)) return;
            if ((action == (byte)PoleEvent.Released || action == (byte)PoleEvent.Abandoned))
                for (int slot = 0; slot < receivedActors.Length; slot++) if (receivedActors[slot] == actor) return;
            Vector3 releasePosition = default, releaseVelocity = default;
            Quaternion releaseRotation = Quaternion.identity;
            if (action == (byte)PoleEvent.Released)
            {
                reader.ReadValueSafe(out releasePosition); reader.ReadValueSafe(out releaseRotation); reader.ReadValueSafe(out releaseVelocity);
                if (!FiniteReleaseVector(releasePosition) || !FiniteReleaseVector(releaseVelocity) || !FiniteReleaseRotation(releaseRotation)) return;
            }
            if ((PoleEvent)action != PoleEvent.Sync && revision <= runtime.State.Revision) return;
            bool continuousSwing = (PoleEvent)action == PoleEvent.Sync && revision == runtime.State.Revision;
            if (!runtime.State.ReceiveCrew(revision, receivedActors, receivedDeadlines, (PoleSawPhase)phase, rearmAt)) return;
            runtime.ReceiveAngle(angle, Time.unscaledTimeAsDouble, continuousSwing);
            runtime.Speed = speed;
            runtime.ReleaseDirection = releaseDirection;
            runtime.MaxDisplayAngle = poles[index].MaxSwingAngle;
            for (int slot = 0; slot < receivedFlights.Length; slot++)
            {
                var rider = runtime.Riders[slot];
                rider.Station = receivedFlights[slot].Station;
                rider.Launch = receivedFlights[slot].Launch;
                rider.Segment = receivedFlights[slot].Segment;
            }
            if (action == (byte)PoleEvent.Released)
            {
                runtime.ReleasePosition = releasePosition; runtime.ReleaseRotation = releaseRotation; runtime.ReleaseVelocity = releaseVelocity;
            }
            if (poles[index].LinkedGate != null)
                poles[index].LinkedGate.Present((PoleSawPhase)phase, (flags & 1) != 0,
                    states[index].State.OccupantAt(0) != CannonSeatState.Nobody, Clock);
            Apply(index, (PoleEvent)action, actor, station, launch, segment);
        }

        void Connection(SessionConnectionState connection)
        {
            Unregister();
            if (connection == SessionConnectionState.Offline)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    states[i] = NewRuntime(i);
                    if (poles[i] != null && poles[i].LinkedGate != null) poles[i].LinkedGate.ResetGate();
                    HumanCannonSystem.Current?.CancelLocalPole(i);
                }
                inputSequence = 0; lastInputDirection = 0; nextInput = nextSnapshot = 0;
                return;
            }
            if (connection != SessionConnectionState.Host && connection != SessionConnectionState.Client) return;
            messaging = session.NetworkManager.CustomMessagingManager;
            if (connection == SessionConnectionState.Host) messaging.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, ReceiveState);
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                WriteRequest(writer, PoleAction.Sync, -1, 0, 0, 0, 0, 0f, 0, -1d);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage); messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging = null;
        }
        static bool FiniteReleaseVector(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static bool FiniteReleaseRotation(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y)
            && float.IsFinite(q.z) && float.IsFinite(q.w) && Quaternion.Dot(q, q) > .001f;
    }
}
