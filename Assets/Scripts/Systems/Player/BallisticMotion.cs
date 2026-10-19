using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>인간대포의 일정 중력 적분. 수평 보정은 이번 프레임 변위에만 더하고 발사 속도에는 누적하지 않는다.</summary>
    public static class BallisticMotion
    {
        public static Vector3 Step(ref Vector3 velocity, Vector3 correction, float gravity, float dt)
        {
            Vector3 displacement = (velocity + correction) * dt + Vector3.down * (.5f * gravity * dt * dt);
            velocity.y -= gravity * dt;
            return displacement;
        }
    }
}
