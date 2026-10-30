using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Results;
using UnityEngine;

namespace CurtainCall.SpaceLink
{
    /// <summary>줄타기 확정 결과를 공용 정산에 한 번 전달한다. 묘기 자체의 성공·실패 규칙은 바꾸지 않는다.</summary>
    public sealed class CampaignTightropeAdapter : MonoBehaviour
    {
        TightropeRun run;
        CampaignPerformanceEntry entry;
        CampaignRun campaign;
        PerformanceRewards rewards;
        CampaignSnapshot performance;
        TightropeResult pending;
        string attemptId;
        int round;
        bool started;
        bool submitted;

        public bool Started => started;

        public void Initialize(TightropeRun target, CampaignPerformanceEntry owner)
        {
            run = target;
            entry = owner;
            campaign = CampaignRun.Instance;
            rewards = PerformanceRewards.EnsureExists();
            run.Results.ResultConfirmed += HandleResult;
            campaign.Changed += HandleCampaign;
        }

        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (run == null || started || submitted || session == null || !session.IsHost
                || !entry.IsCurrentPerformance || !entry.LocalPlaced) return;
            var director = SpaceLinkDirector.Instance;
            if (director != null && director.IsTransitioning) return;
            // NGO 로드 완료와 소유자 순간이동이 모두 도착하기 전에는 판정을 시작하지 않는다.
            var course = run.GetComponent<TightropeCourse>();
            foreach (ulong id in session.NetworkManager.ConnectedClientsIds)
            {
                NetworkPlayer player = null;
                foreach (var candidate in NetworkPlayer.All)
                    if (candidate.IsSpawned && candidate.OwnerClientId == id) { player = candidate; break; }
                if (player == null || player.Slot == NetworkPlayer.NoSlot) return;
                if (player.State == PlayerState.Normal
                    && Vector3.Distance(player.transform.position, course.GetStartPose(player.Slot).position) > 1.5f) return;
            }
            // 밤 정비에서 고른 단계(런 상태, 1일차는 1)로 공용 정산을 연다.
            if (!rewards.BeginPerformance(Mathf.Clamp(campaign.State.Difficulty, 1, 3), GameSettings.Tightrope.Reward)) return;
            performance = campaign.State;
            if (!run.StartExternalPerformance()) return;
            started = true;
            attemptId = run.Results.AttemptId;
            round = run.Round;
        }

        bool SamePerformance()
        {
            var state = campaign.State;
            return state.Phase == CampaignPhase.Performance && state.Generation == performance.Generation
                && state.Day == performance.Day && state.PerformanceIndex == performance.PerformanceIndex
                && state.TrickId == performance.TrickId;
        }

        void HandleCampaign()
        {
            if (!started || SamePerformance()) return;
            pending = null;
            submitted = true;
            run.StopExternalPerformance();
        }

        void HandleResult(TightropeResult result)
        {
            if (!started || submitted || !SamePerformance() || result.AttemptId != attemptId || result.Round != round) return;
            pending = result;
        }

        void LateUpdate()
        {
            if (pending == null) return;
            var result = pending;
            pending = null;
            if (submitted || !SamePerformance() || run.Round != round || run.Results.AttemptId != attemptId
                || rewards.State?.IsOpen != true) return;
            if (run.State != TightropeRunState.Succeeded && run.State != TightropeRunState.Failed) return;
            // 실제 도착자와 각 플레이어의 마지막 위치를 보존한 상태로 정산한다.
            var judgements = new Dictionary<string, string>();
            foreach (var participant in rewards.State.Segments)
                foreach (var person in result.Participants)
                    if (participant.ClientId == person.ClientId) judgements[participant.ParticipantId] = person.Outcome.ToString();
            submitted = true; // FinishPerformance 안의 이벤트 재진입보다 먼저 닫는다.
            run.StopExternalPerformance();
            rewards.FinishPerformance(result.Succeeded, result.Reason.ToString(), judgements);
        }

        void OnDisable()
        {
            if (run != null)
            {
                run.Results.ResultConfirmed -= HandleResult;
                run.StopExternalPerformance();
            }
            if (campaign != null) campaign.Changed -= HandleCampaign;
            pending = null;
        }
    }
}
