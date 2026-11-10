using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeRelayGate
    {
        // 점유는 공통 봉 프로토콜 한 곳에서만 복제한다. 이 메시지는 장치가 연결하는 줄 번호만 전송한다.
        const string RequestMessage = "CurtainCall.Tightrope.RelayGate.Lanes.Request";
        const string StateMessage = "CurtainCall.Tightrope.RelayGate.Lanes.State";
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        void Connect()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening ? next.NetworkManager.CustomMessagingManager : null;
            if (session == next && messaging == manager) return;
            Disconnect(); session = next; messaging = manager;
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState);
            RequestSync();
        }
        void Disconnect()
        {
            messaging?.UnregisterNamedMessageHandler(RequestMessage);
            messaging?.UnregisterNamedMessageHandler(StateMessage);
            messaging = null; session = null;
        }
        void RequestSync()
        {
            if (messaging == null || session.IsHost) return;
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(round);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        { if (session != null && session.IsHost && reader.Length - reader.Position == 4) Publish(sender); }
        void Publish(ulong? recipient = null)
        {
            if (session == null || !session.IsHost || messaging == null) return;
            using var writer = new FastBufferWriter(12 + 4 * PoleCount, Allocator.Temp);
            writer.WriteValueSafe(round); writer.WriteValueSafe(version); writer.WriteValueSafe(PoleCount);
            foreach (var station in stations) writer.WriteValueSafe(station.Opened);
            if (recipient.HasValue) messaging.SendNamedMessage(StateMessage, recipient.Value, writer, NetworkDelivery.ReliableSequenced);
            else messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadState(ulong sender, FastBufferReader reader)
        {
            int size = 12 + 4 * PoleCount;
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || reader.Length - reader.Position != size || !reader.TryBeginRead(size)) return;
            reader.ReadValueSafe(out int generation); reader.ReadValueSafe(out int sequence); reader.ReadValueSafe(out int count);
            if (run == null || generation != run.Round || count != PoleCount || sequence < 0 || (generation == round && sequence < version)) return;
            var opened = new int[count];
            for (int i = 0; i < count; i++)
            { reader.ReadValueSafe(out opened[i]); if (opened[i] < NoLane || opened[i] >= course.LaneCount) return; }
            round = generation; version = sequence;
            for (int i = 0; i < count; i++) stations[i].Opened = opened[i];
            ApplyConnections();
        }
    }
}
