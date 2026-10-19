using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;

namespace CurtainCall.HumanCannon
{
    /// <summary>인간 대포가 활성인 동안 최소 80% 이동 속도를 보장한다. 공통 점프/기어가기 자세·부상 상태는 유지한다.</summary>
    public sealed class HumanCannonBodyPenalty : BodyPenaltyPolicy
    {
        HumanCannonInjurySettings settings;
        public void Initialize(HumanCannonInjurySettings value) => settings = value;

        public override BodyPenalty Evaluate(PlayerCondition condition)
        {
            var result = BodyPenalty.None;
            if (condition != null && settings != null)
                result.MoveSpeed = settings.MovePolicyMultiplier(condition.LostLegCount, GameSettings.Base.BodyDamage.CrawlSpeedMultiplier);
            var trampoline = HumanCannonTrampoline.Current;
            float baseSpeed = GameSettings.Base.Character.FreeMoveSpeed;
            if (baseSpeed > 0f && trampoline != null && trampoline.isActiveAndEnabled && condition != null
                && condition.TryGetComponent<NetworkPlayer>(out var player) && player.IsSpawned
                && player.State == PlayerState.Normal && trampoline.Carrier == player.OwnerClientId)
                result.MoveSpeed *= trampoline.CarryMoveSpeed / baseSpeed;
            // 손 닿는 거리는 줄이지 않는다. 낮은 손잡이/탑승 접근을 유지하고 외팔은 시간만 더 든다.
            return result;
        }
    }
}
