using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonFinalSaws
    {
        const string RequestName = "CurtainCall.HumanCannon.FinalSaws.Request";
        const string StateName = "CurtainCall.HumanCannon.FinalSaws.State";
        const int StateBytes = 44;
        CustomMessagingManager messaging;

        void Connection(SessionConnectionState connection)
        {
            Unregister();
            if (connection == SessionConnectionState.Offline)
            {
                ResetSaws(); revision = 0; resetBarrier.Clear(); return;
            }
            if (connection != SessionConnectionState.Host && connection != SessionConnectionState.Client) return;
            messaging = session.NetworkManager.CustomMessagingManager;
            if (session.IsHost) messaging.RegisterNamedMessageHandler(RequestName, ReceiveRequest);
            else
            {
                messaging.RegisterNamedMessageHandler(StateName, ReceiveState);
                RequestSnapshot(resetBarrier.Waiting ? resetBarrier.Token : 0);
            }
        }

        void RequestSnapshot(int resetToken)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(resetToken);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != 4) return;
            reader.ReadValueSafe(out int resetToken);
            if (resetToken >= 0) SendState(sender, resetToken);
        }

        void Publish()
        {
            if (messaging == null || session == null || !session.IsHost) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, 0);
        }

        void SendState(ulong client, int resetToken)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(StateBytes, Allocator.Temp);
            writer.WriteValueSafe(revision);
            writer.WriteValueSafe(resetToken);
            writer.WriteValueSafe(startedAt);
            writer.WriteValueSafe(warningSeconds);
            writer.WriteValueSafe(travelSeconds);
            writer.WriteValueSafe(horizontalSpeed);
            writer.WriteValueSafe(horizontalHalfWidth);
            writer.WriteValueSafe(horizontalRadius);
            writer.WriteValueSafe(verticalRadius);
            writer.WriteValueSafe(halfThickness);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != StateBytes) return;
            reader.ReadValueSafe(out uint incoming);
            reader.ReadValueSafe(out int resetToken);
            reader.ReadValueSafe(out double start);
            reader.ReadValueSafe(out float warning);
            reader.ReadValueSafe(out float travel);
            reader.ReadValueSafe(out float speed);
            reader.ReadValueSafe(out float width);
            reader.ReadValueSafe(out float horizontal);
            reader.ReadValueSafe(out float vertical);
            reader.ReadValueSafe(out float thickness);
            if (incoming < revision || resetToken < 0 || !double.IsFinite(start) || start < 0 && start != -1
                || !Positive(warning) || !Positive(travel) || !Positive(speed) || !Positive(width)
                || !Positive(horizontal) || !Positive(vertical) || !Positive(thickness) || !resetBarrier.Accept(resetToken)) return;
            if (startedAt != start) { touching.Clear(); previousFeet.Clear(); previousClock = -1; }
            revision = incoming; startedAt = start;
            warningSeconds = warning; travelSeconds = travel; horizontalSpeed = speed; horizontalHalfWidth = width;
            horizontalRadius = horizontal; verticalRadius = vertical; halfThickness = thickness;
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null || !player.IsLocal) return;
            ResetSaws();
            if (session != null && session.IsHost)
            {
                foreach (var other in NetworkPlayer.All)
                    if (other != null && other.IsSpawned)
                        RememberRemoteEntranceAtRestart(other.OwnerClientId, other.IsLocal, other.transform.position);
                revision++;
                Publish();
            }
            else RequestSnapshot(resetBarrier.Begin());
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestName);
            messaging.UnregisterNamedMessageHandler(StateName);
            messaging = null;
        }

        static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }
}
