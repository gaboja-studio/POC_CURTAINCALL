using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>기존 정면 톱과 같은 세운 원판. 오른쪽 축으로 구르는 날의 보이는 지름과 판정을 맞춘다.</summary>
    public static class CooperativeGateGeometry
    {
        public static bool TryGetContact(in BodyCapsule body, Vector3 baseCenter, Vector3 right,
            float diameter, float thickness, out Vector3 contact)
        {
            float radius = diameter * .5f;
            var disc = new SawDisc(baseCenter + Vector3.up * radius, right, radius, thickness * .5f);
            return SawMath.TryGetContact(disc, body, out contact);
        }

        /// <summary>현재 목마 앞점프 규칙의 이론 착지 거리. 공중 조작이나 순간 이동을 넣지 않는다.</summary>
        public static float ForwardJumpRange(float footHeight, float jumpHeight, float gravity, float speed)
        {
            gravity = Mathf.Max(.01f, gravity);
            float up = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, jumpHeight));
            return speed * (up + Mathf.Sqrt(up * up + 2f * gravity * Mathf.Max(0f, footHeight))) / gravity;
        }

        /// <summary>목마 출발선에서만 쓰는 시작 속도. 낮아진 몸 높이를 보완하고 착지까지 궤적은 고정한다.</summary>
        public static bool TryCalculateLaunch(float footHeight, float normalHeight, float gravity, float launchGap,
            float bodyRadius, SawSettings settings, out float jumpHeight, out float alongSpeed) =>
            TryCalculateLaunch(footHeight, normalHeight, gravity, launchGap, bodyRadius,
                settings.cooperativeGatePlateOffset, settings, out jumpHeight, out alongSpeed);

        /// <summary>상층은 발판 너머, 2층은 발판으로 보낸다. 출발 뒤에는 기존 고정 점프를 그대로 사용한다.</summary>
        public static bool TryCalculateLaunch(float footHeight, float normalHeight, float gravity, float launchGap,
            float bodyRadius, float landingOffset, SawSettings settings, out float jumpHeight, out float alongSpeed)
        {
            jumpHeight = alongSpeed = 0f;
            float span = launchGap + landingOffset;
            float radius = settings.cooperativeGateHeight * .5f;
            float contactHalf = radius + bodyRadius + .04f;
            if (!float.IsFinite(footHeight) || footHeight < 0f || !float.IsFinite(gravity) || gravity <= 0f
                || !float.IsFinite(normalHeight) || normalHeight < 0f || !float.IsFinite(span)
                || launchGap <= contactHalf || landingOffset <= contactHalf) return false;
            jumpHeight = Mathf.Max(normalHeight, settings.cooperativeGateHeight + settings.cooperativeGateJumpClearance - footHeight);
            float time = ForwardJumpRange(footHeight, jumpHeight, gravity, 1f);
            // 원판은 진행 방향으로도 반지름만큼 뻗는다. 앞뒤 원호에 맞춰 캡슐의 궤적을 확보한다.
            // 사각 벽의 높이를 전체 폭에 적용하면 착지 직전까지 불필요하게 높은 점프가 된다.
            float entry = (launchGap - contactHalf) / span, exit = (launchGap + contactHalf) / span;
            for (int sample = 0; sample <= 64; sample++)
            {
                float fraction = Mathf.Lerp(entry, exit, sample / 64f);
                float along = fraction * span - launchGap;
                float closest = Mathf.Max(0f, Mathf.Abs(along) - bodyRadius - .04f);
                float clearance = radius + Mathf.Sqrt(Mathf.Max(0f, radius * radius - closest * closest)) + .25f;
                time = Mathf.Max(time, RequiredTime(fraction, clearance));
            }
            float up = .5f * gravity * time - footHeight / time;
            jumpHeight = up * up / (2f * gravity);
            alongSpeed = span / time;
            return float.IsFinite(jumpHeight) && float.IsFinite(alongSpeed) && alongSpeed > 0f;

            float RequiredTime(float fraction, float clearance) => Mathf.Sqrt(Mathf.Max(0f,
                2f * (clearance - footHeight * (1f - fraction)) / (gravity * fraction * (1f - fraction))));
        }
    }
}
