using UnityEngine;
using CurtainCall.Settings;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        [Tooltip("각 줄을 독립된 위치/방향의 구간으로 사용한다. 네트워크 줄 번호는 sceneRopes 배열 순서다.")]
        [SerializeField] bool independentSceneRopes;
        public bool HasIndependentRopes => useSceneGeometry && independentSceneRopes;

        public Vector3 TraversalForwardAt(Vector3 position, Vector3 enteringForward)
            => OrientTraversalForward(ForwardAt(position), enteringForward);

        public Vector3 OrientTraversalForward(Vector3 authoredForward, Vector3 enteringForward)
            => Vector3.Dot(authoredForward, Vector3.ProjectOnPlane(enteringForward, Vector3.up)) < 0
                    ? -authoredForward : authoredForward;

        public int AuthoredLaneDirection(Vector3 position, Vector3 traversalForward, int direction)
            => Vector3.Dot(ForwardAt(position), traversalForward) < 0 ? -direction : direction;

        bool SceneLine(int lane, out Vector3 start, out Vector3 forward, out float length)
        {
            start = transform.position; forward = transform.forward; length = 0f;
            if (!HasIndependentRopes || lane < 0 || lane >= sceneRopes.Length || sceneRopes[lane] is not BoxCollider box) return false;
            start = box.transform.TransformPoint(box.center + new Vector3(0, box.size.y * .5f, -box.size.z * .5f));
            var end = box.transform.TransformPoint(box.center + new Vector3(0, box.size.y * .5f, box.size.z * .5f));
            forward = Vector3.ProjectOnPlane(end - start, Vector3.up);
            length = forward.magnitude;
            forward = length > .001f ? forward / length : Vector3.forward;
            return length > .001f;
        }

        int NearestSceneLine(Vector3 position)
        {
            int nearest = NoLane; float best = float.PositiveInfinity;
            for (int lane = 0; lane < sceneRopes.Length; lane++)
            {
                if (!SceneLine(lane, out var start, out var forward, out float length)) continue;
                var point = start + forward * Mathf.Clamp(Vector3.Dot(position - start, forward), 0, length);
                float distance = (position - point).sqrMagnitude;
                if (distance < best) { best = distance; nearest = lane; }
            }
            return nearest;
        }

        bool IsSceneLineActive(int lane) => lane >= 0 && lane < sceneRopes.Length
            && sceneRopes[lane] is BoxCollider && sceneRopes[lane].enabled && !sceneRopes[lane].isTrigger
            && sceneRopes[lane].gameObject.activeInHierarchy;

        /// <summary>배치 간격·배열 순서·줄 방향과 무관하게 같은 높이·수평 거리 안의 가까운 옆줄을 고른다.</summary>
        public bool TryFindLaneLanding(Vector3 position, Vector3 traversalForward, int direction,
            out int result, out Vector3 landing)
        {
            result = NoLane;
            landing = default;
            if (direction == 0 || !TryGetRopePoint(position, out int from, out float along)
                || !IsLaneUsable(from, along)) return false;
            Vector3 source = GetLanePosition(from, along);
            Vector3 right = Vector3.Cross(Vector3.up, traversalForward).normalized * Mathf.Sign(direction);
            float reach = GameSettings.Tightrope.RopeMovement.LaneJumpReach;
            float best = float.PositiveInfinity;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                if (lane == from || HasIndependentRopes && !IsSceneLineActive(lane)) continue;
                Vector3 start = GetLanePosition(lane, 0f);
                Vector3 forward = (GetLanePosition(lane, 1f) - start).normalized;
                float length = HasIndependentRopes && SceneLine(lane, out _, out _, out float sceneLength)
                    ? sceneLength : Layout.GetLaneEnd(lane);
                float distance = Mathf.Clamp(Vector3.Dot(source - start, forward), 0f, length);
                Vector3 point = start + forward * distance;
                Vector3 delta = point - source;
                // 1mm는 먼 월드 좌표의 float 오차만 허용한다. 높이 허용 판정(발 위치)과 구분한다.
                if (Mathf.Abs(delta.y) > .001f) continue;
                float horizontalSquared = Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude;
                if (Vector3.Dot(delta, right) <= .001f || horizontalSquared > (reach + .001f) * (reach + .001f)
                    || horizontalSquared >= best || !IsLaneUsable(lane, distance)) continue;
                best = horizontalSquared;
                result = lane;
                landing = point;
            }
            return result != NoLane;
        }

        public Vector3 ForwardAt(Vector3 position) => SceneLine(NearestSceneLine(position), out _, out var direction, out _)
            ? direction : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        public bool TryGetHangTarget(int lane, Vector3 position, out float distance, out Vector3 point)
        {
            distance = 0f; point = default;
            if (lane < 0 || lane >= LaneCount || HasIndependentRopes && !IsSceneLineActive(lane)) return false;
            Vector3 start = GetLanePosition(lane, 0f);
            Vector3 forward = (GetLanePosition(lane, 1f) - start).normalized;
            float length = HasIndependentRopes && SceneLine(lane, out _, out _, out float authoredLength)
                ? authoredLength : Layout.GetLaneEnd(lane);
            distance = Mathf.Clamp(Vector3.Dot(position - start, forward), 0f, length);
            point = start + forward * distance;
            if (IsLaneUsable(lane, distance)) return true;
            float best = float.PositiveInfinity;
            float projected = distance;
            foreach (var span in GetUsableSpans(lane))
            {
                float from = Mathf.Max(0f, span.x), to = Mathf.Min(length, span.y);
                if (to <= from) continue;
                float inset = Mathf.Min(.001f, (to - from) * .25f);
                float candidate = Mathf.Clamp(projected, from + inset, to - inset);
                if (!IsLaneUsable(lane, candidate)) continue;
                var target = start + forward * candidate;
                float squared = (target - position).sqrMagnitude;
                if (squared >= best) continue;
                best = squared; distance = candidate; point = target;
            }
            return float.IsFinite(best);
        }

        public Transform HangTargetRoot(int lane)
        {
            if (useSceneGeometry) return lane >= 0 && lane < sceneRopes.Length && sceneRopes[lane] != null ? sceneRopes[lane].transform : null;
            return laneGeometry.TryGetValue(lane, out var root) ? root : null;
        }

        public float RopeHeightAt(Vector3 position) => SceneLine(NearestSceneLine(position), out var start, out _, out _)
            ? start.y : transform.position.y;

        float SceneDistance(Vector3 position)
        {
            SceneLine(NearestSceneLine(position), out var start, out var forward, out _);
            return Vector3.Dot(position - start, forward);
        }

        float SceneSide(Vector3 position)
        {
            int lane = NearestSceneLine(position);
            SceneLine(lane, out var start, out var forward, out _);
            return Layout.GetLaneSide(lane) + Vector3.Dot(position - start, Vector3.Cross(Vector3.up, forward));
        }

        bool SceneRopePoint(Vector3 position, out int lane, out float distance)
        {
            lane = NearestSceneLine(position); distance = 0f;
            if (!SceneLine(lane, out var start, out var forward, out float length)) return false;
            distance = Vector3.Dot(position - start, forward);
            if (distance < 0 || distance > length) { lane = NoLane; return false; }
            return true;
        }

        public bool PastSceneRopeEnd(Vector3 position, float margin)
        {
            if (!useSceneGeometry) return false;
            if (HasIndependentRopes && SceneLine(NearestSceneLine(position), out var start, out var forward, out float length))
            {
                float along = Vector3.Dot(position - start, forward);
                return along < -margin || along > length + margin;
            }
            float distance = GetDistance(position);
            return distance < -margin || distance > FinishDistance + margin;
        }
    }
}
