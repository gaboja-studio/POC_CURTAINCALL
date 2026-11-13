using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using TimerDisplay = CurtainCall.UI.SceneIntegration.RestTimerView;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Shared escape encounter clock. The serialized type/GUID is retained; there is no deadline or countdown.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed partial class EscapeRouteTimer : MonoBehaviour
    {
        [SerializeField] EscapeCampaignContent content;
        [SerializeField] BoxCollider startArea;
        // Hide an old scene's countdown during migration, without changing the waiting-room clock.
        [SerializeField, HideInInspector] TimerDisplay display;

        public static EscapeRouteTimer Current { get; private set; }
        public bool HasStarted => synchronized && !resetBarrier.Waiting && startedAt >= 0;
        public double StartedAt => startedAt;
        public double ElapsedSeconds => HasStarted ? ElapsedAt(Clock, startedAt) : 0;
        public BoxCollider StartArea => startArea;
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        double startedAt = -1, nextSnapshot, nextSync;
        uint revision;
        int generation = -1, resetFrame = -1;
        bool synchronized;
        NetworkSessionManager session;
        readonly Dictionary<ulong, Vector3> awaitingSpawn = new();

        bool RouteReady => content != null && content.IsActive && session != null
            && session.GameState == GameSessionState.Playing && (!content.IsCampaign
                || CampaignRun.Instance != null && CampaignRun.Instance.State.Phase == CampaignPhase.Escape
                    && EscapeSceneTransfer.Current != null && EscapeSceneTransfer.Current.Ready);
        int CurrentGeneration => content != null && content.IsCampaign && CampaignRun.Instance != null
            ? CampaignRun.Instance.State.Generation : 0;

        public static double ElapsedAt(double now, double epoch)
        {
            if (!double.IsFinite(now) || !double.IsFinite(epoch) || epoch < 0) return 0;
            return Math.Max(0, now - epoch);
        }

        bool IsParticipant(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned) return false;
            if (content == null || !content.IsCampaign) return true;
            var transfer = EscapeSceneTransfer.Current;
            if (transfer == null) return false;
            foreach (var id in transfer.Participants) if (id == player.OwnerClientId) return true;
            return false;
        }

        bool CanStart(NetworkPlayer player) => IsParticipant(player) && player.State == PlayerState.Normal
            && startArea != null && startArea.enabled && EscapeEntrance.Contains(startArea, player.transform.position);

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            NetworkPlayer.Restarted += Restart;
            if (content == null) content = GetComponentInParent<EscapeCampaignContent>();
            if (display != null) display.Hide();
        }

        void Update()
        {
            Attach();
            if (display != null) display.Hide();
            if (!RouteReady || SceneCurtainTransition.IsActive)
            {
                return;
            }
            if (session.IsHost)
            {
                if (generation != CurrentGeneration) ResetClock();
                if (resetFrame != Time.frameCount)
                {
                    foreach (var player in NetworkPlayer.All)
                    {
                        if (!IsParticipant(player) || player.State != PlayerState.Normal) continue;
                        if (awaitingSpawn.TryGetValue(player.OwnerClientId, out var spawn))
                        {
                            if ((player.transform.position - spawn).sqrMagnitude > 9) continue;
                            awaitingSpawn.Remove(player.OwnerClientId);
                        }
                        if (!HasStarted && CanStart(player))
                        {
                            startedAt = Clock; revision++; Publish();
                        }
                    }
                }
                if (Time.unscaledTimeAsDouble >= nextSnapshot)
                { nextSnapshot = Time.unscaledTimeAsDouble + .5; Publish(); }
            }
            else if ((!synchronized || resetBarrier.Waiting || generation != CurrentGeneration)
                && Time.unscaledTimeAsDouble >= nextSync)
            {
                nextSync = Time.unscaledTimeAsDouble + .5;
                RequestSnapshot(resetBarrier.Waiting ? resetBarrier.Token : 0);
            }
        }

        void ResetClock()
        {
            startedAt = -1;
            generation = CurrentGeneration; synchronized = true; revision++; Publish();
            if (display != null) display.Hide();
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
                resetFrame = Time.frameCount; ResetClock();
            }
            else if (player.IsLocal)
            {
                synchronized = false; startedAt = -1;
                if (display != null) display.Hide();
                RequestSnapshot(resetBarrier.Begin());
            }
        }

        void OnDisable()
        {
            if (Current != this) return;
            NetworkPlayer.Restarted -= Restart; Detach();
            if (display != null) display.Hide();
            Current = null;
        }
    }
}
