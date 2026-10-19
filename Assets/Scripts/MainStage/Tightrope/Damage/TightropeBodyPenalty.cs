using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 외줄의 신체 손상 불이익(2026-10-04 PM 임시 규칙). 수치는 외줄 세팅(신체 손상 불이익)에서 읽는다.
    /// - 다리 손상: 공통 기어가기 감속을 포함해 외줄 최소 이동 속도를 유지한다. 점프·캡슐 크기는 공통 규칙을 쓴다.
    /// - 외줄 기본 흔들림에 잃은 다리·팔마다 추가 증가(외줄 배율 × 다리 배율 × 팔 배율).
    /// - 두 다리: 착지 충격 증가(점프·옆줄 이동은 할 수 있지만 균형이 더 크게 흔들린다).
    /// - 팔: 목마 올라타기 가능 거리 감소.
    /// 활성 외줄 코스의 CourseControlSwitcher가 참가자에게 적용하고, 코스를 떠나면 이전 정책으로 돌린다.
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

            if (legs == 1) penalty.MoveSpeed = Mathf.Max(rules.MinimumInjuredMoveSpeed, rules.OneLegMoveSpeed);
            if (legs >= 2)
            {
                float crawl = GameSettings.Base.BodyDamage.CrawlSpeedMultiplier;
                if (crawl > 0f) penalty.MoveSpeed = Mathf.Max(crawl, rules.MinimumInjuredMoveSpeed) / crawl;
            }
            penalty.Sway = GameSettings.Tightrope.RopeMovement.NaturalSwayMultiplier
                * (1f + rules.SwayPerLostLeg * legs) * (1f + rules.SwayPerLostArm * arms);
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
