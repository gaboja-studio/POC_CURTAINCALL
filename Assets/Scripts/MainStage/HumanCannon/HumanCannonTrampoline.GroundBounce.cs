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
        internal uint Revision => ownership.Revision;
        internal bool CanRequestGroundBounce => isActiveAndEnabled && session != null && messaging != null
            && (session.IsHost || hasOwnershipSnapshot);

        internal void RequestGroundBounce(uint sequence, uint epoch, Vector3 contact) =>
            SendRequest(TrampolineAction.GroundBounce, -1, sequence, 0, contact, epoch);

        void HandleGroundBounce(NetworkPlayer player, uint epoch, uint sequence, Vector3 contact)
        {
            var cannon = HumanCannonSystem.Current;
            ulong actor = player.OwnerClientId;
            Vector3 storedContact = default;
            bool replay = cannon != null && cannon.MatchesFlight(actor, -1, sequence, 0)
                && groundHistory.TryReplay(actor, sequence, epoch, out storedContact);
            if (replay) contact = storedContact;
            bool available = BounceReady && !GrabPending(player) && !BounceBlocked(contact) && cannon != null && Carrier != actor;
            // Reliable input can arrive before the owner transform. Leave this attempt pending
            // so the owner's retry can be validated once its near-surface pose is visible.
            if (!replay && available && !player.IsLocal && epoch == ownership.Revision
                && ContactWithinSurface(contact) && !NearSurface(player, contact)) return;
            bool accepted = replay || available && NearSurface(player, contact)
                && !cannon.IsFlying(player.OwnerClientId) && !cannon.IsSeated(player.OwnerClientId)
                && groundHistory.TryAccept(player.OwnerClientId, sequence, epoch, ownership.Revision)
                && cannon.BeginGroundBounce(player.OwnerClientId, sequence, epoch, contact, CurtainCall.Settings.GameSettings.Trampoline.UpwardSpeed, CurtainCall.Settings.GameSettings.Trampoline.AirControlSpeed);
            if (accepted && !replay)
            {
                groundHistory.RecordAccepted(actor, contact);
                NotifyBounced(actor);
                CurtainCall.WaitingRoom.Pursuit.PursuitStimuli.Emit(CurtainCall.WaitingRoom.Pursuit.PursuitNoiseKind.TrampolineBounce, contact, transform);
            }
            if (player.IsLocal)
            {
                if (accepted) { if (!replay) LocalBounceCount++; }
                else cannon?.RejectGroundBounce(sequence, epoch);
                return;
            }
            // 호스트는 비행 식별자를, 소유자는 이동을 관리한다. 다른 화면의 위치는 NetworkTransform이 공유한다.
            using var writer = new FastBufferWriter(GroundBounceSize, Allocator.Temp);
            writer.WriteValueSafe((byte)(accepted ? 1 : 0));
            writer.WriteValueSafe(sequence);
            writer.WriteValueSafe(epoch);
            writer.WriteValueSafe(contact);
            writer.WriteValueSafe(CurtainCall.Settings.GameSettings.Trampoline.UpwardSpeed);
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
                if (cannon.BeginGroundBounce(player.OwnerClientId, sequence, epoch, contact, speed, CurtainCall.Settings.GameSettings.Trampoline.AirControlSpeed))
                    LocalBounceCount++;
            }
            else cannon.RejectGroundBounce(sequence, epoch);
        }
    }
}
