using System;
using System.Collections.Generic;
using CurtainCall.Results;

namespace CurtainCall.Reward
{
    public enum FundEntryKind : byte { PerformanceIncome, Expense, GenerationReset }

    public sealed class FundEntry
    {
        public int Number { get; }
        public int Generation { get; }
        public int Day { get; }
        public FundEntryKind Kind { get; }
        public long Amount { get; }
        public long Balance { get; }
        public string Description { get; }
        public string PerformanceId { get; }

        public FundEntry(int number, int generation, int day, FundEntryKind kind, long amount, long balance,
            string description, string performanceId = "")
        {
            if (number < 1 || generation < 1 || day < 1 || kind > FundEntryKind.GenerationReset || balance < 0
                || string.IsNullOrWhiteSpace(description) || description.Length > 100 || performanceId == null
                || performanceId.Length > 100 || (kind == FundEntryKind.PerformanceIncome && amount < 0)
                || (kind == FundEntryKind.Expense && amount >= 0) || (kind == FundEntryKind.GenerationReset && (amount > 0 || balance != 0)))
                throw new ArgumentException("자금 장부가 올바르지 않습니다.");
            Number = number;
            Generation = generation;
            Day = day;
            Kind = kind;
            Amount = amount;
            Balance = balance;
            Description = description;
            PerformanceId = performanceId;
        }
    }

    public sealed class RewardSegment
    {
        public string ParticipantId { get; }
        public ulong ClientId { get; }
        public int Slot { get; }
        public int StartingLostCount { get; }
        public int LostCount { get; }
        public bool Dead { get; }
        public bool Connected { get; }
        public long Amount { get; }

        public RewardSegment(string participantId, ulong clientId, int slot, int startingLostCount, int lostCount,
            bool dead, bool connected, long amount)
        {
            if (string.IsNullOrWhiteSpace(participantId) || participantId.Length > 100 || slot < -1 || slot > 63
                || startingLostCount < 0 || startingLostCount > 4 || lostCount < 0 || lostCount > 4 || amount < 0
                || ((!connected || dead) && amount != 0)) throw new ArgumentException("호응 구간이 올바르지 않습니다.");
            ParticipantId = participantId;
            ClientId = clientId;
            Slot = slot;
            StartingLostCount = startingLostCount;
            LostCount = lostCount;
            Dead = dead;
            Connected = connected;
            Amount = amount;
        }
    }

    /// <summary>결과·호응·잔액·장부 페이지를 한 revision으로 적용하는 읽기 전용 상태.</summary>
    public sealed class RewardSnapshot
    {
        public string SessionId { get; }
        public int Generation { get; }
        public long Revision { get; }
        public long Balance { get; }
        public PerformanceIdentity Performance { get; }
        public bool IsOpen { get; }
        public RewardRules Rules { get; }
        public IReadOnlyList<RewardSegment> Segments { get; }
        public PerformanceResult Latest { get; }
        public int LedgerCount { get; }
        public int LedgerStart { get; }
        public IReadOnlyList<FundEntry> Ledger { get; }

        public RewardSnapshot(string sessionId, int generation, long revision, long balance, PerformanceIdentity performance,
            bool isOpen, RewardRules rules, IReadOnlyList<RewardSegment> segments, PerformanceResult latest,
            int ledgerCount, int ledgerStart, IReadOnlyList<FundEntry> ledger)
        {
            if (!Guid.TryParseExact(sessionId, "N", out _) || generation < 1 || revision < 1 || balance < 0
                || segments == null || segments.Count > 64 || ledger == null || ledger.Count > 20
                || ledgerCount < 0 || ledgerStart < 0 || ledgerStart > ledgerCount || ledger.Count > ledgerCount - ledgerStart
                || (isOpen && performance == null) || (performance == null && segments.Count != 0)
                || (performance != null && (performance.SessionId != sessionId || performance.Generation != generation))
                || (latest != null && (latest.Performance.SessionId != sessionId || latest.Performance.Generation > generation
                    || (isOpen && latest.Performance.Id == performance.Id))))
                throw new ArgumentException("공유 상태가 올바르지 않습니다.");
            var copy = new RewardSegment[segments.Count];
            var keys = new HashSet<string>();
            for (int i = 0; i < copy.Length; i++)
            {
                var segment = segments[i];
                if (segment == null || !keys.Add(segment.ParticipantId) || segment.Amount > rules.Cap)
                    throw new ArgumentException("공유 명부가 올바르지 않습니다.");
                copy[i] = segment;
            }
            var entries = new FundEntry[ledger.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = ledger[i];
                if (entry == null || entry.Number != ledgerStart + i + 1 || entry.Generation > generation
                    || (i > 0 && checked(entries[i - 1].Balance + entry.Amount) != entry.Balance))
                    throw new ArgumentException("장부 페이지가 올바르지 않습니다.");
                entries[i] = entry;
            }
            if (entries.Length > 0 && ledgerStart + entries.Length == ledgerCount && entries[^1].Balance != balance)
                throw new ArgumentException("장부와 잔액이 다릅니다.");
            SessionId = sessionId;
            Generation = generation;
            Revision = revision;
            Balance = balance;
            Performance = performance;
            IsOpen = isOpen;
            Rules = rules;
            Segments = Array.AsReadOnly(copy);
            Latest = latest;
            LedgerCount = ledgerCount;
            LedgerStart = ledgerStart;
            Ledger = Array.AsReadOnly(entries);
        }

        public bool IsNewerThan(RewardSnapshot previous) => previous == null
            || (SessionId == previous.SessionId && Generation >= previous.Generation && Revision > previous.Revision);
    }
}
