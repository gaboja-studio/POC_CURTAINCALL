using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeRun
    {
        const string PlacementMessage = "CurtainCall.Tightrope.Placement";
        readonly Dictionary<NetworkPlayer, int> placedPlayers = new();
        PlayerMover placementLock;
        bool placementControl;
        float nextPlacementSend;

        /// <summary>이번 회차의 소유자 복귀와 호스트 위치 수신을 모두 확인한 참가자만 판정한다.</summary>
        public bool IsParticipantReady(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned) return false;
            // SpaceLink는 자체 배치 확인 후 시작한다. 시작 전 연습에도 적용하지 않는다.
            if (externallyManaged || State != TightropeRunState.Running) return true;
            if (session != null && !session.IsHost && !player.IsLocal) return true;
            return placedPlayers.TryGetValue(player, out int version) && version == player.RestartVersion;
        }

        bool IsStartPosition(Vector3 position) => float.IsFinite(position.x) && float.IsFinite(position.y)
            && float.IsFinite(position.z) && course.GetDistance(position) < -.1f
            && course.GetDistance(position) >= -course.Shape.StartPlatformLength - 1f
            && Mathf.Abs(position.y - course.transform.position.y) <= course.Shape.RopeHeightTolerance
            && Mathf.Abs(course.GetSide(position)) <= (course.LaneCount - 1) * course.LaneSpacing * .5f + course.Shape.SideMargin;

        void UpdatePlacement()
        {
            if (externallyManaged || State != TightropeRunState.Running || session == null)
            { ReleasePlacementLock(); return; }
            var local = NetworkPlayer.Local;
            if (local == null || !local.IsSpawned || local.State != PlayerState.Normal
                || (BridgeManagesPerformance && localPreparedRound != Round)) return;
            if (IsParticipantReady(local)) { ReleasePlacementLock(); return; }
            // 위치 메시지와 RunState/복구 메시지는 서로 다른 순서로 올 수 있다.
            // 출발점에 온 소유자는 확인 응답까지 이동하지 않고 같은 회차를 재전송한다.
            if (!IsStartPosition(local.transform.position)) return;
            if (session.IsHost)
            {
                placedPlayers[local] = local.RestartVersion;
                seenAtStart.Add(local);
                return;
            }
            if (placementLock == null && local.TryGetComponent(out PlayerMover mover))
            { placementLock = mover; placementControl = mover.ControlEnabled; mover.SetControlEnabled(false); }
            if (Time.unscaledTime < nextPlacementSend || Messaging == null) return;
            nextPlacementSend = Time.unscaledTime + .2f;
            SendPlacement(NetworkManager.ServerClientId, false, Round, local.RestartVersion, local.transform.position);
        }

        void SendPlacement(ulong recipient, bool accepted, int round, int restart, Vector3 position)
        {
            using var writer = new FastBufferWriter(21, Allocator.Temp);
            writer.WriteValueSafe(accepted); writer.WriteValueSafe(round);
            writer.WriteValueSafe(restart); writer.WriteValueSafe(position);
            Messaging?.SendNamedMessage(PlacementMessage, recipient, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandlePlacement(ulong sender, FastBufferReader reader)
        {
            if (session == null || externallyManaged || State != TightropeRunState.Running
                || reader.Length - reader.Position != 21 || !reader.TryBeginRead(21)) return;
            reader.ReadValueSafe(out bool accepted); reader.ReadValueSafe(out int round);
            reader.ReadValueSafe(out int restart); reader.ReadValueSafe(out Vector3 position);
            if (round != Round || !IsStartPosition(position)) return;
            if (session.IsHost)
            {
                if (accepted) return;
                NetworkPlayer target = null;
                foreach (var candidate in NetworkPlayer.All)
                    if (candidate.OwnerClientId == sender) { target = candidate; break; }
                if (target == null || !target.IsSpawned || target.State != PlayerState.Normal || target.RestartVersion != restart) return;
                // 재전송에는 계속 응답하되, 최초 확인은 호스트의 실제 위치도 출발점일 때만 한다.
                if (!IsParticipantReady(target) && (!IsStartPosition(target.transform.position)
                    || Vector3.Distance(target.transform.position, position) > .75f)) return;
                placedPlayers[target] = restart;
                seenAtStart.Add(target);
                SendPlacement(sender, true, round, restart, position);
            }
            else
            {
                var local = NetworkPlayer.Local;
                if (!accepted || sender != NetworkManager.ServerClientId || local == null || local.RestartVersion != restart) return;
                placedPlayers[local] = restart;
                ReleasePlacementLock();
            }
        }

        void ReleasePlacementLock()
        {
            if (placementLock != null && NetworkPlayer.Local != null && NetworkPlayer.Local.State == PlayerState.Normal)
                placementLock.SetControlEnabled(placementControl);
            placementLock = null;
        }

        void ResetPlacement()
        {
            ReleasePlacementLock();
            placedPlayers.Clear(); nextPlacementSend = 0f;
        }
    }
}
