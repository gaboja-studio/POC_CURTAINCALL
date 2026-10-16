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
    }
}
