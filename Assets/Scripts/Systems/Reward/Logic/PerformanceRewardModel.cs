using System;
using System.Collections.Generic;
using CurtainCall.Results;

namespace CurtainCall.Reward
{
    /// <summary>
    /// 호스트 전용 판정. 네트워크·Campaign·Unity 수명은 공개 진입점에서 연결한다.
    /// 기준: 보상 확정 규칙(reward-rules.md) — 완주·사망·이탈 시 호응도 고정, 개인별 관심 감소(유예 뒤 초당 감소),
    /// 생존 호응×2·사망 호응×½×2·실제 도착자만 완주 보너스, 실패도 호응 보상 지급.
    /// </summary>
    public sealed class PerformanceRewardModel
    {
        sealed class Participant
        {
            public RewardParticipantSeed Seed;
            public int Parts;
            public int AwardedParts;
            public bool Dead;
            public bool Connected = true;
            public bool Arrived;
            public long Amount;
            public long ReactionSequence;
            public int ReactionDelta, ReactionAmount;
            public double? MaximumDistance;
            public double? FinalDistance;
            /// <summary>관심 감소가 시작되는 시각. NaN이면 다음 <see cref="Tick"/>에서 정한다.</summary>
            public double GraceUntil = double.NaN;
            public double NextDecay = double.NaN;
            public bool Frozen => Dead || !Connected || Arrived;
        }

        readonly List<Participant> roster = new();
        readonly HashSet<string> begun = new();
        readonly List<FundEntry> ledger = new();
        readonly List<PerformanceResult> history = new();
        PerformanceIdentity performance;
        RewardRules rules;
        double? elapsedSeconds, courseLength;
        double clock = double.NaN, grace;

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
            clock = double.NaN;
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
            {
                person.FinalDistance = Math.Clamp(distance, 0, length);
                person.MaximumDistance = Math.Max(person.MaximumDistance ?? 0, Math.Clamp(distance, 0, length));
            }
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

        /// <summary>
        /// 호응 변화. 환호(양수)는 본인 관심 감소 유예를 처음부터 다시 센다. 시간 끌기 야유처럼 감점이어도 유예를 다시
        /// 세는 경우는 <paramref name="resetInterest"/>를 켠다. 완주·사망·이탈한 사람의 호응도는 고정이다.
        /// </summary>
        public bool React(string participantId, int delta, RewardRules currentRules, bool resetInterest = false)
        {
            if (!IsOpen) return false;
            var person = Find(participantId);
            if (person == null || person.Frozen) return false;
            ApplyRules(currentRules);
            if (delta < 0 && person.Amount == 0) return false; // 0점의 야유는 사건·유예 갱신도 만들지 않는다.
            if (delta > 0 || resetInterest) RestartInterest(person);
            long amount = Math.Clamp(person.Amount + (long)delta, 0, rules.Cap);
            if (delta != 0)
            {
                RecordReaction(person, delta, (int)(amount - person.Amount));
                Revision++; // 상한에서도 실제 성공 사건을 공유하되 점수 변경 반환값은 유지한다.
            }
            if (amount == person.Amount) return false;
            person.Amount = amount;
            return true;
        }

        static void RecordReaction(Participant person, int delta, int amount)
        { person.ReactionSequence++; person.ReactionDelta = delta; person.ReactionAmount = amount; }

        /// <summary>실제 도착. 이후 호응도는 고정되고 관심 감소도 멈춘다. 정산에서 완주 보너스를 받는다.</summary>
        public bool MarkArrived(string participantId)
        {
            if (!IsOpen) return false;
            var person = Find(participantId);
            if (person == null || person.Frozen) return false;
            person.Arrived = true;
            Revision++;
            return true;
        }

        /// <summary>
        /// 개인별 관심 감소. 공연 시작(첫 호출)과 마지막 환호부터 <paramref name="graceSeconds"/>초 유예 뒤
        /// 1초마다 <paramref name="decayPerSecond"/>씩 줄인다(최소 0). 고정된 사람은 줄지 않는다.
        /// </summary>
        public bool Tick(double now, double graceSeconds, int decayPerSecond)
        {
            if (!IsOpen || !double.IsFinite(now) || !double.IsFinite(graceSeconds) || graceSeconds < 0) return false;
            if (!double.IsNaN(clock) && now < clock) return false;
            clock = now;
            grace = graceSeconds;
            bool changed = false;
            foreach (var person in roster)
            {
                if (person.Frozen) continue;
                if (double.IsNaN(person.GraceUntil)) { RestartInterest(person); continue; }
                if (decayPerSecond <= 0 || now < person.NextDecay) continue;
                long steps = (long)Math.Floor(now - person.NextDecay) + 1;
                person.NextDecay += steps;
                long amount = Math.Max(0, person.Amount - Math.Min(person.Amount, steps * decayPerSecond));
                if (amount == person.Amount) continue;
                person.Amount = amount;
                changed = true;
            }
            if (changed) Revision++;
            return changed;
        }

