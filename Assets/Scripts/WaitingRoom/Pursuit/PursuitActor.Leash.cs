using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

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
                if (rule.PatrolLeashDistance <= 0 || Reached(patrolReturnPoint)) {
                    returningToPatrol = false; repathAt = 0;
                } else return;
            }
            if (WithinPatrolLeash(transform.position, rule)
                && (target == null || WithinPatrolLeash(target.position, rule))) return;
            returningToPatrol = true;
            target = null; visible = sensingLook = searching = investigating = false;
            unseen = 0; noiseUntil = searchUntil = prepareUntil = 0;
            Stop(); repathAt = 0;
            // Return to an authored waypoint, not a chord that could cross a wall.
            patrolReturnPoint = initialPosition;
            float distance = float.PositiveInfinity;
            var points = PatrolRoute;
            if (points != null) for (int i = 0; i < points.Length; i++) {
                if (points[i] == null) continue;
                float d = (points[i].position - transform.position).sqrMagnitude;
                if (d >= distance) continue;
                distance = d; patrolReturnPoint = points[i].position; waypoint = i;
            }
            if (NavMesh.SamplePosition(patrolReturnPoint, out var hit, 2f, agent.areaMask)) patrolReturnPoint = hit.position;
        }
    }
}
