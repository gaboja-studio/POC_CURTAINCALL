using CurtainCall.Damage;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Survival
{
    /// <summary>탐색은 BodyDamageEffects의 공통 부상 규칙을 사용한다.</summary>
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

        public override BodyPenalty Evaluate(PlayerCondition condition) => BodyPenalty.None;
    }
}
