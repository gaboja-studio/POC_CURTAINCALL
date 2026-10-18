using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>인간 대포의 부상 속도는 정상 속도의 최소 80%. 몸 점프/기어가기 자세는 공통 설정을 쓴다.</summary>
    [Serializable]
    public sealed class HumanCannonInjurySettings
    {
        const float MinimumSpeedMultiplier = 0.8f;

        [Tooltip("다리 하나 손실 시 지상 이동 배율. 운반 중에도 적용하며 최소 0.8을 유지한다.")]
        [SerializeField, Range(MinimumSpeedMultiplier, 1f)] float oneLegMove = 0.8f;
        [Tooltip("다리 하나 손실 시 WASD 공중 보정 배율. 최소 0.8. 발사/반동 속도에는 적용하지 않는다.")]
        [SerializeField, Range(MinimumSpeedMultiplier, 1f)] float oneLegAir = 0.8f;
        [Tooltip("두 다리 손실 시 WASD 공중 보정 배율. 외팔도 같은 값이며 최소 0.8을 유지한다.")]
        [SerializeField, Range(MinimumSpeedMultiplier, 1f)] float twoLegAir = 0.8f;
        [Tooltip("외팔 상태에서 F 잡기/대포 탑승 요청 후 호스트 확정까지 시간(초). 시험값 0.6.")]
        [SerializeField, Min(0f)] float oneArmInteractionSeconds = 0.6f;

        public float MovePolicyMultiplier(int lostLegs, float crawlMultiplier)
        {
            if (lostLegs <= 0) return 1f;
            float oneLeg = Mathf.Clamp(oneLegMove, MinimumSpeedMultiplier, 1f);
            if (lostLegs == 1) return oneLeg;
            // BodyDamageEffects가 공통 기어가기 배율을 곱한 뒤에도 최소 80%가 되도록 보정한다.
            // 정책 배율은 1을 넘을 수 있지만 최종 속도는 한 다리 상태보다 빨라지지 않는다.
            // 공통 배율 0은 이동 금지이므로 나눗셈으로 상쇄하지 않는다.
            return crawlMultiplier > 0f
                ? Mathf.Clamp(crawlMultiplier, MinimumSpeedMultiplier, oneLeg) / crawlMultiplier : 1f;
        }

        public float AirMultiplier(int lostLegs) => lostLegs <= 0 ? 1f
            : lostLegs == 1 ? Mathf.Clamp(oneLegAir, MinimumSpeedMultiplier, 1f)
            : Mathf.Clamp(twoLegAir, MinimumSpeedMultiplier, Mathf.Clamp(oneLegAir, MinimumSpeedMultiplier, 1f));

        public float InteractionSeconds(bool oneArm) => oneArm ? Mathf.Max(0f, oneArmInteractionSeconds) : 0f;
    }
}
