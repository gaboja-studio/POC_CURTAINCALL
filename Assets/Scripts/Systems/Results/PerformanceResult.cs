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
        public long Segment { get; }
        public long Settlement { get; }
        public double? MaximumDistance { get; }

        public PerformanceParticipantResult(string participantId, ulong clientId, int slot, int startingLostCount,
            int endingLostCount, bool dead, bool connected, string judgement, long segment, long settlement, double? maximumDistance = null)
        {
            if (string.IsNullOrWhiteSpace(participantId) || participantId.Length > 100 || slot < -1 || slot > 63
                || startingLostCount < 0 || startingLostCount > 4 || endingLostCount < 0 || endingLostCount > 4
                || string.IsNullOrWhiteSpace(judgement) || judgement.Length > 100 || segment < 0 || settlement < 0
                || settlement > segment || ((!connected || dead) && (segment != 0 || settlement != 0)))
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
        }
    }

    /// <summary>부활·시간대 전환 전에 확정한다. NetworkPlayer나 가변 명부를 노출하지 않는다.</summary>
    public sealed class PerformanceResult
    {
        public const int BaseFundPerParticipant = 10;
        public PerformanceIdentity Performance { get; }
        public bool Succeeded => Failure == PerformanceFailure.None;
        public PerformanceFailure Failure { get; }
        public string EndReason { get; }
        public bool AllSurvived { get; }
        public long Income { get; }
        public long BaseReward { get; }
        public long BonusReward { get; }
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
            long income = 0;
            bool allSurvived = copy.Length > 0;
            for (int i = 0; i < copy.Length; i++)
            {
                var person = participants[i];
                if (person == null || !keys.Add(person.ParticipantId)
                    || (failure != PerformanceFailure.None && person.Settlement != 0)
                    || (failure == PerformanceFailure.None && person.Settlement != person.Segment))
                    throw new ArgumentException("공연 정산이 올바르지 않습니다.");
                copy[i] = person;
                income = checked(income + person.Settlement);
                allSurvived &= person.Eligible;
                if (person.Dead) deaths.Add(person);
            }
            Performance = performance;
            Failure = failure;
            EndReason = endReason;
            // 시작 명부를 기준으로 지급한다. 사망·이탈·공연 실패도 기본 자금은 유지한다.
            BaseReward = checked(copy.Length * (long)BaseFundPerParticipant);
            BonusReward = income;
            Income = checked(BaseReward + BonusReward);
            AllSurvived = allSurvived;
            Participants = Array.AsReadOnly(copy);
            Deaths = deaths.AsReadOnly();
        }
    }
}
