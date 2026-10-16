using System;
using CurtainCall.Results;

namespace CurtainCall.Reward
{
    // 직렬화 DTO는 전송 직전에 만들고 수신 검증 후 불변 모델로 복사한다.
    [Serializable]
    internal sealed class RewardIdentityData
    {
        public string session;
        public int generation;
        public int day;
        public int index;
        public string trick;
        public PerformanceIdentity Read() => new(session, generation, day, index, trick);
        public static RewardIdentityData From(PerformanceIdentity value) => value == null ? null : new()
        {
            session = value.SessionId, generation = value.Generation, day = value.Day, index = value.Index, trick = value.TrickId
        };
    }

    [Serializable]
    internal sealed class RewardPersonData
    {
        public string id;
        public ulong client;
        public int slot;
        public int start;
        public int end;
        public bool dead;
        public bool connected;
        public string judgement;
        public long segment;
        public long settlement;
        public RewardSegment ReadSegment() => new(id, client, slot, start, end, dead, connected, segment);
        public PerformanceParticipantResult ReadResult() => new(id, client, slot, start, end, dead, connected, judgement, segment, settlement);
        public static RewardPersonData From(RewardSegment value) => new()
        {
            id = value.ParticipantId, client = value.ClientId, slot = value.Slot, start = value.StartingLostCount,
            end = value.LostCount, dead = value.Dead, connected = value.Connected, segment = value.Amount
        };
        public static RewardPersonData From(PerformanceParticipantResult value) => new()
        {
            id = value.ParticipantId, client = value.ClientId, slot = value.Slot, start = value.StartingLostCount,
            end = value.EndingLostCount, dead = value.Dead, connected = value.Connected, segment = value.Segment,
            judgement = value.Judgement, settlement = value.Settlement
        };
    }

    [Serializable]
    internal sealed class RewardResultData
    {
        public RewardIdentityData performance;
        public byte failure;
        public string reason;
        public RewardPersonData[] people;
        public PerformanceResult Read()
        {
            if (people == null || people.Length > 64) throw new ArgumentException("결과 명부 크기 오류");
            var copy = new PerformanceParticipantResult[people.Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = people[i].ReadResult();
            return new PerformanceResult(performance.Read(), (PerformanceFailure)failure, reason, copy);
        }
        public static RewardResultData From(PerformanceResult value)
        {
            if (value == null) return null;
            var people = new RewardPersonData[value.Participants.Count];
            for (int i = 0; i < people.Length; i++) people[i] = RewardPersonData.From(value.Participants[i]);
            return new RewardResultData
            {
                performance = RewardIdentityData.From(value.Performance), failure = (byte)value.Failure, reason = value.EndReason, people = people
            };
        }
    }

    [Serializable]
    internal sealed class RewardLedgerData
    {
        public int number;
        public int generation;
        public int day;
        public byte kind;
        public long amount;
        public long balance;
        public string description;
        public string performance;
        public FundEntry Read() => new(number, generation, day, (FundEntryKind)kind, amount, balance, description, performance);
        public static RewardLedgerData From(FundEntry value) => new()
        {
            number = value.Number, generation = value.Generation, day = value.Day, kind = (byte)value.Kind,
            amount = value.Amount, balance = value.Balance, description = value.Description, performance = value.PerformanceId
        };
    }

    [Serializable]
    internal sealed class RewardStateData
    {
        public string session;
        public int generation;
        public long revision;
        public long balance;
        public RewardIdentityData performance;
        public bool open;
        public int cheer;
        public int jeer;
        public int cap;
        public int survivalBonus;
        public int cutBonus;
        public RewardPersonData[] segments;
        public RewardResultData latest;
        public int ledgerCount;
        public int ledgerStart;
        public RewardLedgerData[] ledger;

        public RewardSnapshot Read()
        {
            if (segments == null || segments.Length > 64 || ledger == null || ledger.Length > 20)
                throw new ArgumentException("공유 상태 크기 오류");
            var people = new RewardSegment[segments.Length];
            var entries = new FundEntry[ledger.Length];
            for (int i = 0; i < people.Length; i++) people[i] = segments[i].ReadSegment();
            for (int i = 0; i < entries.Length; i++) entries[i] = ledger[i].Read();
            return new RewardSnapshot(session, generation, revision, balance, performance?.Read(), open,
                new RewardRules(cheer, jeer, cap, survivalBonus, cutBonus), people, latest?.Read(), ledgerCount, ledgerStart, entries);
        }

        public static RewardStateData From(RewardSnapshot value)
        {
            var people = new RewardPersonData[value.Segments.Count];
            var entries = new RewardLedgerData[value.Ledger.Count];
            for (int i = 0; i < people.Length; i++) people[i] = RewardPersonData.From(value.Segments[i]);
            for (int i = 0; i < entries.Length; i++) entries[i] = RewardLedgerData.From(value.Ledger[i]);
            return new RewardStateData
            {
                session = value.SessionId, generation = value.Generation, revision = value.Revision, balance = value.Balance,
                performance = RewardIdentityData.From(value.Performance), open = value.IsOpen,
                cheer = value.Rules.Cheer, jeer = value.Rules.Jeer, cap = value.Rules.Cap,
                survivalBonus = value.Rules.AllSurvivedBonus, cutBonus = value.Rules.CutBonus,
                segments = people, latest = RewardResultData.From(value.Latest), ledgerCount = value.LedgerCount,
                ledgerStart = value.LedgerStart, ledger = entries
            };
        }
    }

    [Serializable]
    internal sealed class RewardRequestData
    {
        public string session;
        public int generation;
        public long revision;
        public int ledgerStart = -1;
    }
}
