using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Survival
{
    /// <summary>
    /// 탐색 중 부상 패널티(대기실·백스테이지·탈출로 묘기 밖). 부상 기획 5-2, 수치는 대기실 담당 미정이라 공통 세팅(신체 손상)의 임시값.
    /// - 다리 하나: 이동 느려짐. 두 다리는 게임 공통 기어가기 속도를 쓴다(여기서 더 줄이지 않음).
    /// - 팔: 손 닿는 거리 감소.
    /// 점프 높이 감소는 탐색·공연 공통이라 <see cref="BodyDamageEffects"/>가 직접 한다.
    /// 공간에 들어갈 때 <c>GetComponent&lt;BodyDamageEffects&gt;().SetPolicy(ExplorationBodyPenalty.Shared)</c>로 끼운다.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Body Penalty/Exploration", fileName = "ExplorationBodyPenalty")]
    public sealed class ExplorationBodyPenalty : BodyPenaltyPolicy
    {
        static ExplorationBodyPenalty shared;

        /// <summary>에셋 없이 쓰는 공용 인스턴스(정책은 상태가 없어 모두 함께 쓴다).</summary>
        public static ExplorationBodyPenalty Shared
        {
            get
            {
                if (shared != null) return shared;
                shared = CreateInstance<ExplorationBodyPenalty>();
                shared.name = "ExplorationBodyPenalty (공용)";
                return shared;
            }
        }

        public override BodyPenalty Evaluate(PlayerCondition condition)
        {
            var penalty = BodyPenalty.None;
            if (condition == null) return penalty;

            var rules = GameSettings.Base.BodyDamage;
            if (condition.LostLegCount == 1) penalty.MoveSpeed = rules.ExploreOneLegMoveSpeed;
            if (condition.LostArmCount >= 1) penalty.Reach = rules.ExploreOneArmReach;
            return penalty;
        }
    }
}
