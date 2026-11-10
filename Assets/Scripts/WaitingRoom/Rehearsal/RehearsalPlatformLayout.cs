using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Fits the expanded landing deck inside the existing room without moving the course or its finish line.</summary>
    public static class RehearsalPlatformLayout
    {
        public static bool TryMeasureInnerRadius(Transform boundary, Vector3 center, float floorY, out float radius)
        {
            radius = float.PositiveInfinity;
            if (boundary == null) return false;
            var atHeight = new Vector3(center.x, floorY, center.z);
            foreach (var wall in boundary.GetComponentsInChildren<BoxCollider>(true))
            {
                var local = wall.transform.InverseTransformPoint(atHeight);
                var min = wall.center - wall.size * .5f;
                var max = wall.center + wall.size * .5f;
                if (local.y < min.y || local.y > max.y) continue; // Door headers do not bound the deck.
                var closest = wall.transform.TransformPoint(new Vector3(Mathf.Clamp(local.x, min.x, max.x), local.y, Mathf.Clamp(local.z, min.z, max.z)));
                float distance = Vector2.Distance(new Vector2(center.x, center.z), new Vector2(closest.x, closest.z));
                if (distance > 0f) radius = Mathf.Min(radius, distance);
            }
            return float.IsFinite(radius) && radius > 0f;
        }

        public static float FitFinishScale(TightropeSettings.CourseSettings shape, Transform pose, Vector3 center, Transform boundary, float requested)
        {
            if (!TryMeasureInnerRadius(boundary, center, pose.position.y, out float radius))
            {
                Debug.LogWarning("[Rehearsal] 연습실 외곽 벽을 측정할 수 없어 도착대를 원래 크기로 생성합니다.");
                return Mathf.Min(1f, requested);
            }
            if (!FitsInsideRoom(TightropeCourse.PreviewStartPlatformBounds(shape), pose, center, radius, 0f))
                Debug.LogError("[Rehearsal] 출발대가 연습실 경계를 넘습니다. 코스 배치를 확인하세요.");
            bool Fits(float scale) => TightropeCourse.PreviewFinishPlatformBounds(shape, scale, out var floor)
                && FitsInsideRoom(floor, pose, center, radius, RehearsalArrivalWalls.Thickness);
            if (Fits(requested)) return requested;
            float lower = 1f, upper = requested;
            if (!Fits(lower))
            {
                Debug.LogError("[Rehearsal] 원본 도착대도 연습실 경계를 넘습니다. 코스 배치를 확인하세요.");
                return lower;
            }
            for (int i = 0; i < 24; i++)
            {
                float middle = (lower + upper) * .5f;
                if (Fits(middle)) lower = middle;
                else upper = middle;
            }
            return lower;
        }

        public static bool FitsInsideRoom(Bounds floor, Transform pose, Vector3 center, float radius, float border)
        {
            float bodyRadius = GameSettings.Base.Character.CapsuleRadius;
            foreach (float x in new[] { floor.min.x - border, floor.max.x + border })
                foreach (float z in new[] { floor.min.z - border, floor.max.z + border })
                {
                    var point = pose.TransformPoint(new Vector3(x, floor.max.y, z));
                    if (Vector2.Distance(new Vector2(center.x, center.z), new Vector2(point.x, point.z)) + bodyRadius > radius) return false;
                }
            return true;
        }
    }
}
