using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Settings;
using CurtainCall.Survival;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>독립 씬의 공통 관찰 화면. 게임 규칙·씬 이동·참가자 상태를 소유하지 않는다.</summary>
    public sealed class DummySpaceDiagnostics : MonoBehaviour
    {
        readonly Queue<string> trace = new();
        readonly Queue<string> story = new();
        readonly Dictionary<string, RewardSegment> observedPeople = new();
        CampaignSnapshot observedStep;
        string observedPerformance;
        float startedAt;
        public string StoryText => string.Join("\n", story);
        public string CurrentStory { get; private set; } = "런 시작을 기다립니다.";
        public int StoryCount => story.Count;
        public string LastNote { get; private set; } = "아직 실행한 상황이 없습니다.";
        public CampaignRun Campaign { get; private set; }
        public PerformanceRewards Rewards { get; private set; }
        public string TraceText => string.Join("\n", trace);

        void Awake()
        {
            Campaign = CampaignRun.EnsureExists();
            Rewards = PerformanceRewards.EnsureExists();
            startedAt = Time.time;
            observedStep = Campaign.State;
            Campaign.Changed += ObserveTimer;
            Rewards.Changed += ObservePeople;
            Campaign.StepChanged += Step;
            Campaign.RecallAlarm += Alarm;
            Campaign.RunEnded += Ended;
            Rewards.ResultConfirmed += Result;
        }

        void OnDestroy()
        {
            if (Campaign != null)
            {
                Campaign.Changed -= ObserveTimer;
                Campaign.StepChanged -= Step;
                Campaign.RecallAlarm -= Alarm;
                Campaign.RunEnded -= Ended;
            }
            if (Rewards != null)
            {
                Rewards.Changed -= ObservePeople;
                Rewards.ResultConfirmed -= Result;
            }
        }

        public void Story(string message)
        {
            if (story.Count == 24) story.Dequeue();
            int seconds = Mathf.FloorToInt(Mathf.Max(0, Time.time - startedAt));
            CurrentStory = message;
            story.Enqueue($"[{seconds / 60:00}:{seconds % 60:00}] {message}");
        }

        void Step(CampaignSnapshot state)
        {
            Note("실제 공통 처리", "028 StepChanged", CampaignRun.Describe(state));
            bool expired = observedStep.Phase == CampaignPhase.RestTime && observedStep.Timer == RestTimerState.Running
                && observedStep.TimeRemaining <= Time.deltaTime + 0.001f;
            Story($"{(expired ? "탐색 제한 시간 만료 → " : "시간대 전환 → ")}{DummySpaceCanvas.PhaseName(state.Phase)} · {state.Day}일차");
        }

        void ObserveTimer()
        {
            var next = Campaign.State;
            if (next.Phase == CampaignPhase.RestTime && next.Timer == RestTimerState.Running
                && (observedStep.Phase != CampaignPhase.RestTime || observedStep.Timer != RestTimerState.Running))
                Story("탐색 제한 시간 시작 · 시간이 실제로 흐릅니다.");
            observedStep = next;
        }

        void ObservePeople()
        {
            var state = Rewards.State;
            if (state == null) return;
            string id = state.Performance?.Id;
            if (id != observedPerformance)
            {
                observedPeople.Clear();
                observedPerformance = id;
                if (state.IsOpen) Story($"공연 명부 고정 · 참가자 {state.Segments.Count}명");
            }
            foreach (var p in state.Segments)
            {
                if (observedPeople.TryGetValue(p.ParticipantId, out var before))
                {
                    if (p.LostCount != before.LostCount || p.Dead != before.Dead)
                        Story($"자리 {p.Slot + 1} 부상: 손실 {before.LostCount} → {p.LostCount} · {(p.Dead ? "사망" : "생존")}");
                    else if (p.Connected != before.Connected) Story($"자리 {p.Slot + 1} {(p.Connected ? "연결" : "이탈")}");
                    else if (state.IsOpen && p.Amount != before.Amount) Story($"자리 {p.Slot + 1} 호응 {before.Amount} → {p.Amount}");
                }
                observedPeople[p.ParticipantId] = p;
            }
        }

        void Alarm()
        {
            Note("실제 공통 처리", "028 RecallAlarm", "소집 경보 → 복귀 신호 대기");
            Story("소집 경보 · 탐색을 마치고 무대 입구로 복귀하세요.");
        }

        void Ended(RunEndReason reason)
        {
            Note("실제 공통 처리", "028 RunEnded", reason.ToString());
            Story($"런 종료 · {reason}");
        }

        void Result(CurtainCall.Results.PerformanceResult result)
        {
            Note("실제 공통 처리", "031 ResultConfirmed",
                $"{result.EndReason}/{result.Failure}, 수입 {result.Income} → 공동 자금 {Rewards.Balance} → 028 {Campaign.State.Phase}");
            Story($"공연 {(result.Succeeded ? "성공" : "실패")} 확정 · {(result.EndReason == "TimeExpired" ? "제한 시간 만료" : result.EndReason)} · 수입 {result.Income} · 현재 {DummySpaceCanvas.PhaseName(Campaign.State.Phase)}");
        }

        public void Note(string source, string input, string output)
        {
            if (trace.Count == 12) trace.Dequeue();
            string line = $"[{source}] {input} → {output}";
            LastNote = line;
            trace.Enqueue(line);
            Debug.Log($"[DummySpaces] {line}");
        }

        public string Variables()
        {
            var s = Campaign.State;
            return $"세대{s.Generation}/일차{s.Day}/{s.Phase}/{s.Rest}/공연{s.PerformanceIndex + 1}, "
                + $"타이머{s.Timer}/{s.TimeRemaining:F1}초/경보{s.AlarmRaised}, 준비{s.ReadyCount}/{s.RequiredCount}, 자금{Rewards.Balance}";
        }

        public bool Invoke(string input, Func<bool> action)
        {
            string before = Variables();
            bool applied = action();
            Note("실제 공통 처리", input, $"{(applied ? "true" : "false/변화 없음")} | {before} → {Variables()}");
            return applied;
        }

        /// <summary>리셋하지 않고 현재 시간대에서 공개 API 한 단계만 진행한다. 공연 종료는 반드시 031 경유.</summary>
        public bool AdvanceOffline()
        {
            var session = NetworkSessionManager.EnsureExists();
            if (session.ConnectionState != SessionConnectionState.Offline || (Campaign.State.IsRunning && !Campaign.IsOfflineRun))
            {
                Note("더미 입력", "오프라인 준비", "접속 중 또는 온라인 런: 준비 거절");
                return false;
            }
            switch (Campaign.State.Phase)
            {
                case CampaignPhase.None: return Invoke("준비 → StartOfflineRun", Campaign.StartOfflineRun);
                case CampaignPhase.Intro: return Invoke("준비 → EndIntro", Campaign.EndIntro);
                case CampaignPhase.Performance: return CompleteOfflinePerformance();
                case CampaignPhase.RestTime: return Invoke("수동 전원 무대 입구 신호 → NotifyAllAtStageEntrance", Campaign.NotifyAllAtStageEntrance);
                default:
                    Note("더미 입력", "오프라인 준비", "밤은 준비 완료, 탈출은 종료 신호를 직접 입력하세요.");
                    return false;
            }
        }

        public bool CompleteOfflinePerformance()
        {
            if (!Campaign.IsOfflineRun || Campaign.State.Phase != CampaignPhase.Performance) return false;
            bool began = Invoke("더미 보상 명부 2명 → BeginOfflinePerformance", () => Rewards.BeginOfflinePerformance(new[]
            {
                new RewardParticipantSeed("Dummy-0", 0, 0), new RewardParticipantSeed("Dummy-1", 1, 1)
            }, 1));
            if (!began) return false;
            // 자금 확인용 환호 1회. 지급액은 031의 현재 보상 세팅이 정한다.
            if (!Invoke("더미 환호 → AddCheer(Dummy-0)", () => Rewards.AddCheer("Dummy-0"))) return false;
            return Invoke("수동 공연 성공 신호 → FinishPerformance", () => Rewards.FinishPerformance(true, "DummyPrepared"));
        }

        public bool CatchPlayer(NetworkPlayer player, SurvivalPlace expected)
        {
            if (player == null || !player.IsSpawned || !player.IsServer || SurvivalRules.ServerPlace != expected)
            {
                Note("미연결", "ServerCatch", $"서버 대상/장소 확인 필요: 현재 {SurvivalRules.ServerPlace}, 요청 {expected}. 장소 설정은 032");
                return false;
            }
            var before = $"{player.State}/{player.LostParts}";
            var outcome = player.ServerCatch();
            Note("실제 공통 처리", "NetworkPlayer.ServerCatch", $"{outcome}: {before} → {player.State}/{player.LostParts}");
            return outcome != CatchOutcome.None;
        }

        public bool RestorePlayer(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned || !player.IsServer) return false;
            var before = player.LostParts;
            var part = player.ServerRestorePart();
            Note("실제 공통 처리", "ServerRestorePart", $"{part}: {before} → {player.LostParts}; 지출 없음");
            return part != BodyPart.None;
        }
    }
}
