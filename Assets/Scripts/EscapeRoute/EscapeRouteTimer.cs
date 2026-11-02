using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.UI.RestTimerV28;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using TimerDisplay = CurtainCall.UI.SceneIntegration.RestTimerView;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A host-owned escape deadline, started once when anyone enters the barred observation hall.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed partial class EscapeRouteTimer : MonoBehaviour
    {
        [SerializeField] EscapeCampaignContent content;
        [SerializeField] BoxCollider startArea;
        [SerializeField, Min(1)] float durationSeconds = 480;
        [SerializeField] TimerDisplay display;

        public static EscapeRouteTimer Current { get; private set; }
        public bool HasStarted => synchronized && !resetBarrier.Waiting && startedAt >= 0;
        public double StartedAt => startedAt;
        public double DurationSeconds => sharedDuration;
        public double RemainingSeconds => RemainingAt(Clock, startedAt, sharedDuration);
        public bool HasExpired => HasStarted && RemainingSeconds <= 0;
        public BoxCollider StartArea => startArea;
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        double startedAt = -1, sharedDuration = 480, nextSnapshot, nextSync;
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

        public static double RemainingAt(double now, double epoch, double duration)
        {
            if (!double.IsFinite(now) || !double.IsFinite(epoch) || !double.IsFinite(duration)
                || duration <= 0 || epoch < 0) return Math.Max(0, double.IsFinite(duration) ? duration : 0);
            return Math.Max(0, duration - Math.Max(0, now - epoch));
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
            if (!RouteReady || SceneCurtainTransition.IsActive)
            {
                if (display != null) display.Hide();
                return;
            }
            if (session.IsHost)
            {
                if (generation != CurrentGeneration) ResetClock();
                if (resetFrame != Time.frameCount && float.IsFinite(durationSeconds) && durationSeconds >= 1)
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
                            startedAt = Clock; sharedDuration = durationSeconds; revision++; Publish();
                        }
                        // EscapeCampaignContent runs first: exit arrivals win this frame, then only pending players retire.
                        // Its existing all-resolved / at-least-one-arrived result rule owns success and failure.
                        if (HasExpired) player.ServerSetState(PlayerState.Retired);
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
            Present();
        }

        void Present()
        {
            if (display == null) return;
            var local = NetworkPlayer.Local;
            bool visible = RouteReady && HasStarted && generation == CurrentGeneration && IsParticipant(local)
                && !SceneCurtainTransition.IsActive;
            int day = CampaignRun.Instance != null ? CampaignRun.Instance.State.Day : 1;
            display.View.SetSnapshot(new RestTimerSnapshot(visible, day, RestTimeOfDay.Unknown,
                RestTimerMode.Limited, RemainingSeconds, sharedDuration));
        }

        void ResetClock()
        {
            startedAt = -1; sharedDuration = float.IsFinite(durationSeconds) ? Math.Max(1, durationSeconds) : 480;
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
