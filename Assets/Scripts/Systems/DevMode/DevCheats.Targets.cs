using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// F9 치트를 적용할 대상 플레이어(Task-127). 제외 목록만 기억하므로 새로 들어온 플레이어는 대상에 포함된다.
    /// 다른 플레이어를 움직이거나 되살리는 판정은 호스트에 있으므로 클라이언트 화면에서는 항상 내 캐릭터만 대상이다.
    /// </summary>
    public static partial class DevCheats
    {
        static readonly HashSet<ulong> excludedTargets = new();
        static readonly List<TeleportPoint> targetPoints = new();

        static bool CanTargetOthers
        {
            get
            {
                var session = NetworkSessionManager.Instance;
                return session == null || session.IsHost;
            }
        }

        static bool IsPlayable(NetworkPlayer player) => player != null && player.IsSpawned && player.Slot != NetworkPlayer.NoSlot;

        public static bool IsTarget(NetworkPlayer player) => IsPlayable(player)
            && (CanTargetOthers ? !excludedTargets.Contains(player.OwnerClientId) : player == NetworkPlayer.Local);

        public static void SetTarget(NetworkPlayer player, bool include)
        {
            if (player == null) return;
            if (include) excludedTargets.Remove(player.OwnerClientId);
            else excludedTargets.Add(player.OwnerClientId);
        }

        public static void SelectAllTargets() => excludedTargets.Clear();

        public static void SelectOnlyLocalTarget()
        {
            excludedTargets.Clear();
            foreach (var player in NetworkPlayer.All)
                if (player != null && player != NetworkPlayer.Local) excludedTargets.Add(player.OwnerClientId);
        }

        /// <summary>지금 치트를 적용할 플레이어(자리 순). 호스트는 선택한 플레이어 전원, 클라이언트는 내 캐릭터만.</summary>
        public static void CollectTargets(List<NetworkPlayer> results)
        {
            results.Clear();
            foreach (var player in NetworkPlayer.All)
                if (IsTarget(player)) results.Add(player);
            results.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }

        static string Names(List<NetworkPlayer> players)
        {
            var names = new List<string>();
            foreach (var player in players) names.Add($"단원 {player.Slot + 1}");
            return string.Join(", ", names);
        }

        // Revival outside the normal flow must also put the player back on the space's roster first.
        static void RestoreRoster(NetworkPlayer player)
        {
            if (WaitingRoomScenes.IsWaitingRoom(CheckpointScene())) PursuitDirector.Current?.ReleaseCheckpointVictim(player);
            else PerformanceRewards.Instance?.RestoreDevCheckpointParticipant(player);
        }

        /// <summary>호스트: 선택한 플레이어를 제자리에서 온전한 몸으로 복구한다(사망·리타이어였으면 그 자리에서 되살린다).</summary>
        public static bool TryRestoreTargetsInPlace(out string message)
        {
            if (!DevMode.Enabled || !CanTargetOthers) { message = "신체 복구는 호스트만 실행할 수 있습니다."; return false; }
            var targets = new List<NetworkPlayer>();
            CollectTargets(targets);
            if (targets.Count == 0) { message = "선택한 대상 플레이어가 없습니다."; return false; }
            foreach (var player in targets)
            {
                if (IsDead(player)) RestoreRoster(player);
                player.ServerRestoreAll();
            }
            message = $"제자리 신체 복구 · {Names(targets)}";
            return true;
        }

        /// <summary>
        /// 선택한 살아 있는 플레이어를 각자 지점으로 옮긴다. <paramref name="poseFor"/>가 false를 돌려준 플레이어는 건너뛴다.
        /// 호스트는 다른 플레이어도 옮기고, 클라이언트는 내 캐릭터만 옮긴다.
        /// </summary>
        public static bool TeleportTargets(System.Func<NetworkPlayer, (bool ok, Pose pose)> poseFor, string destination, out string message)
        {
            var targets = new List<NetworkPlayer>();
            CollectTargets(targets);
            if (!DevMode.Enabled) { message = "Dev 모드를 켜 주세요."; return false; }
            if (targets.Count == 0) { message = "선택한 대상 플레이어가 없습니다."; return false; }
            var moved = new List<NetworkPlayer>();
            int dead = 0, unplaced = 0;
            string reason = "";
            foreach (var player in targets)
            {
                if (player.State != PlayerState.Normal) { dead++; continue; }
                var (ok, pose) = poseFor(player);
                if (!ok) { unplaced++; continue; }
                bool done = player.IsServer
                    ? player.ServerDevTeleport(pose.position, pose.rotation)
                    : player == NetworkPlayer.Local && Teleport(player, pose.position, pose.rotation, out reason);
                if (done) moved.Add(player);
            }
            if (moved.Count == 0)
            {
                message = reason.Length > 0 ? reason
                    : dead > 0 ? "살아 있는 대상만 이동할 수 있습니다. 먼저 되살리세요." : "대상 플레이어가 갈 수 있는 위치가 없습니다.";
                return false;
            }
            message = $"{destination} · {Names(moved)}" + (dead > 0 ? $" · 사망 {dead}명 제외" : "") + (unplaced > 0 ? $" · 위치 없음 {unplaced}명 제외" : "");
            return true;
        }

        /// <summary>F9 목록의 한 지점을 대상 플레이어 자리에 맞춘다. 체크포인트는 자리별 배치, 그 밖의 지점은 같은 이름의 지점을 다시 찾는다.</summary>
        public static (bool ok, Pose pose) PoseForTarget(TeleportPoint point, NetworkPlayer player)
        {
            var route = CurrentCheckpointRoute();
            if (point.Order >= 0 && route != null)
            {
                var authored = point.Order < route.Points.Count ? route.Points[point.Order] : null;
                return DevCheckpointRoute.Available(authored) && route.TryPose(authored, Mathf.Max(0, player.Slot), out var pose)
                    ? (true, pose) : (false, default);
            }
            if (player == NetworkPlayer.Local) return (true, new Pose(point.Position, point.Rotation));
            CollectTeleportPoints(player, targetPoints);
            foreach (var candidate in targetPoints)
                if (candidate.Label == point.Label) return (true, new Pose(candidate.Position, candidate.Rotation));
            return (true, new Pose(point.Position, point.Rotation));
        }

        /// <summary>선택한 플레이어의 균형을 중앙으로 되돌린다(줄 위에서 살아 있는 플레이어만 실제로 바뀐다).</summary>
        public static bool ResetTargetsBalance(out string message)
        {
            var targets = new List<NetworkPlayer>();
            CollectTargets(targets);
            int sent = 0;
            foreach (var player in targets)
            {
                if (player.State != PlayerState.Normal) continue;
                if (player.IsServer) { player.ServerDevResetBalance(); sent++; }
                else if (player == NetworkPlayer.Local && player.TryGetComponent(out PlayerBalance balance) && balance.IsActive && !balance.HasFallen)
                { balance.ResetBalance(); sent++; }
            }
            message = sent > 0 ? $"균형 중앙으로 · {Names(targets)} (줄 위의 생존자만 적용)" : "살아 있는 대상 플레이어가 없습니다.";
            return sent > 0;
        }
    }
}
