using System;
using System.Collections.Generic;

namespace CurtainCall.Results
{
    public enum PerformanceFailure : byte { None, TrickFailed, InsufficientSurvivors }

    /// <summary>세션·세대·일차·순번이 공연의 동일성이다. 다시 시작해도 같은 지급 ID를 쓴다.</summary>
    public sealed class PerformanceIdentity
    {
        public string SessionId { get; }
        public int Generation { get; }
        public int Day { get; }
        public int Index { get; }
        public string TrickId { get; }
        public string Id => $"{SessionId}:{Generation}:{Day}:{Index}";

        public PerformanceIdentity(string sessionId, int generation, int day, int index, string trickId)
        {
            if (!Guid.TryParseExact(sessionId, "N", out _) || generation < 1 || day < 1 || index < 0
                || string.IsNullOrWhiteSpace(trickId) || trickId.Length > 100)
                throw new ArgumentException("공연 식별자가 올바르지 않습니다.");
            SessionId = sessionId;
            Generation = generation;
            Day = day;
            Index = index;
            TrickId = trickId;
        }
    }

    public sealed class PerformanceParticipantResult
    {
        public string ParticipantId { get; }
        public ulong ClientId { get; }
        public int Slot { get; }
        public int StartingLostCount { get; }
        public int EndingLostCount { get; }
        public bool Dead { get; }
        public bool Connected { get; }
        public bool Eligible => Connected && !Dead;
        public string Judgement { get; }
        /// <summary>확정 호응도(0~단계 상한). 사망자는 사망 전 값, 이탈자는 0.</summary>
        public long Segment { get; }
        /// <summary>지급 토큰. 생존 = 호응×2 + 완주 보너스, 사망 = 호응×½×2(= 호응), 이탈 = 0.</summary>
        public long Settlement { get; }
        /// <summary>실제 도착자만 받는 완주 보너스. <see cref="Settlement"/>에 포함되어 있다.</summary>
        public long CompletionBonus { get; }
        public bool Arrived => CompletionBonus > 0;
        public double? MaximumDistance { get; }

        /// <summary>호응 보상 지급액. 완주 보너스를 뺀 값이다.</summary>
        public static long CheerPayout(long segment, bool dead, bool connected) => !connected ? 0 : dead ? segment : checked(segment * 2);

        public PerformanceParticipantResult(string participantId, ulong clientId, int slot, int startingLostCount,
            int endingLostCount, bool dead, bool connected, string judgement, long segment, long settlement,
            double? maximumDistance = null, long completionBonus = 0)
        {
            if (string.IsNullOrWhiteSpace(participantId) || participantId.Length > 100 || slot < -1 || slot > 63
                || startingLostCount < 0 || startingLostCount > 4 || endingLostCount < 0 || endingLostCount > 4
                || string.IsNullOrWhiteSpace(judgement) || judgement.Length > 100 || segment < 0 || settlement < 0 || completionBonus < 0
                || (!connected && segment != 0) || ((!connected || dead) && completionBonus != 0)
                || settlement != checked(CheerPayout(segment, dead, connected) + completionBonus))
                throw new ArgumentException("참가자 결과가 올바르지 않습니다.");
            if (maximumDistance.HasValue && (!double.IsFinite(maximumDistance.Value) || maximumDistance.Value < 0))
                throw new ArgumentException("Invalid maximum distance.");
            MaximumDistance = maximumDistance;
            ParticipantId = participantId;
            ClientId = clientId;
            Slot = slot;
            StartingLostCount = startingLostCount;
            EndingLostCount = endingLostCount;
            Dead = dead;
            Connected = connected;
            Judgement = judgement;
            Segment = segment;
            Settlement = settlement;
            CompletionBonus = completionBonus;
        }
    }

    /// <summary>
    /// 부활·시간대 전환 전에 확정한다. NetworkPlayer나 가변 명부를 노출하지 않는다.
    /// 실패(전원 사망)도 사망 전 호응 보상을 지급한다. 기본 자금·전원 생존 보너스는 2026-10-28 결정으로 없앴다.
    /// </summary>
    public sealed class PerformanceResult
    {
        public PerformanceIdentity Performance { get; }
        public bool Succeeded => Failure == PerformanceFailure.None;
        public PerformanceFailure Failure { get; }
        public string EndReason { get; }
        public long Income { get; }
        /// <summary>호응 보상 합계(생존 호응×2 + 사망 호응).</summary>
        public long CheerReward { get; }
        /// <summary>실제 도착자 완주 보너스 합계.</summary>
        public long CompletionReward { get; }
        public double? ElapsedSeconds { get; }
        public double? CourseLength { get; }
        public IReadOnlyList<PerformanceParticipantResult> Participants { get; }
        public IReadOnlyList<PerformanceParticipantResult> Deaths { get; }

        public PerformanceResult(PerformanceIdentity performance, PerformanceFailure failure, string endReason,
            IReadOnlyList<PerformanceParticipantResult> participants, double? elapsedSeconds = null, double? courseLength = null)
        {
            if (performance == null || failure > PerformanceFailure.InsufficientSurvivors
                || string.IsNullOrWhiteSpace(endReason) || endReason.Length > 100
                || participants == null || participants.Count > 64)
                throw new ArgumentException("공연 결과가 올바르지 않습니다.");
            if ((elapsedSeconds.HasValue && (!double.IsFinite(elapsedSeconds.Value) || elapsedSeconds.Value < 0))
                || (courseLength.HasValue && (!double.IsFinite(courseLength.Value) || courseLength.Value <= 0)))
                throw new ArgumentException("Invalid performance record.");
            ElapsedSeconds = elapsedSeconds;
            CourseLength = courseLength;
            var copy = new PerformanceParticipantResult[participants.Count];
            var deaths = new List<PerformanceParticipantResult>();
            var keys = new HashSet<string>();
            long cheer = 0, completion = 0;
            for (int i = 0; i < copy.Length; i++)
            {
                var person = participants[i];
                if (person == null || !keys.Add(person.ParticipantId))
                    throw new ArgumentException("공연 정산이 올바르지 않습니다.");
                copy[i] = person;
                completion = checked(completion + person.CompletionBonus);
                cheer = checked(cheer + person.Settlement - person.CompletionBonus);
                if (person.Dead) deaths.Add(person);
            }
            Performance = performance;
            Failure = failure;
            EndReason = endReason;
            CheerReward = cheer;
            CompletionReward = completion;
            Income = checked(cheer + completion);
            Participants = Array.AsReadOnly(copy);
            Deaths = deaths.AsReadOnly();
        }
    }
}
