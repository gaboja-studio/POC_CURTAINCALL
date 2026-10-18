using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Results;
using CurtainCall.Reward;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>외줄 코스를 공용 공연 명부·정산·시간대에 연결한다. 독립 연습 씬에는 붙이지 않는다.</summary>
    [RequireComponent(typeof(TightropeRun))]
    [DefaultExecutionOrder(100)]
    public sealed class TightropeCampaignBridge : MonoBehaviour
    {
        [SerializeField, Range(1, 3)] int difficulty = 1;
        TightropeRun run;
        bool observing;

        public static TightropeCampaignBridge Current { get; private set; }
        public CampaignRun Campaign { get; private set; }
        public PerformanceRewards Rewards { get; private set; }
        public string PerformanceId { get; private set; }
        public PerformanceResult Result { get; private set; }
        public bool IsTightropePerformance => Campaign != null
            && Campaign.State.Phase == CampaignPhase.Performance && Campaign.State.TrickId == "Tightrope";
        public bool PerformanceOpen => IsTightropePerformance && Rewards != null && Rewards.State?.IsOpen == true
            && Rewards.State.Performance?.Id == PerformanceId && MatchesCurrentPerformance(Rewards.State.Performance);
        public event Action Changed;

        bool MatchesCurrentPerformance(PerformanceIdentity identity) => identity != null && Campaign != null
            && identity.TrickId == "Tightrope" && identity.Generation == Campaign.State.Generation
            && identity.Day == Campaign.State.Day && identity.Index == Campaign.State.PerformanceIndex;

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            run = GetComponent<TightropeRun>();
            Campaign = CampaignRun.EnsureExists();
            Rewards = PerformanceRewards.EnsureExists();
            // Changed는 공용 정산의 스냅샷 송신 직후, 시간대 전환·외팔 부활보다 먼저 온다.
            Rewards.Changed += ObserveRewards;
            Rewards.ResultConfirmed += HandleResult;
            Campaign.StepChanged += HandleStep;
            ObserveRewards();
        }

        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (session == null || session.ConnectionState == SessionConnectionState.Offline
                || session.ConnectionState == SessionConnectionState.Connecting)
            {
                Clear();
                return;
            }
            if (session.IsHost && IsTightropePerformance)
                Rewards.BeginPerformance(Mathf.Clamp(difficulty, 1, 3), GameSettings.Tightrope.Reward);
            ObserveRewards();
        }

        void HandleStep(CampaignSnapshot step)
        {
            if (step.Phase == CampaignPhase.None || step.IsGenerationChangeIntro) Clear();
            Changed?.Invoke();
            // StepChanged는 공용 부활 구독과 같은 호출 스택이다. 새 공연 시작은 다음 Update에서 한다.
        }

        void ObserveRewards()
        {
            if (observing || Rewards == null) return;
            observing = true;
            try
            {
                var snapshot = Rewards.State;
                var identity = snapshot?.Performance;
                if (identity == null || identity.TrickId != "Tightrope") return;
                bool changed = PerformanceId != identity.Id;
                if (changed)
                {
                    PerformanceId = identity.Id;
                    Result = null;
                }
                if (PerformanceOpen && NetworkSessionManager.Instance?.IsHost == true)
                    run.BeginCampaignPerformance(PerformanceId);
                if (snapshot.Latest?.Performance.Id == PerformanceId) AcceptResult(snapshot.Latest);
                run.SynchronizeCampaignState();
                if (changed) Changed?.Invoke();
            }
            finally { observing = false; }
        }

        void HandleResult(PerformanceResult result)
        {
            if (result?.Performance.Id == PerformanceId) AcceptResult(result);
        }

        void AcceptResult(PerformanceResult result)
        {
            if (Result?.Performance.Id == result.Performance.Id) return;
            Result = result;
            run.AcceptCampaignResult(result);
            Changed?.Invoke();
        }

        internal bool TryFinishAtDestination()
        {
            if (!PerformanceOpen || NetworkSessionManager.Instance?.IsHost != true) return false;
            var arrivals = new Dictionary<string, string>();
            foreach (var participant in Rewards.State.Segments)
            {
                if (!participant.Connected || participant.Dead) continue;
                foreach (var player in NetworkPlayer.All)
                    if (player.OwnerClientId == participant.ClientId && run.HasCampaignArrival(player))
                    { arrivals[participant.ParticipantId] = "Arrived"; break; }
            }
            return arrivals.Count > 0 && Rewards.FinishPerformance(true, "Completed", arrivals);
        }

        internal bool FinishTimedOut() => PerformanceOpen && NetworkSessionManager.Instance?.IsHost == true
            && Rewards.FinishPerformance(false, "TimeExpired");

        void Clear()
        {
            if (PerformanceId == null && Result == null) return;
            PerformanceId = null;
            Result = null;
            Changed?.Invoke();
        }

        void OnDisable()
        {
            if (Rewards != null)
            {
                Rewards.Changed -= ObserveRewards;
                Rewards.ResultConfirmed -= HandleResult;
            }
            if (Campaign != null) Campaign.StepChanged -= HandleStep;
            if (Current == this) Current = null;
            Clear();
        }
    }
}
