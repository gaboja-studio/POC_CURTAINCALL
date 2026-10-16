using System;

namespace CurtainCall.Reward
{
    /// <summary>매 이벤트와 정산 때 현재 세팅을 읽는다. 정산 한 번에는 같은 값 묶음을 쓴다.</summary>
    public readonly struct RewardRules : IEquatable<RewardRules>
    {
        public int Cheer { get; }
        public int Jeer { get; }
        public int Cap { get; }
        public int AllSurvivedBonus { get; }
        public int CutBonus { get; }

        public RewardRules(int cheer, int jeer, int cap, int allSurvivedBonus, int cutBonus)
        {
            if (cheer < 0 || jeer < 0 || cap < 0 || allSurvivedBonus < 0 || cutBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(cheer), "보상 규칙은 0 이상이어야 합니다.");
            Cheer = cheer;
            Jeer = jeer;
            Cap = cap;
            AllSurvivedBonus = allSurvivedBonus;
            CutBonus = cutBonus;
        }

        public static RewardRules Temporary(int difficulty) => new(10, 5, difficulty switch
        {
            1 => 100, 2 => 150, 3 => 200, _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
        }, 0, 0);

        public bool Equals(RewardRules other) => Cheer == other.Cheer && Jeer == other.Jeer && Cap == other.Cap
            && AllSurvivedBonus == other.AllSurvivedBonus && CutBonus == other.CutBonus;
        public override bool Equals(object obj) => obj is RewardRules other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cheer, Jeer, Cap, AllSurvivedBonus, CutBonus);
    }

    public sealed class RewardParticipantSeed
    {
        public string Id { get; }
        public ulong ClientId { get; }
        public int Slot { get; }
        public int LostParts { get; }
        public bool Dead { get; }

        public RewardParticipantSeed(string id, ulong clientId, int slot, int lostParts = 0, bool dead = false)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || slot < -1 || slot > 63 || (lostParts & ~15) != 0)
                throw new ArgumentException("참가 명부가 올바르지 않습니다.");
            Id = id;
            ClientId = clientId;
            Slot = slot;
            LostParts = lostParts;
            Dead = dead || lostParts == 15;
        }

        public static int CountLimbs(int parts)
        {
            int count = 0;
            for (int bit = 1; bit <= 8; bit <<= 1) if ((parts & bit) != 0) count++;
            return count;
        }
    }
}
