using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPoleSystem
    {
        enum PoleAction : byte { Sync, Grab, Release, Swing }
        enum PoleEvent : byte { Sync, Grabbed, Released, Abandoned, Reset }
        const string RequestMessage = "CurtainCall.HumanCannon.Pole.Request";
        const string StateMessage = "CurtainCall.HumanCannon.Pole.State";
        const int RequestSize = 29;
        const int StateSize = 63;
        CustomMessagingManager messaging;

        void SendRequest(PoleAction action, int index, int station, uint launch, uint segment, float input, uint sequence)
        {
            var player = NetworkPlayer.Local;
            if (player == null || player.State != PlayerState.Normal || InputGate.IsBlocked || !Valid(index)) return;
            uint revision = states[index].State.Revision;
            if (session != null && session.IsHost)
                HandleRequest(player.OwnerClientId, action, index, revision, station, launch, segment, input, sequence);
            else if (messaging != null)
            {
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                WriteRequest(writer, action, index, revision, station, launch, segment, input, sequence);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        static void WriteRequest(FastBufferWriter writer, PoleAction action, int index, uint revision,
            int station, uint launch, uint segment, float input, uint sequence)
        {
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe(index);
            writer.WriteValueSafe(revision);
            writer.WriteValueSafe(station);
            writer.WriteValueSafe(launch);
            writer.WriteValueSafe(segment);
            writer.WriteValueSafe(input);
            writer.WriteValueSafe(sequence);
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
            HandleRequest(sender, (PoleAction)action, index, revision, station, launch, segment, input, sequence);
        }

        void Publish(int index, PoleEvent action, ulong actor)
        {
            uint segment = states[index].Segment;
            Apply(index, action, actor);
            if (messaging == null) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, index, action, actor, segment);
        }

        void SendState(ulong client, int index, PoleEvent action, ulong actor, uint? eventSegment = null)
        {
            var runtime = states[index];
            using var writer = new FastBufferWriter(StateSize, Allocator.Temp);
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
            messaging.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != StateSize) return;
            reader.ReadValueSafe(out byte action);
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
            if (index < 0 || index >= states.Length || poles[index] == null || action > (byte)PoleEvent.Reset
                || passed > 1 || !float.IsFinite(angle) || !float.IsFinite(speed)) return;
            var runtime = states[index];
            if ((PoleEvent)action != PoleEvent.Sync && revision <= runtime.State.Revision) return;
            if (!runtime.State.Receive(revision, occupant, (PoleSawPhase)phase, holdUntil, rearmAt)) return;
            runtime.Angle = angle; runtime.Speed = speed;
            runtime.Station = station; runtime.Launch = launch; runtime.Segment = segment;
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
                WriteRequest(writer, PoleAction.Sync, -1, 0, 0, 0, 0, 0f, 0);
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
    }
}
