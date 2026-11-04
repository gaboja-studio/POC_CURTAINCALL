using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        bool returningToPatrol;
        Vector3 patrolReturnPoint;
        public bool ReturningToPatrol => returningToPatrol;
        Transform[] PatrolRoute => Placement != null ? Placement.Route(Room) : Room.PatrolPoints;

        public bool WithinPatrolLeash(Vector3 position, PursuitStage stage) => stage.PatrolLeashDistance <= 0
            || Vector3.Distance(position, ClosestPatrolPosition(position, PatrolRoute, initialPosition)) <= stage.PatrolLeashDistance;

        // Distance to the closed patrol polyline, including height. Sparse waypoints
        // must not make the middle of an otherwise valid patrol segment out of bounds.
        public static Vector3 ClosestPatrolPosition(Vector3 position, Transform[] points, Vector3 fallback)
        {
            Transform first = null, previous = null;
            Vector3 closest = fallback;
            float distance = float.PositiveInfinity;
            void Segment(Vector3 a, Vector3 b) {
                Vector3 delta = b - a;
                float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(position - a, delta) / delta.sqrMagnitude) : 0;
                Vector3 point = a + delta * t;
                float d = (position - point).sqrMagnitude;
                if (d < distance) { distance = d; closest = point; }
            }
            if (points != null) foreach (var point in points) {
                if (point == null) continue;
                if (first == null) first = point;
                Segment(previous != null ? previous.position : point.position, point.position);
                previous = point;
            }
            if (first != null && previous != null) Segment(previous.position, first.position);
            return closest;
        }

        void TickPatrolBoundary()
        {
            if (returningToPatrol) {
                // 복귀 지점에 닿을 수 없거나 막혀도 순찰을 재개한다. 순찰은 다음 도달 가능한 순회지를 찾는다.
                if (rule.PatrolLeashDistance <= 0 || Reached(patrolReturnPoint) || (SameGoal(patrolReturnPoint) && navFailed)) {
                    returningToPatrol = false; repathAt = 0; hasNavGoal = false;
                } else return;
            }
            // 순찰 자체가 벽을 돌아가는 우회로로 제한을 넘는 것은 이탈이 아니다. 추격·조사·수색만 제한한다.
            bool selfOut = Behaviour != PursuitBehaviour.Patrol && !WithinPatrolLeash(transform.position, rule);
            if (!selfOut && (target == null || WithinPatrolLeash(target.position, rule))) return;
            returningToPatrol = true;
            target = null; visible = sensingLook = searching = investigating = false;
            unseen = 0; noiseUntil = searchUntil = prepareUntil = 0;
            Stop(); repathAt = 0;
            // Return to the authored waypoint with the shortest NavMesh path, not a chord that could cross a wall.
            patrolReturnPoint = NearestPatrolByPath(PatrolRoute, out waypoint);
            hasNavGoal = false; patrolSkips = 0;
        }
    }
}
