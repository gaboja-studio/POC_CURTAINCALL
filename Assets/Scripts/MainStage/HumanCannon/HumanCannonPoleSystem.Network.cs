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
        const int StateSize = 63;
        const int ReleaseStateSize = StateSize + 40; // 승인 시점 위치 + 회전 + 최종 속도
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
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(revision);
            writer.WriteValueSafe(station);
            writer.WriteValueSafe(launch);
            writer.WriteValueSafe(segment);
            writer.WriteValueSafe(input);
            writer.WriteValueSafe(sequence);
            writer.WriteValueSafe(observedElapsed);
        }

        void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out float input);
            reader.ReadValueSafe(out uint sequence);
            reader.ReadValueSafe(out double observedElapsed);
            if (action > (byte)PoleAction.PlatformGrab) return;
            HandleRequest(sender, (PoleAction)action, index, revision, station, launch, segment, input, sequence, observedElapsed);
        }

        void Publish(int index, PoleEvent action, ulong actor)
        {
            if (action == PoleEvent.Released)
            {
                // 이동 레일에서 수신 시각으로 재계산하면 클라이언트마다 다른 곳에서 도약한다.
                var pole = poles[index]; var runtime = states[index];
                pole.RefreshMovingPose();
                runtime.ReleasePosition = HangingPosition(index);
                runtime.ReleaseRotation = pole.Grip.rotation;
                runtime.ReleaseVelocity = CannonAirControl.LimitPoleHorizontalSpeed(
                    pole.ReleaseVelocity(runtime.Angle, runtime.Speed), GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit);
            }
            uint segment = states[index].Segment;
            Apply(index, action, actor);
            if (messaging == null) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, index, action, actor, segment);
        }

        void SendState(ulong client, int index, PoleEvent action, ulong actor, uint? eventSegment = null)
        {
            var runtime = states[index];
            using var writer = new FastBufferWriter(action == PoleEvent.Released ? ReleaseStateSize : StateSize, Allocator.Temp);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(runtime.State.Revision);
            writer.WriteValueSafe(runtime.State.Occupant);
            writer.WriteValueSafe((byte)runtime.State.Phase);
            writer.WriteValueSafe(runtime.State.HoldUntil);
            writer.WriteValueSafe(runtime.State.RearmAt);
            writer.WriteValueSafe(runtime.Angle);
            writer.WriteValueSafe(runtime.Speed);
            writer.WriteValueSafe(actor);
            writer.WriteValueSafe(runtime.Station);
            writer.WriteValueSafe(runtime.Launch);
            writer.WriteValueSafe(eventSegment ?? runtime.Segment);
            writer.WriteValueSafe((byte)(poles[index].LinkedGate != null && poles[index].LinkedGate.CarrierPassed ? 1 : 0));
            if (action == PoleEvent.Released)
            {
                writer.WriteValueSafe(runtime.ReleasePosition);
                writer.WriteValueSafe(runtime.ReleaseRotation);
                writer.WriteValueSafe(runtime.ReleaseVelocity);
            }
            messaging.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            int bytes = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || (bytes != StateSize && bytes != ReleaseStateSize)) return;
            reader.ReadValueSafe(out byte action);
            if (bytes != (action == (byte)PoleEvent.Released ? ReleaseStateSize : StateSize)) return;
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out ulong occupant);
            reader.ReadValueSafe(out byte phase);
            reader.ReadValueSafe(out double holdUntil);
            reader.ReadValueSafe(out double rearmAt);
            reader.ReadValueSafe(out float angle);
            reader.ReadValueSafe(out float speed);
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out byte passed);
            Vector3 releasePosition = default, releaseVelocity = default;
            Quaternion releaseRotation = Quaternion.identity;
            if (action == (byte)PoleEvent.Released)
            {
                reader.ReadValueSafe(out releasePosition);
                reader.ReadValueSafe(out releaseRotation);
                reader.ReadValueSafe(out releaseVelocity);
                if (!FiniteReleaseVector(releasePosition) || !FiniteReleaseVector(releaseVelocity)
                    || !FiniteReleaseRotation(releaseRotation)) return;
            }
            if (index < 0 || index >= states.Length || poles[index] == null || action > (byte)PoleEvent.PlatformGrabbed
                || passed > 1 || !float.IsFinite(angle) || !float.IsFinite(speed)) return;
            var runtime = states[index];
            if ((PoleEvent)action != PoleEvent.Sync && revision <= runtime.State.Revision) return;
            if (!runtime.State.Receive(revision, occupant, (PoleSawPhase)phase, holdUntil, rearmAt)) return;
            runtime.Angle = angle; runtime.Speed = speed;
            runtime.Station = station; runtime.Launch = launch; runtime.Segment = segment;
            if (action == (byte)PoleEvent.Released)
            {
                runtime.ReleasePosition = releasePosition; runtime.ReleaseRotation = releaseRotation;
                runtime.ReleaseVelocity = releaseVelocity;
            }
            if (poles[index].LinkedGate != null) poles[index].LinkedGate.Present((PoleSawPhase)phase, passed == 1, Clock);
            Apply(index, (PoleEvent)action, actor);
        }

        void Connection(SessionConnectionState connection)
        {
            Unregister();
            if (connection == SessionConnectionState.Offline)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    states[i] = new Runtime();
                    if (poles[i] != null && poles[i].LinkedGate != null) poles[i].LinkedGate.ResetGate();
                    HumanCannonSystem.Current?.CancelLocalPole(i);
                }
                inputSequence = 0;
                lastInputDirection = 0;
                nextInput = nextSnapshot = 0;
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
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging = null;
        }

        static bool FiniteReleaseVector(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static bool FiniteReleaseRotation(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y)
            && float.IsFinite(q.z) && float.IsFinite(q.w) && Quaternion.Dot(q, q) > .001f;
    }
}
