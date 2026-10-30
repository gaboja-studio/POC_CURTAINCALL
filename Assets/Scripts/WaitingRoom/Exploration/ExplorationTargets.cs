using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 탐색 도구를 쓰는 대상(Task-079). F 한 번이 한 번의 시도이고, 판정 화면(<see cref="ExplorationState"/>)이 성공을 정한다.
    /// 성공(대상 파괴·개방·새 발견)한 때에만 쓴 도구의 사용 횟수를 1 줄인다(item-durability.md).
    /// </summary>
    public interface IExplorationTarget
    {
        string TargetId { get; }
        /// <summary>손에 들어야 하는 기물 ID(ItemUses).</summary>
        string ToolId { get; }
        string ToolName { get; }
        string Label { get; }
        /// <summary>내 발 위치에서 닿는 거리. 닿지 않으면 음수.</summary>
        float Reach(Vector3 feet);
        /// <summary>판정 화면: <paramref name="attemptNumber"/>번째 시도(1부터)가 성공하는가.</summary>
        bool Resolve(int attemptNumber, System.Random rng);
        /// <summary>요청자에게 보낼 결과 문구.</summary>
        string Describe(bool success, int attemptNumber);
        /// <summary>열린 때 그 자리에 나오는 발견품(없으면 빈 문자열)과 놓을 곳.</summary>
        string RewardItemId { get; }
        Vector3 RewardPoint { get; }
        /// <summary>발견품 UID 구분 번호(1~99, 발견품 자리와 겹치지 않게).</summary>
        int RewardUidOffset { get; }
    }

    public static class ExplorationTargets
    {
        static readonly List<IExplorationTarget> all = new();

        public static IReadOnlyList<IExplorationTarget> All => all;

        public static void Add(IExplorationTarget t) { if (!all.Contains(t)) all.Add(t); }
        public static void Remove(IExplorationTarget t) => all.Remove(t);

        public static IExplorationTarget Find(string id)
        {
            foreach (var t in all) if (t != null && t.TargetId == id) return t;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();

        /// <summary>지금 내 손에 든 기물이 이 도구인가(F 안내용, 모든 화면).</summary>
        public static bool LocalHolds(string toolId)
        {
            var carry = PropCarry.Instance;
            if (carry == null) return false;
            var item = carry.Selected(carry.LocalClientId);
            return item.Uid != 0 && item.ItemId == toolId && item.Uses != 0;
        }
    }
}