        void RestartInterest(Participant person)
        {
            person.GraceUntil = double.IsNaN(clock) ? double.NaN : clock + grace;
            person.NextDecay = person.GraceUntil + 1;
        }

        /// <summary>
        /// 같은 부위의 재관찰·복구·재절단은 중복 가산하지 않는다. 절단 보너스를 먼저 더한 뒤 사망·이탈을 확정한다.
        /// 사망자는 사망 전 호응도를 유지하고, 이탈자는 0이 된다.
        /// </summary>
        public bool Observe(string participantId, int lostParts, bool dead, bool connected, RewardRules currentRules, bool arrived = false)
        {
            if (!IsOpen || (lostParts & ~15) != 0) return false;
            var person = Find(participantId);
            if (person == null) return false;
            ApplyRules(currentRules);
            bool nextDead = person.Dead || dead || lostParts == 15;
            bool nextConnected = person.Connected && connected;
            bool nextArrived = person.Arrived || (arrived && !nextDead && nextConnected);
            int newlyCut = RewardParticipantSeed.CountLimbs(lostParts & ~person.AwardedParts);
            long amount = person.Frozen ? person.Amount : Math.Min(rules.Cap, person.Amount + (long)newlyCut * rules.CutBonus);
            if (!nextConnected) amount = 0;
            if (!person.Frozen && nextConnected && newlyCut > 0 && rules.CutBonus > 0)
            {
                RecordReaction(person, (int)Math.Min(int.MaxValue, (long)newlyCut * rules.CutBonus), (int)(amount - person.Amount));
                RestartInterest(person);
            }
            bool changed = person.Parts != lostParts || person.Dead != nextDead || person.Connected != nextConnected
                || person.Arrived != nextArrived || person.Amount != amount;
            person.Parts = lostParts;
            person.AwardedParts |= lostParts;
            person.Dead = nextDead;
            person.Connected = nextConnected;
            person.Arrived = nextArrived;
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
            var people = new PerformanceParticipantResult[roster.Count];
            for (int i = 0; i < people.Length; i++)
            {
                var person = roster[i];
                string supplied = null;
                bool hasJudgement = judgements != null && judgements.TryGetValue(person.Seed.Id, out supplied);
                bool alive = !person.Dead && person.Connected;
                bool arrived = alive && (person.Arrived || supplied == "Arrived");
                long segment = person.Connected ? Math.Min(person.Amount, currentRules.Cap) : 0;
                long bonus = arrived ? currentRules.CompletionBonus : 0;
                long settlement = checked(PerformanceParticipantResult.CheerPayout(segment, person.Dead, person.Connected) + bonus);
                string judgement = hasJudgement ? supplied
                    : person.Dead ? "Dead" : !person.Connected ? "Disconnected" : arrived ? "Arrived" : "Survived";
                people[i] = new PerformanceParticipantResult(person.Seed.Id, person.Seed.ClientId, person.Seed.Slot,
                    RewardParticipantSeed.CountLimbs(person.Seed.LostParts), RewardParticipantSeed.CountLimbs(person.Parts),
                    person.Dead, person.Connected, judgement, segment, settlement, person.MaximumDistance, bonus, person.FinalDistance);
            }
            var next = new PerformanceResult(performance, failure, endReason, people, elapsedSeconds, courseLength);
            if (next.Income > long.MaxValue - Balance) return false;
            long balance = Balance + next.Income;
            var entry = new FundEntry(ledger.Count + 1, Generation, performance.Day, FundEntryKind.PerformanceIncome,
                next.Income, balance, next.Succeeded ? "공연 성공 정산" : "공연 실패 정산", performance.Id);
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
                    person.Dead, person.Connected, person.Amount, person.ReactionSequence, person.ReactionDelta, person.ReactionAmount);
            }
            return new RewardSnapshot(SessionId, Generation, Revision, Balance, performance, IsOpen, rules, segments,
                Latest, ledger.Count, start, entries);
        }

        Participant Find(string participantId) => roster.Find(person => person.Seed.Id == participantId);
    }
}
