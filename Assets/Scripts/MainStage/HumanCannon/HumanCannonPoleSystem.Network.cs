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
        enum PoleAction : byte { Sync, Grab, Release, Swing, PlatformGrab }
        enum PoleEvent : byte { Sync, Grabbed, Released, Abandoned, Reset, PlatformGrabbed }
        const string RequestMessage = "CurtainCall.HumanCannon.Pole.Request";
        const string StateMessage = "CurtainCall.HumanCannon.Pole.State";
        const int RequestSize = 37;
        const int StateSize = 160; // 48 byte header + four fixed 28 byte rider records.
        const int ReleaseStateSize = StateSize + 40;
        CustomMessagingManager messaging;

        void SendRequest(PoleAction action, int index, int station, uint launch, uint segment, float input, uint sequence)
        {
            var player = NetworkPlayer.Local;
            if (player == null || player.State != PlayerState.Normal || InputGate.IsBlocked || !Valid(index)) return;
            uint revision = states[index].State.Revision;
            double observedElapsed = poles[index].Loop != null ? poles[index].Loop.Elapsed : -1d;
            if (session != null && session.IsHost)
                HandleRequest(player.OwnerClientId, action, index, revision, station, launch, segment, input, sequence, observedElapsed);
            else if (messaging != null)
            {
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                WriteRequest(writer, action, index, revision, station, launch, segment, input, sequence, observedElapsed);
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
            if (action > (byte)PoleAction.PlatformGrab) return;
            HandleRequest(sender, (PoleAction)action, index, revision, station, launch, segment, input, sequence, observedElapsed);
        }

        void Publish(int index, PoleEvent action, ulong actor, int seat = -1)
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
                runtime.ReleaseRotation = pole.Grip.rotation;
                runtime.ReleaseVelocity = CannonAirControl.LimitPoleHorizontalSpeed(
                    pole.ReleaseVelocity(runtime.Angle, runtime.Speed, seat), GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit);
            }
            if (rider != null && (action == PoleEvent.Grabbed || action == PoleEvent.PlatformGrabbed || action == PoleEvent.Released))
                rider.Segment++;
            if (action == PoleEvent.Released || action == PoleEvent.Abandoned) runtime.LastDeparture[actor] = runtime.State.Revision;
            Apply(index, action, actor, station, launch, segment);
            if (messaging == null) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, index, action, actor, seat, station, launch, segment);
        }

        void SendState(ulong client, int index, PoleEvent action, ulong actor, int seat = -1, int station = 0, uint launch = 0, uint segment = 0)
        {
            var runtime = states[index];
            using var writer = new FastBufferWriter(action == PoleEvent.Released ? ReleaseStateSize : StateSize, Allocator.Temp);
            writer.WriteValueSafe((byte)action); writer.WriteValueSafe(index); writer.WriteValueSafe(runtime.State.Revision);
            writer.WriteValueSafe((byte)runtime.State.Phase); writer.WriteValueSafe(runtime.State.RearmAt);
            writer.WriteValueSafe(runtime.Angle); writer.WriteValueSafe(runtime.Speed); writer.WriteValueSafe(actor);
            writer.WriteValueSafe((byte)(seat < 0 ? 255 : seat));
            writer.WriteValueSafe(station); writer.WriteValueSafe(launch); writer.WriteValueSafe(segment);
            writer.WriteValueSafe((byte)(poles[index].LinkedGate != null && poles[index].LinkedGate.CarrierPassed ? 1 : 0));
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
            messaging.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            int bytes = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || states == null || (bytes != StateSize && bytes != ReleaseStateSize)) return;
            reader.ReadValueSafe(out byte action);
            if (action > (byte)PoleEvent.PlatformGrabbed || bytes != (action == (byte)PoleEvent.Released ? ReleaseStateSize : StateSize)) return;
            reader.ReadValueSafe(out int index); reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out byte phase); reader.ReadValueSafe(out double rearmAt);
            reader.ReadValueSafe(out float angle); reader.ReadValueSafe(out float speed); reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out byte seat); reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch); reader.ReadValueSafe(out uint segment); reader.ReadValueSafe(out byte passed);
            if (index < 0 || index >= states.Length || poles[index] == null || passed > 1
                || !float.IsFinite(angle) || !float.IsFinite(speed)) return;
            var runtime = states[index];
            bool riderEvent = action == (byte)PoleEvent.Grabbed || action == (byte)PoleEvent.PlatformGrabbed
                || action == (byte)PoleEvent.Released || action == (byte)PoleEvent.Abandoned;
            if (riderEvent ? actor == CannonSeatState.Nobody || seat >= runtime.State.Capacity
                : actor != CannonSeatState.Nobody || seat != 255) return;
            var actors = new ulong[PoleGateState.MaxRiders]; var holds = new double[PoleGateState.MaxRiders];
            var riders = new Rider[PoleGateState.MaxRiders];
            for (int slot = 0; slot < PoleGateState.MaxRiders; slot++)
            {
                reader.ReadValueSafe(out actors[slot]); reader.ReadValueSafe(out holds[slot]);
                reader.ReadValueSafe(out int source); reader.ReadValueSafe(out uint flight); reader.ReadValueSafe(out uint part);
                if (actors[slot] == CannonSeatState.Nobody && (source != 0 || flight != 0 || part != 0)) return;
                riders[slot] = new Rider { Station = source, Launch = flight, Segment = part };
            }
            if ((action == (byte)PoleEvent.Grabbed || action == (byte)PoleEvent.PlatformGrabbed)
                && (actors[seat] != actor || riders[seat].Station != station || riders[seat].Launch != launch || riders[seat].Segment != segment + 1)) return;
            if ((action == (byte)PoleEvent.Released || action == (byte)PoleEvent.Abandoned))
                for (int slot = 0; slot < actors.Length; slot++) if (actors[slot] == actor) return;
            Vector3 releasePosition = default, releaseVelocity = default;
            Quaternion releaseRotation = Quaternion.identity;
            if (action == (byte)PoleEvent.Released)
            {
                reader.ReadValueSafe(out releasePosition); reader.ReadValueSafe(out releaseRotation); reader.ReadValueSafe(out releaseVelocity);
                if (!FiniteReleaseVector(releasePosition) || !FiniteReleaseVector(releaseVelocity) || !FiniteReleaseRotation(releaseRotation)) return;
            }
            if ((PoleEvent)action != PoleEvent.Sync && revision <= runtime.State.Revision) return;
            if (!runtime.State.ReceiveCrew(revision, actors, holds, (PoleSawPhase)phase, rearmAt)) return;
            runtime.Angle = angle; runtime.Speed = speed;
            for (int slot = 0; slot < riders.Length; slot++) runtime.Riders[slot] = riders[slot];
            if (action == (byte)PoleEvent.Released)
            {
                runtime.ReleasePosition = releasePosition; runtime.ReleaseRotation = releaseRotation; runtime.ReleaseVelocity = releaseVelocity;
            }
            if (poles[index].LinkedGate != null) poles[index].LinkedGate.Present((PoleSawPhase)phase, passed == 1, Clock);
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
