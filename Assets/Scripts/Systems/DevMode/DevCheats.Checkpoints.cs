using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom.Pursuit;
using CurtainCall.SpaceLink;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev
{
    public static partial class DevCheats
    {
        readonly struct Passage
        {
            public readonly int Index, PoseSlot, Restart;
            public Passage(int index, int poseSlot, int restart)
            { Index = index; PoseSlot = poseSlot; Restart = restart; }
        }

        static readonly Dictionary<NetworkPlayer, Passage> passages = new();
        static readonly List<NetworkPlayer> despawned = new();
        static Scene progressScene;
        static DevCheckpointRoute progressRoute;
        static CampaignRun progressCampaign;
        static (int generation, int day, int performance, CampaignPhase phase, string trick, int difficulty) progressStep;
        static NetworkSessionManager progressSession;
        static bool holdCheckpointWipe = true;

        public static bool HoldCheckpointWipe
        {
            get => DevMode.Enabled && holdCheckpointWipe;
            set { if (DevMode.Enabled) holdCheckpointWipe = value; }
        }

        static Scene CheckpointScene()
        {
            var escape = EscapeCampaignContent.Current;
            return escape != null && escape.IsActive ? escape.gameObject.scene : SceneManager.GetActiveScene();
        }

        internal static DevCheckpointRoute CurrentCheckpointRoute()
            => DevMode.Enabled ? DevCheckpointRoute.In(CheckpointScene()) : null;

        // The Dev panel is present even when closed. Only its host samples progress.
        internal static void TrackCheckpoints()
        {
            var route = SyncProgress();
            if (route == null) return;
            foreach (var player in passages.Keys)
                if (player == null || !player.IsSpawned) despawned.Add(player);
            foreach (var player in despawned) passages.Remove(player);
            despawned.Clear();
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.State == PlayerState.Normal) RecordPassage(route, player);
        }

        internal static void RecordBeforeDeath(NetworkPlayer player)
        {
            var route = SyncProgress();
            if (route != null) RecordPassage(route, player);
        }

        static void RecordPassage(DevCheckpointRoute route, NetworkPlayer player)
        {
            if (!IsParticipant(player) || !player.TryGetDevCheckpointFeet(out var feet)) return;
            bool waitingRoom = WaitingRoomScenes.IsWaitingRoom(route.gameObject.scene);
            // Rooms can be visited in any order. Keep the current room while its radius still overlaps another.
            if (waitingRoom && passages.TryGetValue(player, out var current) && current.Restart == player.RestartVersion && current.Index < route.Points.Count)
            {
                var existing = route.Points[current.Index];
                if (DevCheckpointRoute.Available(existing) && !existing.teleportOnly && route.TryRecognize(existing, feet, player.Slot, out _)) return;
            }
            int previous = !waitingRoom && passages.TryGetValue(player, out var known) && known.Restart == player.RestartVersion ? known.Index : -1;
            for (int i = route.Points.Count - 1; i > previous; i--)
            {
                var point = route.Points[i];
                if (!DevCheckpointRoute.Available(point) || point.teleportOnly
                    || !route.TryRecognize(point, feet, player.Slot, out int poseSlot)) continue;
                passages[player] = new Passage(i, poseSlot, player.RestartVersion);
                break;
            }
        }

        static DevCheckpointRoute SyncProgress()
        {
            var session = NetworkSessionManager.Instance;
            if (!DevMode.Enabled || session == null || !session.IsHost || session.GameState != GameSessionState.Playing)
            {
                ResetCheckpointProgress();
                return null;
            }
            var scene = CheckpointScene();
            var campaign = CampaignRun.Instance;
            var state = campaign != null ? campaign.State : default;
            var key = (state.Generation, state.Day, state.PerformanceIndex, state.Phase, state.TrickId, state.Difficulty);
            if (scene != progressScene || session != progressSession || campaign != progressCampaign || key != progressStep)
            {
                passages.Clear();
                progressScene = scene;
                progressRoute = DevCheckpointRoute.In(scene);
                progressCampaign = campaign;
                progressStep = key;
                progressSession = session;
            }
            bool allowedPhase = WaitingRoomScenes.IsWaitingRoom(scene)
                ? state.Phase is CampaignPhase.RestTime or CampaignPhase.Rehearsal or CampaignPhase.Night
                : state.Phase is CampaignPhase.Performance or CampaignPhase.Escape;
            if (SpaceLinkDirector.Instance?.IsTransitioning == true || SceneCurtainTransition.IsActive
                || state.IsRunning && !allowedPhase) return null;
            if (EscapeCampaignContent.IsTraversalActive)
            {
                if (EscapeDevelopmentSession.AwaitingEntrance) return null;
                if (EscapeCampaignContent.Current.IsCampaign && EscapeSceneTransfer.Current?.Ready != true) return null;
            }
            return progressRoute;
        }

        static bool IsParticipant(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || !player.IsServer || player.Slot == NetworkPlayer.NoSlot) return false;
            if (EscapeCampaignContent.Current?.IsCampaign != true || !EscapeCampaignContent.IsTraversalActive) return true;
            var transfer = EscapeSceneTransfer.Current;
            if (transfer == null) return false;
            foreach (ulong id in transfer.Participants)
                if (id == player.OwnerClientId) return true;
            return false;
        }

        static bool IsDead(NetworkPlayer player) => player.State is PlayerState.Fallen or PlayerState.Retired;

        static bool TryGetPassage(DevCheckpointRoute route, NetworkPlayer player, out DevCheckpointRoute.Point point, out Pose pose)
        {
            point = null;
            pose = default;
            if (!passages.TryGetValue(player, out var record) || record.Restart != player.RestartVersion
                || record.Index >= route.Points.Count) return false;
            point = route.Points[record.Index];
            return DevCheckpointRoute.Available(point) && !point.teleportOnly && route.TryPose(point, record.PoseSlot, out pose);
        }

        internal static string LastCheckpointLabel(NetworkPlayer player)
        {
            var route = SyncProgress();
            return route != null && IsParticipant(player) && TryGetPassage(route, player, out var point, out _)
                ? point.label : null;
        }

        // Dev test aid: let the host open F9 after a wipe before the normal failure transition.
        internal static bool ShouldHoldCheckpointWipe()
        {
            if (!HoldCheckpointWipe) return false;
            var route = SyncProgress();
            if (route == null) return false;
            foreach (var player in NetworkPlayer.All)
                if (IsParticipant(player) && IsDead(player) && TryGetPassage(route, player, out _, out _)) return true;
            return false;
        }

        public static bool TryRecoverTeamAtLastCheckpoint(out string message)
        {
            var route = SyncProgress();
            if (route == null) { message = "체크포인트가 있는 장소를 플레이 중인 호스트만 부활시킬 수 있습니다."; return false; }

            var targets = new List<(NetworkPlayer player, string label)>();
            int skipped = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (!IsParticipant(player) || !IsDead(player)) continue;
                if (!TryGetPassage(route, player, out var point, out var pose)) { skipped++; continue; }

                // Restore the roster before the synchronous player-state callback can settle a wipe.
                if (WaitingRoomScenes.IsWaitingRoom(progressScene))
                    PursuitDirector.Current?.ReleaseCheckpointVictim(player);
                else
                    PerformanceRewards.Instance?.RestoreDevCheckpointParticipant(player);
                player.ServerRecoverAtDevCheckpoint(pose.position + Vector3.up * .05f, pose.rotation, progressScene.name);
                targets.Add((player, point.label));
            }
            if (targets.Count == 0)
            {
                message = skipped > 0 ? "사망한 플레이어의 통과 기록이 없습니다." : "부활시킬 사망 플레이어가 없습니다.";
                return false;
            }
            var summary = new List<string>();
            foreach (var (player, label) in targets) summary.Add($"단원 {player.Slot + 1}: {label}");
            message = $"사망자 {targets.Count}명 부활 · {string.Join(" / ", summary)}"
                + (skipped > 0 ? $" · 기록 없는 {skipped}명 제외" : "");
            return true;
        }

        static void ResetCheckpointProgress()
        {
            passages.Clear();
            despawned.Clear();
            progressScene = default;
            progressRoute = null;
            progressCampaign = null;
            progressSession = null;
            progressStep = default;
        }
    }
}
