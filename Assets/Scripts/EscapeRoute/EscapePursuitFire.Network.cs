using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuitFire
    {
        const string RequestName = "CurtainCall.Escape.PursuitFire.Request";
        const string StateName = "CurtainCall.Escape.PursuitFire.State";
        const int StateBytes = 16;
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
            writer.WriteValueSafe(revision); writer.WriteValueSafe(token); writer.WriteValueSafe(armedAt);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId
                || reader.Length - reader.Position != StateBytes) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out int token); reader.ReadValueSafe(out double epoch);
            ApplyState(incoming, token, epoch);
        }

        bool ApplyState(uint incoming, int token, double epoch)
        {
            if (incoming < revision || token < 0 || !double.IsFinite(epoch) || epoch < 0 && epoch != -1) return false;
            // Same revision is only a repeated snapshot, never a different schedule.
            if (synchronized && incoming == revision && epoch != armedAt) return false;
            if (!resetBarrier.Accept(token)) return false;
            bool changed = epoch != armedAt;
            revision = incoming; armedAt = epoch; synchronized = true;
            if (changed) HidePresentation();
            return true;
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null || session == null) return;
            if (session.IsHost)
            {
                if (!player.IsLocal && player.TryGetComponent(out PlayerMover mover))
                {
                    PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out var spawn, out _);
                    awaitingSpawn[player.OwnerClientId] = spawn;
                }
                if (resetFrame == Time.frameCount) return;
                resetFrame = Time.frameCount; armedAt = -1; revision++; HidePresentation(); Publish();
            }
            else if (player.IsLocal)
            {
                synchronized = false; armedAt = -1; HidePresentation();
                RequestSnapshot(resetBarrier.Begin());
            }
        }

        void Detach()
        {
            if (messaging != null)
            { messaging.UnregisterNamedMessageHandler(RequestName); messaging.UnregisterNamedMessageHandler(StateName); }
            messaging = null; session = null; synchronized = false; armedAt = -1; revision = 0;
            awaitingSpawn.Clear(); resetBarrier.Clear(); HidePresentation();
        }
    }
}
