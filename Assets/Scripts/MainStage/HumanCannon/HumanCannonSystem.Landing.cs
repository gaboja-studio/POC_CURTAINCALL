using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        const string LandingMessage = "CurtainCall.HumanCannon.Landing";
        const int LandingSize = 21; // actor + station + launch + segment + safe
        /// <summary>M2: 비행 중 일반 바닥·코스 밖으로 떨어지면 잃는 팔다리 수. 잃은 팔다리가 4개가 되면 사망.</summary>
        public const int FallCuts = 2;
        public int LocalSafeLandingCount { get; private set; }

        /// <summary>내 캐릭터가 마지막으로 탄(발사한) 대포 번호. 아직 없으면 -1. 코스 밖으로 떨어지면 이 대포 앞으로 돌아온다.</summary>
        public int LocalLastStation { get; private set; } = -1;

        /// <summary>호스트: 추락으로 다쳤다(접속 번호). 관객 야유 등 관찰용이며 같은 추락은 한 번만 알린다.</summary>
        public static event Action<ulong> ServerFell;

        /// <summary>
        /// 호스트: 추락 부상(M2 부상 규칙). 즉사가 아니라 공통 순서로 <see cref="FallCuts"/>곳을 잃고, 4곳이 되면 공용 규칙이 사망으로 확정한다.
        /// 불꽃 같은 즉사 장애물은 이것을 쓰지 않는다.
        /// </summary>
        public static void ServerFallInjury(NetworkPlayer player)
        {
            if (player == null || !player.IsServer || player.State != PlayerState.Normal) return;
            // 추락 야유를 먼저 알리고 절단한다. 절단으로 사망하면 그 뒤 호응도는 고정된다.
            if (ServerFell != null)
                foreach (Action<ulong> observer in ServerFell.GetInvocationList())
                    try { observer(player.OwnerClientId); }
                    catch (Exception error) { Debug.LogException(error); }
            player.ServerCut(FallCuts);
        }

        /// <summary>호스트가 현재 비행의 착지를 판정한 뒤 한 번 알린다. Position은 판정 시 호스트의 플레이어 좌표다.</summary>
        public event Action<ulong, int, uint, uint, Vector3, bool> LandingConfirmed;

        void NotifyLanding(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            if (LandingConfirmed == null) return;
            foreach (Action<ulong, int, uint, uint, Vector3, bool> observer in LandingConfirmed.GetInvocationList())
                try { observer(actor, station, revision, segment, position, safe); }
                catch (Exception error) { Debug.LogException(error, this); }
        }

        void HandleLanding(NetworkPlayer player, int index, uint revision, uint segment, Vector3 position)
        {
            // 현재 좌석 소유권과 무관하게 해당 비행만 한 번 종료한다. R/이전 반동의 늦은 요청은 무시한다.
            if (!flying.Matches(player.OwnerClientId, index, revision, segment)) return;
            if (HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0) return;
            bool safe = (player.transform.position - position).sqrMagnitude <= 25f
                && HumanCannonLandingZone.ContainsLanding(position);
            ApplyLanding(player.OwnerClientId, index, revision, segment, safe);
            NotifyLanding(player.OwnerClientId, index, revision, segment, player.transform.position, safe);
            // 부딪힌 바닥에서 부상 상태로 계속한다(2026-10-28 사용자 결정). 코스 아래 낭떠러지는 경로 시스템이 같은 추락으로 한 번만 처리한다.
            if (!safe && !HumanCannonRouteSystem.IsBelowCourse(player.transform.position)) ServerFallInjury(player);
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
            LocalLastStation = index;
            if (safe) LocalSafeLandingCount++;
            if (pilot != null) pilot.CompleteLanding(index, revision, segment);
        }
    }
}
