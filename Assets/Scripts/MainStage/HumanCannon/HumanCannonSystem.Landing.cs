using CurtainCall.Network.PlayerSync;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        const string LandingMessage = "CurtainCall.HumanCannon.Landing";
        const int LandingSize = 21; // actor + station + launch + segment + safe
        public int LocalSafeLandingCount { get; private set; }

        void HandleLanding(NetworkPlayer player, int index, uint revision, uint segment, Vector3 position)
        {
            // 현재 좌석 소유권과 무관하게 해당 비행만 한 번 종료한다. R/이전 반동의 늦은 요청은 무시한다.
            if (!flying.Matches(player.OwnerClientId, index, revision, segment)) return;
            bool safe = (player.transform.position - position).sqrMagnitude <= 25f
                && HumanCannonLandingZone.ContainsLanding(position);
            ApplyLanding(player.OwnerClientId, index, revision, segment, safe);
            if (!safe) player.ServerKill();
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
            {
                if (client == NetworkManager.ServerClientId) continue;
                using var writer = new FastBufferWriter(LandingSize, Allocator.Temp);
                writer.WriteValueSafe(player.OwnerClientId);
                writer.WriteValueSafe(index);
                writer.WriteValueSafe(revision);
                writer.WriteValueSafe(segment);
                writer.WriteValueSafe((byte)(safe ? 1 : 0));
                messaging.SendNamedMessage(LandingMessage, client, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        void HandleLandingMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != LandingSize) return;
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out byte safe);
            if (safe > 1) return;
            ApplyLanding(actor, index, revision, segment, safe == 1);
        }

        void ApplyLanding(ulong actor, int index, uint revision, uint segment, bool safe)
        {
            if (!flying.TryComplete(actor, index, revision, segment)) return;
            if (boundPlayer == null || actor != boundPlayer.OwnerClientId) return;
            if (safe) LocalSafeLandingCount++;
            if (pilot != null) pilot.CompleteLanding(index, revision, segment);
        }
    }
}
