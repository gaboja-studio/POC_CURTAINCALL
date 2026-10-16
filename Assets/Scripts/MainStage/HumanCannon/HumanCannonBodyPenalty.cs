using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Settings;

namespace CurtainCall.HumanCannon
{
    /// <summary>인간 대포가 활성인 동안에만 설치하는 정책. 공통 점프/기어가기·부상 상태는 변경하지 않는다.</summary>
    public sealed class HumanCannonBodyPenalty : BodyPenaltyPolicy
    {
        HumanCannonInjurySettings settings;
        public void Initialize(HumanCannonInjurySettings value) => settings = value;

        public override BodyPenalty Evaluate(PlayerCondition condition)
        {
            var result = BodyPenalty.None;
            if (condition != null && settings != null)
                result.MoveSpeed = settings.MovePolicyMultiplier(condition.LostLegCount, GameSettings.Base.BodyDamage.CrawlSpeedMultiplier);
            // 손 닿는 거리는 줄이지 않는다. 낮은 손잡이/탑승 접근을 유지하고 외팔은 시간만 더 든다.
            return result;
        }
    }
}
