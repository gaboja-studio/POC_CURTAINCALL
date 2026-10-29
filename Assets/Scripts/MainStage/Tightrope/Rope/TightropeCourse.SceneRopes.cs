using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        [Tooltip("각 줄을 독립된 위치/방향의 구간으로 사용한다. 네트워크 줄 번호는 sceneRopes 배열 순서다.")]
        [SerializeField] bool independentSceneRopes;
        public bool HasIndependentRopes => useSceneGeometry && independentSceneRopes;

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
            && sceneRopes[lane] != null && sceneRopes[lane].enabled && sceneRopes[lane].gameObject.activeInHierarchy;

        /// <summary>배열 번호가 아닌 실제 옆줄 점프 도착점에서 같은 방향/높이의 지지줄을 찾는다.</summary>
        int FindSceneLandingLane(Vector3 position, int direction)
        {
            if (direction == 0 || LaneSpacing <= 0f) return NoLane;
            int from = NearestSceneLine(position);
            if (!IsSceneLineActive(from) || !SceneLine(from, out var start, out var forward, out float length)) return NoLane;
            float along = Vector3.Dot(position - start, forward);
            if (along < 0f || along > length || !IsLaneUsable(from, along)) return NoLane;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 landing = start + forward * along + right * (Mathf.Sign(direction) * LaneSpacing);
            float tolerance = Mathf.Max(.01f, Mathf.Min(.15f, Shape.RopeWalkWidth * .5f));
            float best = tolerance * tolerance;
            int result = NoLane;
            for (int lane = 0; lane < sceneRopes.Length; lane++)
            {
                if (lane == from || !IsSceneLineActive(lane)
                    || !SceneLine(lane, out var otherStart, out var otherForward, out float otherLength)
                    || Vector3.Dot(forward, otherForward) < .999f
                    || Mathf.Abs(otherStart.y - start.y) > Shape.RopeHeightTolerance) continue;
                float distance = Vector3.Dot(landing - otherStart, otherForward);
                if (distance < 0f || distance > otherLength || !IsLaneUsable(lane, distance)) continue;
                Vector3 error = Vector3.ProjectOnPlane(otherStart + otherForward * distance - landing, Vector3.up);
                if (error.sqrMagnitude > best) continue;
                best = error.sqrMagnitude;
                result = lane;
            }
            return result;
        }

        public Vector3 ForwardAt(Vector3 position) => SceneLine(NearestSceneLine(position), out _, out var direction, out _)
            ? direction : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

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
