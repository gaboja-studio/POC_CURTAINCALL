using CurtainCall.Network.PlayerSync;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonTrampoline
    {
        const string GroundBounceMessage = "CurtainCall.Trampoline.GroundBounce";
        const int GroundBounceSize = 25; // accepted + sequence + epoch + contact + speed
        readonly GroundBounceHistory groundHistory = new();
        uint groundSequence;
        internal uint Revision => ownership.Revision;
        internal uint NextGroundSequence() => ++groundSequence;

        internal void RequestGroundBounce(uint sequence, Vector3 contact) =>
            SendRequest(TrampolineAction.GroundBounce, -1, sequence, 0, contact);

        void HandleGroundBounce(NetworkPlayer player, uint epoch, uint sequence, Vector3 contact)
        {
            var cannon = HumanCannonSystem.Current;
            bool nearSurface = NearSurface(player, contact);
            bool accepted = !BounceBlocked(contact) && cannon != null && Carrier != player.OwnerClientId && nearSurface
                && !cannon.IsFlying(player.OwnerClientId) && !cannon.IsSeated(player.OwnerClientId)
                && groundHistory.TryAccept(player.OwnerClientId, sequence, epoch, ownership.Revision)
                && cannon.BeginGroundBounce(player.OwnerClientId, sequence, epoch, contact, upwardSpeed, groundBounceAirControlSpeed);
            if (accepted) ServerBounced?.Invoke(player.OwnerClientId);
            if (player.IsLocal)
            {
                if (accepted) LocalBounceCount++;
                else cannon?.RejectGroundBounce(sequence, epoch);
                return;
            }
            // 호스트는 비행 식별자를, 소유자는 이동을 관리한다. 다른 화면의 위치는 NetworkTransform이 공유한다.
            using var writer = new FastBufferWriter(GroundBounceSize, Allocator.Temp);
            writer.WriteValueSafe((byte)(accepted ? 1 : 0));
            writer.WriteValueSafe(sequence);
            writer.WriteValueSafe(epoch);
            writer.WriteValueSafe(contact);
            writer.WriteValueSafe(upwardSpeed);
            messaging.SendNamedMessage(GroundBounceMessage, player.OwnerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleGroundBounceMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != GroundBounceSize) return;
            reader.ReadValueSafe(out byte accepted);
            reader.ReadValueSafe(out uint sequence);
            reader.ReadValueSafe(out uint epoch);
            reader.ReadValueSafe(out Vector3 contact);
            reader.ReadValueSafe(out float speed);
            var cannon = HumanCannonSystem.Current;
            var player = NetworkPlayer.Local;
            if (cannon == null || player == null) return;
            if (accepted == 1)
            {
                if (cannon.BeginGroundBounce(player.OwnerClientId, sequence, epoch, contact, speed, groundBounceAirControlSpeed))
                    LocalBounceCount++;
            }
            else cannon.RejectGroundBounce(sequence, epoch);
        }
    }
}
