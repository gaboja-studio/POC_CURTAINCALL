using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>처음에는 위로 튀고, 대기 중인 동료를 넘어 뒤에 착지한 뒤 기존 방향으로 계속 굴러간다.</summary>
    public static class CooperativeGateHop
    {
        // x는 관문 정지 지점에서 시작점 방향의 거리, y는 줄에서의 높이다.
        public static Vector2 Sample(float elapsed, float duration, float distance, float height, float speed, float initialLift = 0f)
        {
            if (!float.IsFinite(elapsed) || !float.IsFinite(duration) || !float.IsFinite(distance)
                || !float.IsFinite(height) || !float.IsFinite(speed) || !float.IsFinite(initialLift)) return Vector2.zero;
            duration = Mathf.Max(.5f, duration); distance = Mathf.Max(3f, distance);
            height = Mathf.Max(0f, height); speed = Mathf.Max(.1f, speed);
            initialLift = Mathf.Max(0f, initialLift);
            if (elapsed >= duration) return new Vector2(-distance - (elapsed - duration) * speed, 0f);
            float t = Mathf.Clamp01(elapsed / duration);
            return new Vector2(-distance * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .1f) / .9f)),
                Mathf.Lerp(initialLift, 0f, t) + 4f * height * t * (1f - t));
        }
    }
}
