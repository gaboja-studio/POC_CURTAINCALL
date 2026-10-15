using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Survival
{
    /// <summary>
    /// 사망·잡힘 결과가 달라지는 장소 구분. 규칙: Harness/Project/Decisions/injury-rules.md #3·#5
    /// 온라인으로 보내지 않지만 다른 기능이 숫자로 저장할 수 있으니 값은 끝에만 추가한다.
    /// </summary>
    public enum SurvivalPlace : byte
    {
        /// <summary>공연(묘기 중). 사망 = 추락(<see cref="PlayerState.Fallen"/>), 공연 끝까지 관전 후 다음 시간대에 외팔 부활. 잡힘 없음.</summary>
        Performance = 0,
        /// <summary>탐색(대기실·백스테이지, 쉬는 시간). 사망 = 추락. 잡히면 외팔 상태까지 절단, 이미 외팔이면 사망.</summary>
        Exploration = 1,
        /// <summary>탈출로. 사망·잡힘 모두 리타이어(<see cref="PlayerState.Retired"/>).</summary>
        EscapeRoute = 2,
    }

    /// <summary>잡혔을 때 일어난 일.</summary>
    public enum CatchOutcome : byte
    {
        /// <summary>아무 일 없음(잡힘이 없는 장소·이미 사망 등으로 거절).</summary>
        None = 0,
        /// <summary>외팔 상태가 될 때까지 잘림(백스테이지). 무대로 끌려가는 것은 공간 연결(032)이 한다.</summary>
        CutToOneArm = 1,
        /// <summary>이미 외팔이라 사망(백스테이지).</summary>
        Died = 2,
        /// <summary>리타이어(탈출로).</summary>
        Retired = 3,
    }

    /// <summary>
    /// 장소별 사망·잡힘 규칙과 기물 소지 칸 수. 판정은 호스트가 <see cref="NetworkPlayer"/>의 Server 함수로 한다.
    /// 지금 장소(<see cref="ServerPlace"/>)는 호스트에서 공간 연결(032)이 정한다. 정하지 않으면 공연(외줄과 같은 동작).
    /// 묘기 안에서 "지금 다칠 수 있나"는 이와 별개로 <see cref="NetworkPlayer.ServerCanChangeState"/>(묘기가 끼움)가 함께 본다.
    /// </summary>
    public static class SurvivalRules
    {
        /// <summary>호스트: 지금 장소. 사망·잡힘 결과가 이 값으로 갈린다.</summary>
        public static SurvivalPlace ServerPlace { get; set; } = SurvivalPlace.Performance;

        /// <summary>그 장소에서 죽으면 되는 상태. 탈출로 = 리타이어, 그 밖 = 추락(사망).</summary>
        public static PlayerState DeathState(SurvivalPlace place) =>
            place == SurvivalPlace.EscapeRoute ? PlayerState.Retired : PlayerState.Fallen;

        /// <summary>그 장소에서 잃은 팔다리 수가 <paramref name="lostCount"/>인 사람이 잡히면 일어날 일.</summary>
        public static CatchOutcome ResolveCatch(SurvivalPlace place, int lostCount) => place switch
        {
            SurvivalPlace.Exploration => lostCount >= PlayerCondition.OneArmLostCount ? CatchOutcome.Died : CatchOutcome.CutToOneArm,
            SurvivalPlace.EscapeRoute => CatchOutcome.Retired,
            _ => CatchOutcome.None,
        };

        /// <summary>
        /// 지닐 수 있는 기물 개수. 팔 2개 = 기본 2, 팔 1개 = 기본 1, 배낭 +2(공통 세팅 · 기물 소지). 팔이 없으면 0.
        /// 결정: injury-rules.md #8
        /// </summary>
        public static int ItemSlots(int armCount, bool hasBackpack)
        {
            if (armCount <= 0) return 0;
            var carry = GameSettings.Base.ItemCarry;
            int slots = armCount >= 2 ? carry.TwoArmSlots : carry.OneArmSlots;
            return slots + (hasBackpack ? carry.BackpackBonus : 0);
        }

        /// <summary>이 몸 상태로 지닐 수 있는 기물 개수. 몸 상태가 없으면 팔 2개로 본다.</summary>
        public static int ItemSlots(PlayerCondition condition, bool hasBackpack) =>
            ItemSlots(condition != null ? condition.ArmCount : 2, hasBackpack);

        // 도메인 리로드 없이 플레이를 다시 시작해도 공연으로 돌아가게 한다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ServerPlace = SurvivalPlace.Performance;
    }
}
