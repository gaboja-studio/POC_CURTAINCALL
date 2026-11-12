using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonTrampoline
    {
        enum TrampolineAction : byte { Sync, Grab, Drop, Bounce, GroundBounce }
        const string RequestMessage = "CurtainCall.Trampoline.Request";
        const string StateMessage = "CurtainCall.Trampoline.State";
        const string BounceMessage = "CurtainCall.Trampoline.Bounce";
        const int RequestSize = 45;
        const int StateSize = 40;
        const int BounceSize = 37;

        void SendRequest(TrampolineAction action, int station, uint launch, uint segment, Vector3 position)
        {
            var player = NetworkPlayer.Local;
            if (player == null || player.State != PlayerState.Normal) return;
            if (action != TrampolineAction.Bounce && action != TrampolineAction.GroundBounce && InputGate.IsBlocked) return;
            if (action == TrampolineAction.Grab) CurtainCall.Telemetry.Pipeline.TelemetryRuntime.PerformanceInteractionAttempt(player,this,"TRAMPOLINE");
            if (session != null && session.IsHost)
                HandleRequest(player.OwnerClientId, action, ownership.Revision, station, launch, segment, position, transform.rotation);
            else if (messaging != null)
            {
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                writer.WriteValueSafe((byte)action);
                writer.WriteValueSafe(ownership.Revision);
                writer.WriteValueSafe(station);
                writer.WriteValueSafe(launch);
                writer.WriteValueSafe(segment);
                writer.WriteValueSafe(position);
                writer.WriteValueSafe(transform.rotation);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        void HandleRequestMessage(ulong sender, FastBufferReader reader)
        {
            if (reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Quaternion rotation);
            HandleRequest(sender, (TrampolineAction)action, revision, station, launch, segment, position, rotation);
        }

        void PublishOwnership()
        {
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendOwnership(client);
        }

        void SendOwnership(ulong client)
        {
            using var writer = new FastBufferWriter(StateSize, Allocator.Temp);
            writer.WriteValueSafe(ownership.Revision);
            writer.WriteValueSafe(Carrier);
            writer.WriteValueSafe(SharedPosition);
            writer.WriteValueSafe(transform.rotation);
            messaging.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleStateMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != StateSize) return;
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out ulong carrier);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Quaternion rotation);
            if (!ownership.Receive(revision, carrier)) return;
            // 운반자가 공중에서 죽어 놓인 경우 순간이동 대신 바로 아래 지면까지 떨어뜨린다.
            if (carrier == CannonSeatState.Nobody && TryStartRemoteDrop(position)) transform.rotation = rotation;
            else
            {
                StopDrop();
                transform.SetPositionAndRotation(position, rotation);
            }
            if (carrier != CannonSeatState.Nobody)
            {
                var player = FindPlayer(carrier);
                if (TightropeHangSystem.Current?.IsHanging(player) != true)
                {
                    HumanCannonSystem.Current?.StartCarrying(carrier);
                    if (player != null && player.State == PlayerState.Normal) FollowCarrier(player);
                }
            }
            ResetPreviousPose();
        }

        void SendBounce(ulong client, bool accepted, ulong actor, int station, uint launch, uint segment, Vector3 contact)
        {
            using var writer = new FastBufferWriter(BounceSize, Allocator.Temp);
            writer.WriteValueSafe((byte)(accepted ? 1 : 0));
            writer.WriteValueSafe(actor);
            writer.WriteValueSafe(station);
            writer.WriteValueSafe(launch);
            writer.WriteValueSafe(segment);
            writer.WriteValueSafe(contact);
            writer.WriteValueSafe(CurtainCall.Settings.GameSettings.Trampoline.UpwardSpeed);
            messaging.SendNamedMessage(BounceMessage, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleBounceMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != BounceSize) return;
            reader.ReadValueSafe(out byte accepted);
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out int station);
            reader.ReadValueSafe(out uint launch);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out Vector3 contact);
            reader.ReadValueSafe(out float speed);
            var cannon = HumanCannonSystem.Current;
            if (cannon == null) return;
            bool local = NetworkPlayer.Local != null && NetworkPlayer.Local.OwnerClientId == actor;
            if (accepted == 1)
            {
                if (cannon.ApplyRebound(actor, station, launch, segment, contact, speed) && local) LocalBounceCount++;
            }
            else if (local) cannon.RejectRebound(station, launch, segment);
        }

        void HandleConnection(SessionConnectionState state)
        {
            localGrabPending = false;
            Unregister();
            if (state == SessionConnectionState.Offline)
            {
                ownership = new CannonSeatState();
                StopDrop();
                groundHistory.Clear();
                groundSequence = 0;
                transform.SetPositionAndRotation(initialPosition, initialRotation);
                ResetPreviousPose();
                LocalBounceCount = 0;
                return;
            }
            if (state != SessionConnectionState.Host && state != SessionConnectionState.Client) return;
            messaging = session.NetworkManager.CustomMessagingManager;
            if (state == SessionConnectionState.Host)
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequestMessage);
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, HandleStateMessage);
                messaging.RegisterNamedMessageHandler(BounceMessage, HandleBounceMessage);
                messaging.RegisterNamedMessageHandler(GroundBounceMessage, HandleGroundBounceMessage);
                using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
                writer.WriteValueSafe((byte)TrampolineAction.Sync);
                writer.WriteValueSafe(0u);
                writer.WriteValueSafe(-1);
                writer.WriteValueSafe(0u);
                writer.WriteValueSafe(0u);
                writer.WriteValueSafe(Vector3.zero);
                writer.WriteValueSafe(Quaternion.identity);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(BounceMessage);
            messaging.UnregisterNamedMessageHandler(GroundBounceMessage);
            messaging = null;
        }
    }
}
