using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>외줄의 기본 균형 배율. 부상 자체는 공통 규칙에서 처리한다.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Body Penalty/Tightrope", fileName = "TightropeBodyPenalty")]
    public sealed class TightropeBodyPenalty : BodyPenaltyPolicy
    {
        public override BodyPenalty Evaluate(PlayerCondition condition)
        {
            var penalty = BodyPenalty.None;
            penalty.Sway = GameSettings.Tightrope.RopeMovement.NaturalSwayMultiplier;
            return penalty;
        }
    }
}
