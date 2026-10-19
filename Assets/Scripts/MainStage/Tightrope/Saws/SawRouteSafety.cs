using CurtainCall.Tightrope.Fire;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>정면 톱날의 옆줄 회피와 화재 전조 중 필수 이동 경로. 호스트가 생성/유지 여부를 정할 때 쓴다.</summary>
    public static class SawRouteSafety
    {
        /// <summary>그 위치에서 피할 수 있는 바로 옆줄이 없으면 인원수와 무관하게 정면 톱날을 두지 않는다.</summary>
        public static bool HasEscapeLane(TightropeCourse course, int lane, float distance,
            FireSettings fire = null, int pendingSegment = -1)
        {
            if (course == null || !course.IsLaneUsable(lane, distance) || !course.IsLaneChangeAllowed(distance)) return false;
            return CanLand(course, lane - 1, distance, fire, pendingSegment)
                || CanLand(course, lane + 1, distance, fire, pendingSegment);
        }

        static bool CanLand(TightropeCourse course, int lane, float distance, FireSettings fire, int pendingSegment)
        {
            if (!course.IsLaneUsable(lane, distance)) return false;
            return !TryWarning(fire, pendingSegment, distance, out var warning) || lane != warning.lane - 1;
        }

        /// <summary>아직 지나가지 않은 톱날이 현재 플레이어의 유일한 줄 또는 경고 줄을 건너야 하는 경로를 막는지.</summary>
        public static bool BlocksPlayerRoute(TightropeCourse course, Vector3 position, int sawLane,
            float sawDistance, float contactReach, FireSettings fire = null, int pendingSegment = -1)
        {
            if (course == null) return false;
            float distance = course.GetDistance(position);
            if (distance < 0f || distance >= course.FinishDistance || sawDistance + contactReach < distance) return false;
            int lane = course.Layout.GetLaneSlotAt(course.GetSide(position));
            if (!course.IsLaneUsable(lane, distance)) return false;
            if (lane == sawLane && !HasEscapeLane(course, lane, distance, fire, pendingSegment)) return true;
            if (!TryWarning(fire, pendingSegment, distance, out var warning)) return false;

            int burningLane = warning.lane - 1;
            if (burningLane < 0 || burningLane >= course.LaneCount) return false;
            int survivor = fire.FindSurvivingLane(course.LaneCount) - 1;
            if (survivor < 0)
            {
                // 예전 부분 화재처럼 최종 줄이 하나가 아니면 경고 줄에서 가능한 양쪽 착지만 보호한다.
                return lane == burningLane && Mathf.Abs(sawLane - lane) <= 1;
            }
            int direction = System.Math.Sign(survivor - burningLane);
            if (direction == 0 || (lane - burningLane) * direction > 0) return false;
            int destination = burningLane + direction;
            return sawLane >= Mathf.Min(lane, destination) && sawLane <= Mathf.Max(lane, destination);
        }

        static bool TryWarning(FireSettings fire, int pendingSegment, float distance, out FireSegment warning)
        {
            warning = default;
            if (fire?.segments == null || pendingSegment < 0 || pendingSegment >= fire.segments.Length) return false;
            warning = fire.segments[pendingSegment];
            return distance >= warning.from && distance < warning.to && distance < fire.safeDistance;
        }
    }
}
