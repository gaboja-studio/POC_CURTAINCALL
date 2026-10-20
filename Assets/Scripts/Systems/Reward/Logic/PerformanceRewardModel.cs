using System;
using System.Collections.Generic;
using CurtainCall.Results;

namespace CurtainCall.Reward
{
    /// <summary>호스트 전용 판정. 네트워크·Campaign·Unity 수명은 공개 진입점에서 연결한다.</summary>
    public sealed class PerformanceRewardModel
    {
        sealed class Participant
        {
            public RewardParticipantSeed Seed;
            public int Parts;
            public int AwardedParts;
            public bool Dead;
            public bool Connected = true;
            public long Amount;
            public double? MaximumDistance;
        }

        readonly List<Participant> roster = new();
        readonly HashSet<string> begun = new();
        readonly List<FundEntry> ledger = new();
        readonly List<PerformanceResult> history = new();
        PerformanceIdentity performance;
        RewardRules rules;
        double? elapsedSeconds, courseLength;

        public string SessionId { get; }
        public int Generation { get; private set; }
        public long Revision { get; private set; } = 1;
        public long Balance { get; private set; }
        public bool IsOpen { get; private set; }
        public int SurvivorCount
        {
            get
            {
                int count = 0;
                foreach (var person in roster) if (!person.Dead && person.Connected) count++;
                return count;
            }
        }
        public PerformanceIdentity Performance => performance;
        public PerformanceResult Latest => history.Count == 0 ? null : history[^1];
        public IReadOnlyList<PerformanceResult> History => history.AsReadOnly();
        public IReadOnlyList<FundEntry> Ledger => ledger.AsReadOnly();

        public PerformanceRewardModel(string sessionId, int generation)
        {
            if (!Guid.TryParseExact(sessionId, "N", out _) || generation < 1)
                throw new ArgumentException("런 식별자가 올바르지 않습니다.");
            SessionId = sessionId;
            Generation = generation;
        }

        public bool Begin(PerformanceIdentity identity, IReadOnlyList<RewardParticipantSeed> people, RewardRules currentRules)
        {
            if (identity == null || identity.SessionId != SessionId || identity.Generation != Generation
                || people == null || people.Count > 64) return false;
            if (performance != null && performance.Id == identity.Id) return true;
            if (IsOpen || begun.Contains(identity.Id) || (performance != null
                && (identity.Day < performance.Day || (identity.Day == performance.Day && identity.Index <= performance.Index)))) return false;
            var next = new List<Participant>();
            var ids = new HashSet<string>();
            var clients = new HashSet<ulong>();
            foreach (var person in people)
            {
                if (person == null || !ids.Add(person.Id) || !clients.Add(person.ClientId)) return false;
                next.Add(new Participant
                {
                    Seed = person, Parts = person.LostParts, AwardedParts = person.LostParts, Dead = person.Dead
                });
            }
            performance = identity;
            elapsedSeconds = courseLength = null;
            roster.Clear();
            roster.AddRange(next);
            begun.Add(identity.Id);
            rules = currentRules;
            IsOpen = true;
            Revision++;
            return true;
        }

        // Sample without publishing each frame; Confirm freezes the record.
        public bool Record(string identity, string participantId, double? elapsed, double length, double distance)
        {
            if (!IsOpen || performance.Id != identity || !double.IsFinite(length) || length <= 0
                || !double.IsFinite(distance) || (elapsed.HasValue && (!double.IsFinite(elapsed.Value) || elapsed.Value < 0))) return false;
            var person = Find(participantId);
            if (person == null) return false;
            courseLength = length;
            if (elapsed.HasValue) elapsedSeconds = Math.Max(elapsedSeconds ?? 0, elapsed.Value);
            if (!person.Dead && person.Connected)
                person.MaximumDistance = Math.Max(person.MaximumDistance ?? 0, Math.Clamp(distance, 0, length));
            return true;
        }

        public bool ApplyRules(RewardRules currentRules)
        {
            if (!IsOpen || rules.Equals(currentRules)) return false;
            rules = currentRules;
            foreach (var person in roster) person.Amount = Math.Min(person.Amount, rules.Cap);
            Revision++;
            return true;
        }

        public bool Cheer(string participantId, bool positive, RewardRules currentRules)
            => React(participantId, positive ? currentRules.Cheer : -currentRules.Jeer, currentRules);

        public bool React(string participantId, int delta, RewardRules currentRules)
        {
            if (!IsOpen) return false;
            var person = Find(participantId);
            if (person == null || person.Dead || !person.Connected) return false;
            ApplyRules(currentRules);
            long amount = Math.Clamp(person.Amount + (long)delta, 0, rules.Cap);
            if (amount == person.Amount) return false;
            person.Amount = amount;
            Revision++;
            return true;
        }

        /// <summary>같은 부위의 재관찰·복구·재절단은 중복 가산하지 않는다. 사망·이탈은 이 공연 안에서 확정한다.</summary>
        public bool Observe(string participantId, int lostParts, bool dead, bool connected, RewardRules currentRules)
        {
            if (!IsOpen || (lostParts & ~15) != 0) return false;
            var person = Find(participantId);
            if (person == null) return false;
            ApplyRules(currentRules);
            bool nextDead = person.Dead || dead || lostParts == 15;
            bool nextConnected = person.Connected && connected;
            int newlyCut = RewardParticipantSeed.CountLimbs(lostParts & ~person.AwardedParts);
            long amount = nextDead || !nextConnected ? 0
                : Math.Min(rules.Cap, person.Amount + (long)newlyCut * rules.CutBonus);
            bool changed = person.Parts != lostParts || person.Dead != nextDead || person.Connected != nextConnected || person.Amount != amount;
            person.Parts = lostParts;
            person.AwardedParts |= lostParts;
            person.Dead = nextDead;
            person.Connected = nextConnected;
            person.Amount = amount;
            if (changed) Revision++;
            return changed;
        }

