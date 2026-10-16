using System;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>044 사용자 승인 시험값. 몸 점프/기어가기 세팅은 공통 설정을 그대로 쓴다.</summary>
    [Serializable]
    public sealed class HumanCannonInjurySettings
    {
        [Tooltip("다리 하나 손실 시 지상 이동 배율. 운반 중에도 적용한다. 시험값 0.8.")]
        [SerializeField, Range(0f, 1f)] float oneLegMove = 0.8f;
        [Tooltip("다리 하나 손실 시 WASD 공중 보정 배율. 발사/반동 속도에는 적용하지 않는다.")]
        [SerializeField, Range(0f, 1f)] float oneLegAir = 0.8f;
        [Tooltip("두 다리 손실 시 WASD 공중 보정 배율. 외팔도 같은 값. 시험값 0.5.")]
        [SerializeField, Range(0f, 1f)] float twoLegAir = 0.5f;
        [Tooltip("외팔 상태에서 F 잡기/대포 탑승 요청 후 호스트 확정까지 시간(초). 시험값 0.6.")]
        [SerializeField, Min(0f)] float oneArmInteractionSeconds = 0.6f;

        public float MovePolicyMultiplier(int lostLegs, float crawlMultiplier)
        {
            if (lostLegs <= 0) return 1f;
            float oneLeg = Mathf.Clamp01(oneLegMove);
            // BodyDamageEffects가 기어가기 배율을 따로 곱한다. 중복 감소하지 않되
            // 공통 설정을 조정해도 두 다리 손실이 한 다리 손실보다 빨라지지 않는다.
            return lostLegs == 1 ? oneLeg : crawlMultiplier > 0f ? Mathf.Min(1f, oneLeg / crawlMultiplier) : 1f;
        }

        public float AirMultiplier(int lostLegs) => lostLegs <= 0 ? 1f
            : lostLegs == 1 ? Mathf.Clamp01(oneLegAir) : Mathf.Clamp(twoLegAir, 0f, Mathf.Clamp01(oneLegAir));

        public float InteractionSeconds(bool oneArm) => oneArm ? Mathf.Max(0f, oneArmInteractionSeconds) : 0f;
    }
}
