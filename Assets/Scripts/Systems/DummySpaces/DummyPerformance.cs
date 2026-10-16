using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>독립 더미 묘기 씬의 상황 입력. 결과·보상·생존 실패 판정과 시간대 전환은 031에 맡긴다.</summary>
    [RequireComponent(typeof(DummySpaceDiagnostics))]
    public sealed class DummyPerformance : MonoBehaviour
    {
        readonly HashSet<string> arrived = new();
        CampaignRun campaign;
        PerformanceRewards rewards;
        NetworkSessionManager session;
        DummySpaceDiagnostics diagnostics;
        string message = "";
        readonly HashSet<string> clockIds = new();
        string observedPerformanceId;
        string clockId;
        float performanceTimeRemaining;
        float performanceTimeLimit;
        bool clockRunning;
        bool clockFinished;
        bool expiryRequested;
        string clockStatus = "공연 명부 시작 대기";

        public float PerformanceTimeRemaining => performanceTimeRemaining;
        public float PerformanceTimeLimit => performanceTimeLimit;
        public bool ClockRunning => clockRunning && CanRunClock() && clockId == rewards.State?.Performance?.Id;
        public string ClockStatus => campaign != null && session != null
            && (session.ConnectionState != SessionConnectionState.Offline || (campaign.State.IsRunning && !campaign.IsOfflineRun))
                ? "더미 자동 시계는 오프라인 전용 · 온라인 만료는 수동 입력"
                : clockStatus;
        public string Message => message;
        public PerformanceRewards Rewards => rewards;
        public bool HasArrived(string id) => arrived.Contains(id);
        public bool CanAct(string id) => ActivePerson(id) != null;

        void Start()
        {
            diagnostics = GetComponent<DummySpaceDiagnostics>();
            campaign = CampaignRun.EnsureExists();
            rewards = PerformanceRewards.EnsureExists();
            session = NetworkSessionManager.EnsureExists();
            rewards.ResultConfirmed += HandleResult;
            // 씬 재로드 전의 잔여 시간은 복원하지 못한다. 기존 명부에 새 전체 시간을 주지 않는다.
            observedPerformanceId = rewards.State?.Performance?.Id;
            if (rewards.State?.IsOpen == true && !string.IsNullOrEmpty(observedPerformanceId))
            {
                clockIds.Add(observedPerformanceId);
                clockStatus = "기존 공연 명부 · 시계 복원 미지원 · 만료는 수동 입력";
            }
        }

        void Update()
        {
            ObserveClock();
            if (!ClockRunning) return;
            performanceTimeRemaining = Mathf.Max(0f, performanceTimeRemaining - Time.deltaTime);
            if (performanceTimeRemaining > 0f || expiryRequested) return;
            // 공통 종료 API가 동기 이벤트를 발생시켜도 같은 명부의 만료는 한 번만 요청한다.
            expiryRequested = true;
            clockFinished = true;
            clockRunning = false;
            clockStatus = "더미 공연 시계 자연 만료 · 종료 요청 완료";
            diagnostics.Story("더미 공연 시계 자연 만료 → 제한 시간 종료 요청");
            TimeExpired();
        }

        bool CanRunClock() => isActiveAndEnabled && campaign != null && rewards != null && session != null
            && campaign.IsOfflineRun && session.ConnectionState == SessionConnectionState.Offline
            && campaign.HasAuthority && rewards.HasAuthority && campaign.State.Phase == CampaignPhase.Performance
            && rewards.State?.IsOpen == true && !rewards.TransitionPending;

        void ObserveClock()
        {
            string id = rewards.State?.Performance?.Id;
            if (id != observedPerformanceId)
            {
                StopClock("공연 명부 변경", true);
                arrived.Clear();
                observedPerformanceId = id;
            }
            if (!CanRunClock())
            {
                bool ended = rewards.State?.IsOpen != true || campaign.State.Phase != CampaignPhase.Performance
                    || rewards.TransitionPending;
                StopClock(ended ? "공연 종료 또는 시간대 이탈" : "오프라인 판정 권한 대기", ended);
                return;
            }
            if (string.IsNullOrEmpty(id)) return;
            if (id != clockId)
            {
                if (!clockIds.Add(id)) return;
                clockId = id;
                performanceTimeLimit = GameSettings.Tightrope.Run.TimeLimit;
                performanceTimeRemaining = performanceTimeLimit;
                clockFinished = false;
                expiryRequested = false;
                clockRunning = true;
                clockStatus = "더미 공연 시계 진행 중 · 줄타기 제한 시간 참조";
                diagnostics.Story($"더미 공연 시계 시작 · 제한 시간 {performanceTimeLimit:0.#}초 (줄타기 설정 참조)");
            }
            else if (!clockFinished && !expiryRequested && !clockRunning)
            {
                // 드라이버 재활성화·권한 복귀는 보관한 잔여 시간만 재개한다.
                clockRunning = true;
                clockStatus = "더미 공연 시계 재개 · 보관한 잔여 시간 사용";
            }
        }

        void StopClock(string reason, bool finished, bool record = true)
        {
            if (finished) clockFinished = true;
            if (!clockRunning) return;
            clockRunning = false;
            clockStatus = $"더미 공연 시계 중지 · {reason}";
            if (record) diagnostics.Story($"더미 공연 시계 중지 · {reason}");
        }

        void OnDisable() => StopClock("드라이버 비활성 · 잔여 시간 보관", false);

        void OnDestroy()
        {
            if (rewards != null) rewards.ResultConfirmed -= HandleResult;
        }

        /// <summary>접속 없는 독립 씬에서만 새 테스트 런을 시작한다. 진행 중인 런은 초기화하지 않는다.</summary>
        public bool StartOfflineRun()
        {
            if (campaign == null || session == null || campaign.State.IsRunning
                || session.ConnectionState != SessionConnectionState.Offline) return false;
            return diagnostics.Invoke("StartOfflineRun → EndIntro", () => Report(campaign.StartOfflineRun() && campaign.EndIntro(), "오프라인 런 시작"));
        }

        /// <summary>오프라인은 2명 입력 명부, 온라인은 실제 spawn된 참가 명부를 031에 전달한다.</summary>
        public bool BeginPerformance()
        {
            if (rewards == null || campaign == null) return false;
            string previous = rewards.State?.Performance?.Id;
            bool began = campaign.IsOfflineRun
                ? rewards.BeginOfflinePerformance(new[]
                {
                    new RewardParticipantSeed("Dummy-0", 0, 0),
                    new RewardParticipantSeed("Dummy-1", 1, 1)
                }, 1)
                : rewards.BeginPerformance(1);
            if (began)
            {
                if (previous != rewards.State?.Performance?.Id) arrived.Clear();
                ObserveClock();
            }
            diagnostics.Note("실제 공통 처리", "BeginPerformance/BeginOfflinePerformance", $"반환 {began}, 명부 {rewards.State?.Performance?.Id}; 정산은 031");
            return Report(began, "공연 명부 고정");
        }

        public bool Arrive(string participantId) => diagnostics.Invoke($"도착 {participantId} → 031 종료 조건 입력", () => ArriveCore(participantId));

        bool ArriveCore(string participantId)
        {
            var person = ActivePerson(participantId);
            if (person == null || arrived.Contains(participantId)) return false;
            arrived.Add(participantId);
            var judgements = new Dictionary<string, string>();
            foreach (var other in rewards.State.Segments)
            {
                if (!other.Connected || other.Dead) continue;
                if (!arrived.Contains(other.ParticipantId)) return Report(true, "도착 입력");
                judgements[other.ParticipantId] = "Arrived";
            }
            return Report(rewards.FinishPerformance(true, "Completed", judgements), "도착 종료 요청");
        }

        public bool Cut(string participantId) => diagnostics.Invoke($"절단 {participantId} → 030/031", () => ChangePerson(participantId, false));
        public bool Kill(string participantId) => diagnostics.Invoke($"사망 {participantId} → 030/031", () => ChangePerson(participantId, true));
        // 기존 UI의 유효 참가자 조건을 공개 입력에서도 동일하게 적용한다.
        public bool Cheer(string id) => diagnostics.Invoke($"환호 {id} → 031 AddCheer", () => ActivePerson(id) != null && rewards.AddCheer(id));

        public bool TimeExpired() => diagnostics.Invoke("제한 시간 → 031 FinishPerformance → 028", () => Report(rewards != null && rewards.FinishPerformance(false, "TimeExpired"), "제한 시간 종료 요청"));

        RewardSegment ActivePerson(string id)
        {
            if (rewards == null || !rewards.HasAuthority || rewards.State == null || !rewards.State.IsOpen
                || campaign.State.Phase != CampaignPhase.Performance || rewards.TransitionPending) return null;
            foreach (var person in rewards.State.Segments)
                if (person.ParticipantId == id && person.Connected && !person.Dead) return person;
            return null;
        }

        bool ChangePerson(string id, bool dead)
        {
            var person = ActivePerson(id);
            if (person == null) return false;
            if (!campaign.IsOfflineRun)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (!player.IsSpawned || player.OwnerClientId != person.ClientId) continue;
                    if (dead) return Report(player.ServerKill(), "사망 입력");
                    return Report(player.ServerCut() != BodyPart.None, "절단 입력");
                }
                return Report(false, "네트워크 참가자 찾기");
            }

            // 오프라인 입력에도 030의 고정 절단 순서를 쓴다. 031이 결과·최소 생존 인원을 판정한다.
            var parts = PlayerCondition.PartsForLostCount(person.LostCount);
            if (!dead) parts |= PlayerCondition.NextFixedCut(parts);
            return Report(rewards.ObserveParticipant(id, (int)parts, dead || PlayerCondition.IsAllLimbsLost(parts)), dead ? "사망 입력" : "절단 입력");
        }

        void HandleResult(CurtainCall.Results.PerformanceResult result)
        {
            // 결과 문장은 진단 컴포넌트가 기록한다. 뒤늦은 중지 문장으로 결과를 덮지 않는다.
            StopClock("공통 공연 결과 확정", true, false);
            Debug.Log($"[DummyPerformance] {(result.Succeeded ? "성공" : "실패")} / {result.EndReason} / {result.Failure} / 수입 {result.Income}");
        }

        bool Report(bool applied, string operation)
        {
            message = $"{operation}: {(applied ? "적용" : "거절/변화 없음")}";
            return applied;
        }
    }
}
