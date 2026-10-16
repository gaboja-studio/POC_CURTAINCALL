using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Results;
using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>살아 있는 첫 도착자를 호스트가 확인하고 공용 정산에 전달한다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class HumanCannonDestination : MonoBehaviour
    {
        [SerializeField] HumanCannonLandingZone destination;
        [Range(1, 3), SerializeField, Tooltip("공용 보상표 난이도. 별도 호응·보상 수치를 만들지 않는다.")] int difficultyLevel = 1;
        CampaignRun campaign;
        PerformanceRewards rewards;
        string performanceId;
        public static HumanCannonDestination Current { get; private set; }
        public bool PracticeCompleted { get; private set; }
        public bool PracticeFailed { get; private set; }
        public PerformanceResult Result { get; private set; }
        public bool IsResolved => Result != null || PracticeCompleted || PracticeFailed;
        public bool PerformanceOpen => rewards != null && rewards.State?.IsOpen == true
            && rewards.State.Performance?.Id == performanceId;
        public string StatusText => Result != null
            ? $"공연 {(Result.Succeeded ? "성공" : "실패")} · 수입 {Result.Income} · 공용 정산 완료"
            : PracticeCompleted ? "연습 도착 성공! R로 다시 시작 (공용 정산 없음)"
            : PracticeFailed ? "연습 실패 · R로 다시 시작 (공용 정산 없음)"
            : PerformanceOpen ? "살아 있는 1명 목적지 착지 → 공연 성공 / 전멸 → 실패"
            : "초록 FINISH 착지판이 목적지 · 대기 중에는 연습 판정";

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            PracticeCompleted = PracticeFailed = false;
            campaign = CampaignRun.EnsureExists();
            rewards = PerformanceRewards.EnsureExists();
            rewards.ResultConfirmed += Confirmed;
            NetworkPlayer.Restarted += Restart;
        }

        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (session == null || !session.NetworkManager.IsListening)
            {
                performanceId = null;
                Result = null;
                PracticeCompleted = PracticeFailed = false;
                return;
            }
            bool humanCannon = campaign.State.Phase == CampaignPhase.Performance && campaign.State.TrickId == "HumanCannon";
            if (humanCannon && session.IsHost) rewards.BeginPerformance(difficultyLevel);
            var identity = rewards.State?.Performance;
            if (identity?.TrickId == "HumanCannon" && performanceId != identity.Id)
            {
                performanceId = identity.Id;
                Result = null;
                PracticeCompleted = PracticeFailed = false;
            }
            // 결과 알림보다 시간대가 먼저 진행되어도 같은 공연 ID의 공개 스냅샷으로 복원한다.
            if (rewards.Latest != null && rewards.Latest.Performance.Id == performanceId) Confirmed(rewards.Latest);
            if (!session.IsHost || IsResolved) return;
            if (humanCannon && PerformanceOpen)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (!HasArrived(player)) continue;
                    foreach (var participant in rewards.State.Segments)
                        if (participant.ClientId == player.OwnerClientId && participant.Connected && !participant.Dead)
                        {
                            rewards.FinishPerformance(true, "Completed", new Dictionary<string, string> { [participant.ParticipantId] = "Arrived" });
                            return;
                        }
                }
                // 전멸·이탈 실패는 공용 명부 관찰이 처리한다. 자체 정산/시간대 전환을 중복 호출하지 않는다.
            }
            else if (Application.isEditor && session.GameState == GameSessionState.Waiting
                && session.NetworkManager.ConnectedClientsIds.Count == 1)
            {
                var player = NetworkPlayer.Local;
                if (player == null || !player.IsSpawned) return;
                PracticeCompleted = HasArrived(player);
                PracticeFailed = player.State != PlayerState.Normal;
            }
        }

        public bool HasArrived(NetworkPlayer player)
        {
            var cannon = HumanCannonSystem.Current;
            return player != null && player.IsSpawned && player.State == PlayerState.Normal && !player.IsAirborne
                && destination != null && destination.SupportsLanding(player.transform.position)
                && cannon != null && !cannon.IsFlying(player.OwnerClientId) && !cannon.IsSeated(player.OwnerClientId);
        }

        void Confirmed(PerformanceResult result)
        {
            if (result != null && result.Performance.Id == performanceId) Result = result;
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null || !player.IsLocal) return;
            PracticeCompleted = PracticeFailed = false;
            // 정산된 공연을 R로 다시 지급하지 않는다. 새 공연 ID가 와야 결과를 비운다.
        }

        void OnDisable()
        {
            if (rewards != null) rewards.ResultConfirmed -= Confirmed;
            NetworkPlayer.Restarted -= Restart;
            if (Current == this) Current = null;
        }
    }
}
