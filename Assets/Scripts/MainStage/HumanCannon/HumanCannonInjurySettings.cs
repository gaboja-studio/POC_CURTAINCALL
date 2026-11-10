using System;
using CurtainCall.Settings;

namespace CurtainCall.HumanCannon
{
    /// <summary>기존 호출자 호환 조회. 대포별 부상 덮어쓰기 없이 Base 공통 설정만 읽는다.</summary>
    [Serializable]
    public sealed class HumanCannonInjurySettings
    {
        public float MovePolicyMultiplier(int lostLegs, float crawlMultiplier) => lostLegs == 1 ? GameSettings.Base.BodyDamage.ExploreOneLegMoveSpeed : 1f;
        public float AirMultiplier(int lostLegs) => GameSettings.Base.BodyDamage.MoveMultiplier(lostLegs);
        public float InteractionSeconds(bool oneArm) => 0f;
    }
}