        public bool Confirm(bool trickSucceeded, string endReason, IReadOnlyDictionary<string, string> judgements,
            int minimumSurvivors, RewardRules currentRules, out PerformanceResult result)
        {
            result = null;
            if (!IsOpen || minimumSurvivors < 1 || minimumSurvivors > 64
                || string.IsNullOrWhiteSpace(endReason) || endReason.Length > 100) return false;
            if (judgements != null)
                foreach (var judgement in judgements)
                    if (Find(judgement.Key) == null || string.IsNullOrWhiteSpace(judgement.Value) || judgement.Value.Length > 100) return false;
            int survivors = 0;
            foreach (var person in roster) if (!person.Dead && person.Connected) survivors++;
            var failure = survivors < minimumSurvivors ? PerformanceFailure.InsufficientSurvivors
                : trickSucceeded ? PerformanceFailure.None : PerformanceFailure.TrickFailed;
            bool allSurvived = survivors == roster.Count && survivors > 0;
            var people = new PerformanceParticipantResult[roster.Count];
            for (int i = 0; i < people.Length; i++)
            {
                var person = roster[i];
                long amount = person.Dead || !person.Connected || person.Seed.Forfeit ? 0 : Math.Min(person.Amount, currentRules.Cap);
                if (failure == PerformanceFailure.None && allSurvived && !person.Seed.Forfeit)
                    amount = Math.Min(currentRules.Cap, amount + currentRules.AllSurvivedBonus);
                string judgement = judgements != null && judgements.TryGetValue(person.Seed.Id, out var supplied)
                    ? supplied : person.Dead ? "Dead" : !person.Connected ? "Disconnected" : person.Seed.Forfeit ? "Forfeited" : "Survived";
                people[i] = new PerformanceParticipantResult(person.Seed.Id, person.Seed.ClientId, person.Seed.Slot,
                    RewardParticipantSeed.CountLimbs(person.Seed.LostParts), RewardParticipantSeed.CountLimbs(person.Parts),
                    person.Dead, person.Connected, judgement, amount, failure == PerformanceFailure.None ? amount : 0, person.MaximumDistance);
            }
            var next = new PerformanceResult(performance, failure, endReason, people, elapsedSeconds, courseLength);
            if (next.Income > long.MaxValue - Balance) return false;
            long balance = Balance + next.Income;
            var entry = new FundEntry(ledger.Count + 1, Generation, performance.Day, FundEntryKind.PerformanceIncome,
                next.Income, balance, next.Succeeded ? "공연 성공 정산" : "공연 실패 정산(0)", performance.Id);
            for (int i = 0; i < roster.Count; i++) roster[i].Amount = people[i].Segment;
            rules = currentRules;
            IsOpen = false;
            Balance = balance;
            ledger.Add(entry);
            history.Add(next);
            if (history.Count > 10) history.RemoveAt(0);
            Revision++;
            result = next;
            return true;
        }

        public bool Spend(long amount, string description, int day)
        {
            if (amount <= 0 || amount > Balance || day < 1 || string.IsNullOrWhiteSpace(description) || description.Length > 100) return false;
            long next = Balance - amount;
            var entry = new FundEntry(ledger.Count + 1, Generation, day, FundEntryKind.Expense, -amount, next, description);
            Balance = next;
            ledger.Add(entry);
            Revision++;
            return true;
        }

        public bool AdvanceGeneration(int generation, int day)
        {
            if (generation <= Generation || day < 1) return false;
            var entry = new FundEntry(ledger.Count + 1, generation, day, FundEntryKind.GenerationReset, -Balance, 0, "세대 교체 초기화");
            Generation = generation;
            Balance = 0;
            IsOpen = false;
            performance = null;
            roster.Clear();
            ledger.Add(entry);
            Revision++;
            return true;
        }

        public RewardSnapshot Snapshot(int ledgerStart = -1)
        {
            int start = ledgerStart < 0 ? Math.Max(0, ledger.Count - 20) : Math.Clamp(ledgerStart, 0, ledger.Count);
            var entries = ledger.GetRange(start, Math.Min(20, ledger.Count - start));
            var segments = new RewardSegment[roster.Count];
            for (int i = 0; i < segments.Length; i++)
            {
                var person = roster[i];
                segments[i] = new RewardSegment(person.Seed.Id, person.Seed.ClientId, person.Seed.Slot,
                    RewardParticipantSeed.CountLimbs(person.Seed.LostParts), RewardParticipantSeed.CountLimbs(person.Parts),
                    person.Dead, person.Connected, person.Amount);
            }
            return new RewardSnapshot(SessionId, Generation, Revision, Balance, performance, IsOpen, rules, segments,
                Latest, ledger.Count, start, entries);
        }

        Participant Find(string participantId) => roster.Find(person => person.Seed.Id == participantId);
    }
}
