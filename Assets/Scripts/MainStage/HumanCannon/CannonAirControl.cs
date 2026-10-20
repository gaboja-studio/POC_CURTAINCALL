using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>한 프레임의 수평 보정만 소비한다. 발사 속도나 수직 운동에는 누적하지 않는다.</summary>
    public sealed class CannonAirControl
    {
        Vector2 input;
        int inputFrame = -1;

        public void SetInput(Vector2 value, int frame)
        {
            input = Vector2.ClampMagnitude(value, 1f);
            inputFrame = frame;
        }

        public void Clear()
        {
            input = Vector2.zero;
            inputFrame = -1;
        }

        public Vector3 ConsumeVelocity(int frame, bool blocked, Vector3 viewForward, float speed)
        {
            Vector2 current = !blocked && inputFrame == frame ? input : Vector2.zero;
            Clear();
            Vector3 forward = Vector3.ProjectOnPlane(viewForward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return (right * current.x + forward * current.y) * Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 봉 도약의 수평 속력을 유지하면서 입력 방향으로 반동을 돌린다.
        /// 키를 놓으면 마지막 비행 방향으로 계속 날고, 반복 방향 전환으로 속력이 줄거나 늘지 않는다.
        /// 수직 탄도는 바꾸지 않는다.
        /// </summary>
        public static Vector3 SteerPoleMomentum(Vector3 velocity, Vector3 correction)
        {
            Vector3 direction = Vector3.ProjectOnPlane(correction, Vector3.up);
            if (direction.sqrMagnitude < 0.000001f) return velocity;
            direction.Normalize();
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            return Vector3.up * velocity.y + direction * horizontal.magnitude;
        }
    }
}
