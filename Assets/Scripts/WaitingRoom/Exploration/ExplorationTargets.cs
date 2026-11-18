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

    /// <summary>내구도를 가진 부수기 대상(2026-11-16 사용자: 누르는 시간 = 내구도 ÷ 손·도구의 힘).</summary>
    public interface IStrikeDurability
    {
        /// <summary>대상 내구도. 0 이하면 옛 횟수에서 환산(<see cref="ExplorationTargets.Durability"/>).</summary>
        float Durability { get; }
    }

    public static class ExplorationTargets
    {
        public static bool ToolsBlocked => CurtainCall.Settings.GameSettings.Base.CarryDevices.BlockExplorationToolsDuringPerformance
            && (CurtainCall.Campaign.CampaignRun.Instance?.State.Phase == CurtainCall.Campaign.CampaignPhase.Performance
                // 대기실 연습실의 자유 연습 코스도 '진행 중인 외줄'로 잡혀 대기실 내내 도구가 막혔다(2026-11-15 사용자: 환풍구·녹슨 벽이 더 안 부서짐). 대기실 씬의 코스는 공연으로 보지 않는다.
                || CurtainCall.Tightrope.TightropeRun.Current is { PracticeMode: false, TraversalOnly: false, State: CurtainCall.Tightrope.TightropeRunState.Running } rope
                    && rope.gameObject.scene.name != CurtainCall.Settings.GameSettings.Base.SpaceTransition.WaitingRoomScene
                || CurtainCall.HumanCannon.HumanCannonSystem.Current is { PracticeMode: false } cannon
                    && cannon.gameObject.scene.name == CurtainCall.Settings.GameSettings.Base.SpaceTransition.HumanCannonScene);
        /// <summary>맨몸으로 칠 수 있는 대상인가.</summary>
        public static bool AllowsBareHands(IExplorationTarget t) => t is IBareHandTarget { AllowsBareHands: true };

        /// <summary>지금 방식(맨몸/도구) 기준 친 수와 필요한 수(F 안내용, 모든 화면).</summary>
        public static (int done, int total) StrikeCount(IBareHandTarget t, bool withTool)
        {
            int units = ExplorationState.Instance != null ? ExplorationState.Instance.Hits(t.TargetId) : 0;
            return (StrikeProgress.Done(units, t.BareHits, t.ToolHits, withTool), withTool ? t.ToolHits : t.BareHits);
        }

        /// <summary>맨몸 부수기 전체 진행(0~1, F 안내 게이지용). <paramref name="partial"/>은 지금 채우는 한 대 분량의 비율.</summary>
        public static float BareProgress(IBareHandTarget t, float partial) => StrikeHoldProgress(t, partial, false);

        /// <summary>부수기 전체 진행(0~1). <paramref name="withTool"/>이면 말뚝 뽑개 한 대 분량으로 센다.</summary>
        public static float StrikeHoldProgress(IBareHandTarget t, float partial, bool withTool)
        {
            int units = ExplorationState.Instance != null ? ExplorationState.Instance.Hits(t.TargetId) : 0;
            // 맨몸 불가 대상은 판정 화면이 시도 수(=뽑개 타격 수)를 그대로 센다.
            int bare = t.AllowsBareHands ? t.BareHits : 0;
            float step = StrikeProgress.Step(bare, t.ToolHits, withTool);
            return UnityEngine.Mathf.Clamp01((units + UnityEngine.Mathf.Clamp01(partial) * step) / StrikeProgress.Total(bare, t.ToolHits));
        }

        /// <summary>
        /// 말뚝 뽑개로 F를 꾹 눌러 부수는 대상인가(2026-11-16 사용자: 벽·상자 연타 → F 홀드). 부서질 때 뽑개 내구도 1 감소는 그대로.
        /// 판자·상자(뜯기)·금 간 벽·부술 수 있는 문이 해당한다. 머리핀 자물쇠·열쇠 문은 그대로 짧게 누른다.
        /// </summary>
        public static bool UsesToolHold(IShopTarget t) => t is IBareHandTarget hand && hand.ToolId == ItemUses.StakePuller
            && (t is not ExplorationGate gate || gate.IsBreakable);

        /// <summary>내구도를 따로 정하지 않은 옛 대상: 맨몸 횟수(없으면 도구 횟수) × 이 값(옛 "맨몸 한 대 1.5초"와 같은 시간).</summary>
        public const float LegacyDurabilityPerHit = 1.5f;

        /// <summary>
        /// 부수기 대상의 내구도(2026-11-16 사용자). 누르는 시간 = 내구도 ÷ 힘(손 또는 뽑개).
        /// 대상 컴포넌트의 "내구도" 값이 0이면 옛 횟수에서 환산한다.
        /// </summary>
        public static float Durability(IBareHandTarget t)
        {
            if (t is IStrikeDurability { Durability: > 0f } own) return own.Durability;
            return UnityEngine.Mathf.Max(1, t.AllowsBareHands ? t.BareHits : t.ToolHits) * LegacyDurabilityPerHit;
        }

        /// <summary>
        /// 1초 동안 깎는 내구도(힘). 맨몸: 설정 "맨몸 부수기 힘". 뽑개: 기물 목록(ItemCatalog) 천막 말뚝 뽑개의 효과 수치(effect).
        /// 0이면 그 방식은 누르기로 부수지 않는다(짧게 눌러 친다).
        /// </summary>
        public static float StrikePower(bool withTool) => withTool
            ? ItemCatalog.Get(ItemUses.StakePuller) is { effect: > 0f } stake ? stake.effect : 0f
            : CurtainCall.Settings.GameSettings.WaitingRoom.BareHandPower;

        /// <summary>대상을 다 부수는 데 F를 누르고 있어야 하는 전체 시간(초) = 내구도 ÷ 힘. 힘이 0이면 0(누르기 부수기 없음).</summary>
        public static float StrikeHoldTotalSeconds(IBareHandTarget t, bool withTool)
        {
            float power = StrikePower(withTool);
            return power > 0f ? Durability(t) / power : 0f;
        }

        /// <summary>
        /// 진행 한 단계(판정 화면에 한 번 기록하는 한 대)를 채우는 시간 = 전체 시간 ÷ 그 방식의 단계 수(맨몸/도구 횟수).
        /// 단계 수는 금이 늘어나는 모습·섞어 치기 비율에만 쓰고, 전체 시간은 내구도와 힘으로만 정해진다.
        /// </summary>
        public static float StrikeHoldSeconds(IBareHandTarget t, bool withTool)
        {
            int steps = UnityEngine.Mathf.Max(1, withTool ? t.ToolHits : t.BareHits);
            return StrikeHoldTotalSeconds(t, withTool) / steps;
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
            if (ToolsBlocked) return false;
            var carry = PropCarry.Instance;
            if (carry == null) return false;
            var item = carry.InHand(carry.LocalClientId);
            return item.Uid != 0 && ItemUses.ToolMatches(toolId, item.ItemId) && item.Uses != 0;
        }
    }
}
