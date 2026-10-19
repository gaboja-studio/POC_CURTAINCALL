using CurtainCall.HumanCannon;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeRelayGate
    {
        enum Operation : byte { Grab, Release, Sync }
        const string RequestMessage = "CurtainCall.Tightrope.RelayGate.Request";
        const string StateMessage = "CurtainCall.Tightrope.RelayGate.State";
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                ? next.NetworkManager.CustomMessagingManager : null;
            if (session == next && messaging == manager) return;
            RestoreBinding(false); Disconnect(); ResetState(); round = -1; session = next; messaging = manager;
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState);
            Send(Operation.Sync);
        }
        void Disconnect()
        {
            messaging?.UnregisterNamedMessageHandler(RequestMessage);
            messaging?.UnregisterNamedMessageHandler(StateMessage);
            messaging = null; session = null;
        }
        void Send(Operation operation, int index = 0)
        {
            if (session == null || messaging == null || index < 0 || index >= PoleCount) return;
            uint revision = stations[index].State.Revision;
            if (session.IsHost) { ServerHandle(session.NetworkManager.LocalClientId, operation, index, revision); return; }
            using var writer = new FastBufferWriter(10, Allocator.Temp);
            writer.WriteValueSafe((byte)operation); writer.WriteValueSafe(round);
            writer.WriteValueSafe((byte)index); writer.WriteValueSafe(revision);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length != 10 || !reader.TryBeginRead(10)) return;
            reader.ReadValueSafe(out byte operation); reader.ReadValueSafe(out int generation);
            reader.ReadValueSafe(out byte index); reader.ReadValueSafe(out uint revision);
            if (operation > (byte)Operation.Sync || index >= PoleCount
                || ((Operation)operation != Operation.Sync && (generation != round || run == null || generation != run.Round))) return;
            ServerHandle(sender, (Operation)operation, index, revision);
        }
        void Publish(ulong? recipient = null)
        {
            if (session == null || !session.IsHost || messaging == null) return;
            using var writer = new FastBufferWriter(125, Allocator.Temp);
            writer.WriteValueSafe(round); writer.WriteValueSafe(version); writer.WriteValueSafe((byte)PoleCount);
            foreach (var station in stations)
            {
                var state = station.State;
                writer.WriteValueSafe(state.Revision); writer.WriteValueSafe(state.Occupant);
                writer.WriteValueSafe((byte)state.Phase); writer.WriteValueSafe(state.HoldUntil); writer.WriteValueSafe(state.RearmAt);
            }
            if (recipient.HasValue) messaging.SendNamedMessage(StateMessage, recipient.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId
                || reader.Length != 125 || !reader.TryBeginRead(125)) return;
            reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out int sequence); reader.ReadValueSafe(out byte count);
            run = TightropeRun.Current;
            if (run == null || generation != run.Round || sequence < 0 || count != PoleCount
                || (generation == round && sequence < version)) return;
            var received = new PoleGateState[PoleCount];
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out uint revision); reader.ReadValueSafe(out ulong owner); reader.ReadValueSafe(out byte phase);
                reader.ReadValueSafe(out double until); reader.ReadValueSafe(out double rearm);
                var state = new PoleGateState();
                if (until > Clock + 125d || rearm > Clock + 10d || !state.Receive(revision, owner, (PoleSawPhase)phase, until, rearm)) return;
                for (int j = 0; j < i; j++) if (owner != Nobody && received[j].Occupant == owner) return;
                received[i] = state;
            }
            if (generation != round) { RestoreBinding(false); ResetState(); round = generation; }
            for (int i = 0; i < count; i++) stations[i].State = received[i];
            version = sequence; pendingUntil = 0d;
            ConfirmLocalAbort();
            ApplyConnections(); UpdateBinding();
        }
    }
}
