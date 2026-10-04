using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 외줄의 신체 손상 불이익(2026-10-04 PM 임시 규칙). 수치는 외줄 세팅(신체 손상 불이익)에서 읽는다.
    /// - 다리 하나: 이동 느려짐. 두 다리는 게임 공통 기어가기 속도를 쓴다(여기서 더 줄이지 않음).
    /// - 잃은 다리·팔마다 자연 흔들림 증가(다리 쪽 배율 × 팔 쪽 배율).
    /// - 두 다리: 착지 충격 증가(점프·옆줄 이동은 할 수 있지만 균형이 더 크게 흔들린다).
    /// - 팔: 목마 올라타기 가능 거리 감소.
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Body Penalty/Tightrope", fileName = "TightropeBodyPenalty")]
    public sealed class TightropeBodyPenalty : BodyPenaltyPolicy
    {
        public override BodyPenalty Evaluate(PlayerCondition condition)
        {
            var penalty = BodyPenalty.None;
            if (condition == null) return penalty;

            var rules = GameSettings.Tightrope.BodyPenalty;
            int legs = condition.LostLegCount;
            int arms = condition.LostArmCount;

            if (legs == 1) penalty.MoveSpeed = rules.OneLegMoveSpeed;
            penalty.Sway = (1f + rules.SwayPerLostLeg * legs) * (1f + rules.SwayPerLostArm * arms);
            if (legs >= 2)
            {
                penalty.LandingShock = rules.LeglessLandingShock;
                penalty.LaneLandingShock = rules.LeglessLaneLandingShock;
            }
            penalty.Reach = Mathf.Max(0f, 1f - rules.ReachLossPerArm * arms);
            return penalty;
        }
    }
}
