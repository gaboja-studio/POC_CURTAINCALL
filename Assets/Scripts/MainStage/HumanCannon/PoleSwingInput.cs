using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>한 방향 입력을 한 번의 반동으로 소비한다. 유지 입력으로 중력을 계속 상쇄하지 않는다.</summary>
    public struct PoleSwingInput
    {
        const float PushSeconds = 0.65f;
        int direction;
        float remaining;

        public static int DirectionOf(float input) => !float.IsFinite(input) || Mathf.Abs(input) < 0.01f
            ? 0 : input > 0f ? 1 : -1;

        // 네트워크 입력이 한 물리 프레임 안에 여러 개 와도 키를 놓은 경계를 보존한다.
        public void Observe(float input)
        {
            int nextDirection = DirectionOf(input);
            if (nextDirection == 0)
            {
                this = default;
                return;
            }
            if (direction != nextDirection)
            {
                direction = nextDirection;
                remaining = PushSeconds;
            }
        }

        public float Step(float input, float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f) return 0f;
            Observe(input);
            if (direction == 0) return 0f;
            // 물리 적분과 같은 시간 범위를 소비하고, 마지막 부분 프레임도 힘의 총량을 맞춘다.
            float elapsed = Mathf.Min(deltaTime, 0.25f);
            float active = Mathf.Min(remaining, elapsed);
            remaining -= active;
            return Mathf.Clamp(input, -1f, 1f) * (active / elapsed);
        }
    }
}
