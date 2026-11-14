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
        float Reach(Vector3 feet, Component actor = null);
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

    /// <summary>
    /// 맨몸으로도 칠 수 있는 대상(통로 기믹 CC-255, 2026-11-10 사용자). 맨몸 <see cref="BareHits"/>대 또는 도구 <see cref="ToolHits"/>대에 열리고,
    /// 섞어 치면 비율로 합산한다(<see cref="StrikeProgress"/>). 진행 기록 값은 타격 수가 아니라 진행 단위다.
    /// 도구 사용 횟수는 지금처럼 도구로 친 타격으로 열렸을 때만 1 줄인다.
    /// </summary>
    public interface IBareHandTarget : IExplorationTarget
    {
        bool AllowsBareHands { get; }
        int BareHits { get; }
        int ToolHits { get; }
        /// <summary>요청자에게 보낼 결과 문구. <paramref name="done"/>/<paramref name="total"/>은 친 방식 기준 타격 수.</summary>
        string DescribeStrike(bool opened, int done, int total, bool withTool);
    }

    public static class ExplorationTargets
    {
        /// <summary>맨몸으로 칠 수 있는 대상인가.</summary>
        public static bool AllowsBareHands(IExplorationTarget t) => t is IBareHandTarget { AllowsBareHands: true };

        /// <summary>지금 방식(맨몸/도구) 기준 친 수와 필요한 수(F 안내용, 모든 화면).</summary>
        public static (int done, int total) StrikeCount(IBareHandTarget t, bool withTool)
        {
            int units = ExplorationState.Instance != null ? ExplorationState.Instance.Hits(t.TargetId) : 0;
            return (StrikeProgress.Done(units, t.BareHits, t.ToolHits, withTool), withTool ? t.ToolHits : t.BareHits);
        }

        /// <summary>맨몸 부수기 전체 진행(0~1, F 안내 게이지용). <paramref name="partial"/>은 지금 채우는 한 대 분량의 비율.</summary>
        public static float BareProgress(IBareHandTarget t, float partial)
        {
            int units = ExplorationState.Instance != null ? ExplorationState.Instance.Hits(t.TargetId) : 0;
            float step = StrikeProgress.Step(t.BareHits, t.ToolHits, false);
            return UnityEngine.Mathf.Clamp01((units + UnityEngine.Mathf.Clamp01(partial) * step) / StrikeProgress.Total(t.BareHits, t.ToolHits));
        }

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

        /// <summary>
        /// 도구 이름 대신 보여 줄 힌트(2026-11-01 사용자). 문구 파일 target.&lt;대상 id&gt;.hint → item.&lt;도구 id&gt;.hint 순서로 찾고,
        /// 둘 다 없으면 기본 힌트. 힌트 안에서 {tool}(도구 이름)을 쓸 수 있다.
        /// </summary>
        public static string Hint(IExplorationTarget t)
        {
            string key = "target." + t.TargetId + ".hint";
            if (!WaitingRoomText.Has(key)) key = "item." + t.ToolId + ".hint";
            return WaitingRoomText.Format(key, "알맞은 도구가 필요할 것 같다", ("tool", t.ToolName));
        }

        /// <summary>지금 내 손에 든 기물이 이 도구인가(F 안내용, 모든 화면).</summary>
        public static bool LocalHolds(string toolId)
        {
            var carry = PropCarry.Instance;
            if (carry == null) return false;
            var item = carry.InHand(carry.LocalClientId);
            return item.Uid != 0 && item.ItemId == toolId && item.Uses != 0;
        }
    }
}
