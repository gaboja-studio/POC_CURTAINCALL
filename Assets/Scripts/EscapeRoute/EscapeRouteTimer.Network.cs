using CurtainCall.HumanCannon;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeRouteTimer
    {
        const string RequestName = "CurtainCall.Escape.EncounterClock.Request.v2";
        const string StateName = "CurtainCall.Escape.EncounterClock.State.v2";
        const int StateBytes = 20;
        readonly RouteResetBarrier resetBarrier = new();
        CustomMessagingManager messaging;

        void Attach()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                && (next.ConnectionState == SessionConnectionState.Host || next.ConnectionState == SessionConnectionState.Client)
                ? next.NetworkManager.CustomMessagingManager : null;
            if (manager == messaging) return;
            Detach(); session = next; messaging = manager;
            if (manager == null) return;
            if (session.IsHost)
            { synchronized = true; manager.RegisterNamedMessageHandler(RequestName, ReadRequest); }
            else
            { manager.RegisterNamedMessageHandler(StateName, ReadState); RequestSnapshot(0); }
        }

        void RequestSnapshot(int token)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(token);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != 4
                || !session.NetworkManager.ConnectedClients.ContainsKey(sender)) return;
            reader.ReadValueSafe(out int token);
            if (token >= 0) SendState(sender, token);
        }

        void Publish()
        {
            if (session == null || !session.IsHost || messaging == null) return;
            foreach (var client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, 0);
        }

        void SendState(ulong client, int token)
        {
            using var writer = new FastBufferWriter(StateBytes, Allocator.Temp);
            writer.WriteValueSafe(revision); writer.WriteValueSafe(token); writer.WriteValueSafe(generation);
            writer.WriteValueSafe(startedAt);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId
                || reader.Length - reader.Position != StateBytes) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out int token); reader.ReadValueSafe(out int round);
            reader.ReadValueSafe(out double epoch);
            ApplyState(incoming, token, round, epoch);
        }

        bool ApplyState(uint incoming, int token, int round, double epoch)
        {
            if (incoming < revision || token < 0 || round < 0 || !double.IsFinite(epoch) || epoch < 0 && epoch != -1) return false;
            if (synchronized && incoming == revision && (epoch != startedAt || round != generation)) return false;
            if (!resetBarrier.Accept(token)) return false;
            revision = incoming; generation = round; startedAt = epoch; synchronized = true;
            return true;
        }

        void Detach()
        {
            if (messaging != null)
            { messaging.UnregisterNamedMessageHandler(RequestName); messaging.UnregisterNamedMessageHandler(StateName); }
            messaging = null; session = null; synchronized = false; startedAt = -1; generation = -1; revision = 0;
            awaitingSpawn.Clear(); resetBarrier.Clear();
        }
    }
}
