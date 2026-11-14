using System;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>열림/파괴 대상에 붙이는 지정 드랍 목록. 빈 목록은 빈 보상. 확률표는 범위 밖.</summary>
    public sealed class ExplorationDrops : MonoBehaviour
    {
        public const string PerformanceTokenId = "performance-token-10";
        // 금액이 다른 공연 토큰(2026-11-13 사용자: 20원·50원·100원). 기존 ID는 50원(설정값)으로 그대로 쓴다.
        public const string SmallTokenId = "performance-token-20", LargeTokenId = "performance-token-100";
        /// <summary>10원짜리 동전(2026-11-13 사용자: 숨겨진 방 1). PerformanceTokenId는 이름과 달리 50원짜리다.</summary>
        public const string CoinTokenId = "performance-token-coin";
        public static bool IsToken(string itemId) => itemId == PerformanceTokenId || itemId == SmallTokenId || itemId == LargeTokenId || itemId == CoinTokenId;
        public static int TokenFunds(string itemId)
        {
            var settings = CurtainCall.Settings.GameSettings.WaitingRoom;
            if (itemId == CoinTokenId) return settings.ExplorationTokenFundsCoin;
            return itemId == SmallTokenId ? settings.ExplorationTokenFundsSmall : itemId == LargeTokenId ? settings.ExplorationTokenFundsLarge : settings.ExplorationTokenFunds;
        }
        public bool exclusiveWeighted;
        [Serializable]
        public sealed class Entry
        {
            public string itemId;
            [Range(1, 99)] public int uidOffset = 70;
            public Transform point;
            [Range(0, 100)] public int chancePercent = 100;
        }
        [Tooltip("기존 단일 보상 대신 이 목록 사용. UID는 같은 세대의 모든 발견품과 겹치지 않게 지정.")]
        public Entry[] entries = Array.Empty<Entry>();
        [Tooltip("보상 미정이면 기존 단일 보상도 지급하지 않고 설정을 기다린다.")]
        [SerializeField] bool pending;
        public bool Pending => pending;
        public void ConfigureOldBoxLoot(int hairpinOffset, int tokenOffset)
        {
            exclusiveWeighted = true; pending = false;
            // 2026-11-13 사용자: 머리핀 20% / 토큰 50원 35% / 토큰 20원 30% / 토큰 100원 15%.
            // 하나만 나오므로 토큰 세 종류는 UID 번호 하나를 같이 쓴다(번호는 1~99뿐이라 상자마다 네 개씩 쓰면 모자란다).
            entries = new[] {
                new Entry { itemId = ItemUses.MagicianHairpin, uidOffset = hairpinOffset, chancePercent = 20 },
                new Entry { itemId = PerformanceTokenId, uidOffset = tokenOffset, chancePercent = 35 },
                new Entry { itemId = SmallTokenId, uidOffset = tokenOffset, chancePercent = 30 },
                new Entry { itemId = LargeTokenId, uidOffset = tokenOffset, chancePercent = 15 }
            };
        }
        public int Choose(int roll)
        {
            int limit = 0;
            for (int i = 0; i < entries.Length; i++) { limit += entries[i].chancePercent; if (roll < limit) return i; }
            // 꽝 없음(2026-11-12 사용자): 확률 합이 100에 못 미치는 나머지는 마지막 보상(공연 토큰)이 받는다. 목록이 비면 보상 없음.
            return entries.Length - 1;
        }
        public void ConfigureEmpty() { entries = Array.Empty<Entry>(); pending = false; exclusiveWeighted = false; }
        public void ConfigurePending() => pending = true;
        public bool TryConfigure(Entry[] rewards, out string error)
        {
            if (!Validate(rewards, out error)) return false;
            entries = rewards != null ? (Entry[])rewards.Clone() : Array.Empty<Entry>();
            exclusiveWeighted = false;
            pending = false;
            return true;
        }
        public bool TryValidate(out string error)
        {
            if (!Validate(entries, out error, exclusiveWeighted)) return false;
            int total = 0;
            if (exclusiveWeighted) foreach (var entry in entries) {
                if (entry.chancePercent < 0 || entry.chancePercent > 100) { error = "드롭 확률 범위 오류"; return false; }
                total += entry.chancePercent;
            }
            if (exclusiveWeighted && total > 100) { error = "드롭 확률 합계가 100%를 초과합니다."; return false; }
            return true;
        }
        // 하나만 나오는 목록(exclusive)은 같은 UID 번호를 여러 항목이 같이 써도 된다.
        static bool Validate(Entry[] rewards, out string error, bool exclusive = false)
        {
            var ids = new System.Collections.Generic.HashSet<int>();
            if (rewards != null) foreach (var reward in rewards)
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.itemId))
                { error = "드랍 아이템이 지정되지 않았습니다."; return false; }
                if (reward.uidOffset < 1 || reward.uidOffset > 99 || (!ids.Add(reward.uidOffset) && !exclusive))
                { error = "드랍 UID는1~99에서 중복 없이 지정해야 합니다."; return false; }
            }
            error = ""; return true;
        }
    }
}
