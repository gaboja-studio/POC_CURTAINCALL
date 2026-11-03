using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>한 프레임의 수평 보정만 소비한다. 발사 속도나 수직 운동에는 누적하지 않는다.</summary>
    public sealed class CannonAirControl
    {
        Vector2 input;
        int inputFrame = -1;
        Vector3 poleCorrection;
        float inheritedPump;
        float pumpGrace;

        public void BeginPoleFlight(float pumpInput)
        {
            poleCorrection = Vector3.zero;
            inheritedPump = Mathf.Abs(pumpInput) > .1f ? Mathf.Sign(pumpInput) : 0f;
            pumpGrace = .15f;
        }

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
        /// 봉에서 받은 관성은 보존하고 입력 중에만 부드러운 수평 보정을 더한다.
        /// 반동을 주던 W/S는 놓기 직후 잠깐 유예하며 새 방향 입력은 즉시 받는다.
        /// </summary>
        public Vector3 ConsumePoleVelocity(int frame, bool blocked, Vector3 viewForward, float speed,
            Vector3 launchVelocity, float speedLimit, float dt)
        {
            dt = Mathf.Max(0f, dt);
            if (pumpGrace > 0f && inputFrame == frame)
            {
                if (Mathf.Abs(input.y) > .1f && Mathf.Sign(input.y) == inheritedPump) input.y = 0f;
                else pumpGrace = 0f;
            }
            pumpGrace = Mathf.Max(0f, pumpGrace - dt);
            Vector3 target = ConsumeVelocity(frame, blocked, viewForward, speed);
            poleCorrection = blocked ? Vector3.zero : Vector3.ClampMagnitude(
                Vector3.Lerp(poleCorrection, target, 1f - Mathf.Exp(-dt / .1f)), Mathf.Max(0f, speed));
            Vector3 horizontal = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
            return Vector3.ClampMagnitude(horizontal + poleCorrection, Mathf.Max(0f, speedLimit)) - horizontal;
        }

        /// <summary>봉 도약의 수평 속도만 제한한다. 상승·하강 속도와 수평 진행 방향은 유지한다.</summary>
        public static Vector3 LimitPoleHorizontalSpeed(Vector3 velocity, float limit) =>
            Vector3.up * velocity.y + Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up), Mathf.Max(0f, limit));
    }
}
