using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonHazardCourse
    {
        const string RequestName = "CurtainCall.HumanCannon.Hazards.Request";
        const string StateName = "CurtainCall.HumanCannon.Hazards.State";
        const int StateBytes = 36;
        readonly RouteResetBarrier resetBarrier = new();
        CustomMessagingManager messaging;

        void Connection(SessionConnectionState state)
        {
            Unregister(); ClearContacts(); restartPositions.Clear(); synchronized = false;
            if (state == SessionConnectionState.Offline) { runStart = runEnd = -1; revision = 0; resetBarrier.Clear(); return; }
            if (state != SessionConnectionState.Host && state != SessionConnectionState.Client) return;
            messaging = session.NetworkManager.CustomMessagingManager;
            if (session.IsHost)
            {
                epoch = Clock; runStart = runEnd = -1; synchronized = true;
                messaging.RegisterNamedMessageHandler(RequestName, ReceiveRequest);
            }
            else
            {
                messaging.RegisterNamedMessageHandler(StateName, ReceiveState);
                RequestSnapshot(resetBarrier.Waiting ? resetBarrier.Token : 0);
            }
        }

        void RequestSnapshot(int token)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(token);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != 4) return;
            reader.ReadValueSafe(out int token);
            if (token >= 0) SendState(sender, token);
        }

        void Publish()
        {
            if (session == null || !session.IsHost || messaging == null) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, 0);
        }

        void SendState(ulong client, int token)
        {
            using var writer = new FastBufferWriter(StateBytes, Allocator.Temp);
            writer.WriteValueSafe(revision); writer.WriteValueSafe(token); writer.WriteValueSafe(difficulty);
            writer.WriteValueSafe(epoch); writer.WriteValueSafe(runStart); writer.WriteValueSafe(runEnd);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != StateBytes) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out int token); reader.ReadValueSafe(out int level);
            reader.ReadValueSafe(out double start); reader.ReadValueSafe(out double run); reader.ReadValueSafe(out double end);
            if (incoming < revision || !CannonDifficulty.Valid(level) || !double.IsFinite(start) || start < 0
                || !double.IsFinite(run) || run < -1 || !double.IsFinite(end) || end < -1 || !resetBarrier.Accept(token)) return;
            if (epoch != start || difficulty != level) ClearContacts();
            revision = incoming; difficulty = level; epoch = start; runStart = run; runEnd = end; synchronized = true;
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null) return;
            if (!player.IsLocal)
            {
                if (session != null && session.IsHost && player.TryGetComponent(out CurtainCall.Player.PlayerMover mover))
                {
                    PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out var spawn, out _);
                    restartPositions[player.OwnerClientId] = spawn;
                    previousFeet.Remove(player.OwnerClientId);
                    touching.RemoveWhere(contact => contact.Item2 == player.OwnerClientId);
                }
                return;
            }
            ClearContacts(); runStart = runEnd = -1;
            if (session != null && session.IsHost) { epoch = Clock; revision++; Publish(); }
            else { synchronized = false; RequestSnapshot(resetBarrier.Begin()); }
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestName); messaging.UnregisterNamedMessageHandler(StateName);
            messaging = null;
        }
    }
}
