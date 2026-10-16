using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>손잡이를 운반자 발 위치에 붙이고, 손잡이에서 장치 중앙을 향하는 방향을 정면에 맞춘다.</summary>
    public static class TrampolineCarryPose
    {
        public static bool TryCalculate(Vector3 carrierPosition, Vector3 carrierForward, Vector3 scaledHandleOffset,
            out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!Finite(carrierPosition) || !Finite(carrierForward) || !Finite(scaledHandleOffset)
                || (carrierForward.x == 0f && carrierForward.z == 0f)
                || (scaledHandleOffset.x == 0f && scaledHandleOffset.z == 0f)) return false;

            // 수평 두 방향의 yaw 차이만 적용한다. 정반대 방향에서도 pitch/roll을 만들지 않는다.
            float targetYaw = Mathf.Atan2(carrierForward.x, carrierForward.z) * Mathf.Rad2Deg;
            float handleToCenterYaw = Mathf.Atan2(-scaledHandleOffset.x, -scaledHandleOffset.z) * Mathf.Rad2Deg;
            Quaternion facing = Quaternion.Euler(0f, targetYaw - handleToCenterYaw, 0f);
            Vector3 center = carrierPosition - facing * scaledHandleOffset;
            if (!Finite(center)) return false;
            position = center;
            rotation = facing;
            return true;
        }

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
