using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>외줄에만 필요한 구간 배치. 움직임·중력·공중 보정은 인간대포 시스템을 사용한다.</summary>
    public static class SwingPoleMath
    {
        public const float EntryOffset = .8f, ExitOffset = 1.2f, GripHeight = 1.3f;
        public static bool HasGap(float finish, float start, float length) =>
            float.IsFinite(finish) && float.IsFinite(start) && float.IsFinite(length)
            && start >= 10f && length >= 4f && length <= 12f && start + length + 10f < finish;
        public static bool Contains(float distance, float start, float length) => distance >= start && distance < start + length;

        /// <summary>줄 높이까지의 탄도 체공 시간으로 출발 시 수평 속도 상한을 정한다.</summary>
        public static float FlightSpeedLimit(float height, float upwardSpeed, float gravity, float range)
        {
            if (!float.IsFinite(height) || !float.IsFinite(upwardSpeed) || !float.IsFinite(gravity)
                || !float.IsFinite(range) || gravity <= 0f || range <= 0f) return 0f;
            float time = (upwardSpeed + Mathf.Sqrt(upwardSpeed * upwardSpeed + 2f * gravity * Mathf.Max(0f, height))) / gravity;
            return range / Mathf.Max(.05f, time);
        }

        /// <summary>반동과 공중 입력의 수평 속도만 제한한다. 위치를 잘라 내거나 수직 탄도를 바꾸지 않는다.</summary>
        public static Vector3 LimitFlightVelocity(Vector3 velocity, float speedLimit)
        {
            Vector3 horizontal = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up), Mathf.Max(0f, speedLimit));
            return horizontal + Vector3.up * velocity.y;
        }

        /// <summary>다음 봉 높이까지의 하강 교차 시각으로 비거리를 제한한다. 줄 착지용 계산과 분리한다.</summary>
        public static float PoleFlightSpeedLimit(Vector3 origin, Vector3 target, Vector3 forward, float up, float gravity)
        {
            if (!float.IsFinite(gravity) || gravity <= 0f || !float.IsFinite(up)) return 0f;
            float square = up * up + 2f * gravity * (origin.y - target.y);
            if (!float.IsFinite(square) || square < 0f) return 0f;
            float time = (up + Mathf.Sqrt(square)) / gravity;
            float range = Vector3.Dot(target - origin, Vector3.ProjectOnPlane(forward, Vector3.up).normalized);
            return time > .01f && float.IsFinite(range) ? Mathf.Max(0f, range) / time : 0f;
        }

        /// <summary>봉 길이 안에서 필요한 상승량과 상한을 맞춘다. 반동의 수평 방향·세기는 유지한다.</summary>
        public static Vector3 PoleFlightVelocity(Vector3 origin, Vector3 target, Vector3 velocity, Vector3 forward,
            float gravity, float hangLength, out float speedLimit)
        {
            speedLimit = 0f;
            if (!float.IsFinite(gravity) || gravity <= 0f || !float.IsFinite(hangLength) || hangLength <= 0f) return Vector3.zero;
            float rise = Mathf.Max(0f, target.y - origin.y);
            velocity.y = Mathf.Clamp(velocity.y, Mathf.Sqrt(2f * gravity * (rise + Mathf.Min(.5f, hangLength))),
                Mathf.Sqrt(2f * gravity * (rise + hangLength)));
            speedLimit = PoleFlightSpeedLimit(origin, target, forward, velocity.y, gravity);
            return LimitFlightVelocity(velocity, speedLimit);
        }

        /// <summary>봉/동료 중심에서 모든 방향에 같은 반지름을 사용하는 구형 잡기 범위.</summary>
        public static bool InPoleReach(Vector3 position, Vector3 target, float reach)
        {
            Vector3 delta = position - target;
            float distance = 2.5f * reach;
            return float.IsFinite(distance) && distance > 0f && delta.sqrMagnitude <= distance * distance;
        }

        /// <summary>다음 봉 방향 수평 비거리의 50~100%에서 잡을 수 있다. 옆 방향은 같은 통로 안으로 제한한다.</summary>
        public static bool InCatchWindow(Vector3 origin, Vector3 position, Vector3 forward, float limit, float width)
        {
            if (!float.IsFinite(limit) || limit <= .01f || !float.IsFinite(width) || width <= 0f) return false;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return false;
            Vector3 offset = Vector3.ProjectOnPlane(position - origin, Vector3.up);
            float along = Vector3.Dot(offset, forward);
            return along >= limit * .5f - .001f && along <= limit + .001f
                && (offset - forward * along).sqrMagnitude <= width * width;
        }
    }
}
