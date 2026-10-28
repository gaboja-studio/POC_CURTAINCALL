using System;

namespace CurtainCall.Reward
{
    /// <summary>
    /// 매 이벤트와 정산 때 현재 세팅을 읽는다. 정산 한 번에는 같은 값 묶음을 쓴다.
    /// 기준: 보상 확정 규칙(reward-rules.md) — 단계별 1인 호응 상한과 실제 도착자 1명당 완주 보너스 100/150/200, 절단 1개당 +10.
    /// </summary>
    public readonly struct RewardRules : IEquatable<RewardRules>
    {
        public int Cheer { get; }
        public int Jeer { get; }
        public int Cap { get; }
        /// <summary>실제 도착자 1명에게만 주는 완주 보너스(토큰). 호응도와 별개로 정산에 더한다.</summary>
        public int CompletionBonus { get; }
        public int CutBonus { get; }

        public RewardRules(int cheer, int jeer, int cap, int completionBonus, int cutBonus)
        {
            if (cheer < 0 || jeer < 0 || cap < 0 || completionBonus < 0 || cutBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(cheer), "보상 규칙은 0 이상이어야 합니다.");
            Cheer = cheer;
            Jeer = jeer;
            Cap = cap;
            CompletionBonus = completionBonus;
            CutBonus = cutBonus;
        }

        public static RewardRules Temporary(int difficulty)
        {
            int tier = difficulty switch
            {
                1 => 100, 2 => 150, 3 => 200, _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
            };
            return new(10, 5, tier, tier, 10);
        }

        public bool Equals(RewardRules other) => Cheer == other.Cheer && Jeer == other.Jeer && Cap == other.Cap
            && CompletionBonus == other.CompletionBonus && CutBonus == other.CutBonus;
        public override bool Equals(object obj) => obj is RewardRules other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cheer, Jeer, Cap, CompletionBonus, CutBonus);
    }

    public sealed class RewardParticipantSeed
    {
        public string Id { get; }
        public ulong ClientId { get; }
        public int Slot { get; }
        public int LostParts { get; }
        public bool Dead { get; }

        // 쉬는 시간에 잡혀 끌려온 사람의 보상 박탈은 2026-10-28 사용자 결정으로 폐기했다(새 보상 규칙대로 일반 지급).
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
