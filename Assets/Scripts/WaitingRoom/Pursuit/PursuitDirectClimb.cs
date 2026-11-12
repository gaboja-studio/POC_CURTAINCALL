using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// NavMesh가 없는 소품 위(좁은 상자·드럼통·책상·침대 등)로 직접 오르기.
    /// 씬·NavMesh를 바꾸지 않고 실제 충돌체 윗면·가장자리·머리 공간만 물리 검사로 찾는다.
    /// </summary>
    public static class PursuitDirectClimb
    {
        public enum Plan { None, Approach, Climb }
        /// <summary>가장자리에서 이 거리 안이면 바로 오른다. 멀면 가장자리 앞까지 걸어간다.</summary>
        public const float EdgeReach = .95f;
        /// <summary>더 다가갈 바닥이 없을 때 이 자리에서 바로 오를 수 있는 가장자리까지 최대 수평 거리.</summary>
        public const float MaxDirectReach = 1.8f;
        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        /// <summary>소품 위 대상을 직접 오를 후보로 보는 최대 수평 거리.</summary>
        public const float SearchDistance = 4f;
        const float StepOut = .45f, LipClearance = 1f, BodyRadius = .25f, BodyHeight = 1.5f, SurfaceTolerance = .3f;
        /// <summary>몸 두께 검사 반경과 시작 높이(발 위). 가는 선 하나로는 통로 바닥 밑·난간 옆에서 몸이 겹친 채 오른다.</summary>
        public const float BodyCheckRadius = .2f, BodyLift = .45f;
        static readonly Vector3[] DropDirections = new Vector3[8];
        static PursuitDirectClimb() { for (int k = 0; k < 8; k++) DropDirections[k] = Quaternion.Euler(0, k * 45, 0) * Vector3.forward; }
        static readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly Collider[] overlaps = new Collider[16];

        /// <summary>
        /// origin(추격자 발)에서 goal(소품 위 대상 발)로 오를 계획. Climb이면 point는 윗면 착지점,
        /// Approach면 가장자리 앞 바닥 NavMesh 지점이다.
        /// </summary>
        public static Plan Find(Vector3 origin, Vector3 goal, Transform self, float maxHeight, int areaMask, out Vector3 point)
        {
            point = default;
            if (!SurfaceAt(goal, goal.y, self, out var under)) return Plan.None;
            float top = under.point.y, height = top - origin.y;
            if (height < PursuitClimbLinks.MinHeight - .15f || height > maxHeight + .05f) return Plan.None;
            var flat = goal - origin; flat.y = 0;
            float distance = flat.magnitude;
            if (distance > SearchDistance) return Plan.None;
            var plan = Along(origin, goal, top, self, areaMask, out point);
            // 곧장 가는 쪽이 막혔으면(벽에 붙은 소품·다른 소품) 다른 방향 가장자리 앞 바닥으로 돌아간다.
            return plan != Plan.None ? plan : Around(origin, goal, top, self, areaMask, out point);
        }

        static Plan Around(Vector3 origin, Vector3 goal, float top, Transform self, int areaMask, out Vector3 point)
        {
            point = default;
            float best = float.PositiveInfinity;
            for (int k = 0; k < 8; k++) {
                var direction = Quaternion.Euler(0, k * 45, 0) * Vector3.forward;
                float edge = -1;
                for (float d = .1f; d <= 2.5f; d += .1f)
                    if (!SurfaceAt(goal + direction * d, top, self, out _)) { edge = d; break; }
                if (edge < 0) continue;
                // 가장자리 바깥 바닥은 윗면이 아니라 추격자가 서 있는 바닥 높이에서 찾는다.
                var probe = goal + direction * (edge + StepOut); probe.y = origin.y;
                if (!NavMesh.SamplePosition(probe, out var floor, .6f, areaMask)
                    || Mathf.Abs(floor.position.y - origin.y) > .45f) continue;
                var stand = floor.position;
                var offset = stand - origin; offset.y = 0;
                if (offset.sqrMagnitude < .3f * .3f || offset.magnitude >= best) continue;
                if (Along(stand, goal, top, self, areaMask, out _) != Plan.Climb) continue;
                best = offset.magnitude; point = stand;
            }
            return best < float.PositiveInfinity ? Plan.Approach : Plan.None;
        }

        static Plan Along(Vector3 origin, Vector3 goal, float top, Transform self, int areaMask, out Vector3 point)
        {
            point = default;
            var flat = goal - origin; flat.y = 0;
            float distance = flat.magnitude;
            var direction = distance > .05f ? flat / distance : (self != null ? self.forward : Vector3.forward);
            // 추격자 쪽에서 대상 쪽으로 훑어 처음 윗면이 나오는 곳이 가장자리다. 폭 10cm 미만 윗면(난간·가는 배관)도 놓치지 않게 5cm 간격.
            float edge = -1;
            for (float d = .05f; d <= distance + .05f; d += .05f)
                if (SurfaceAt(origin + direction * d, top, self, out _)) { edge = d; break; }
            if (edge < 0) return Plan.None;
            if (edge > EdgeReach) {
                // 가장자리 앞 바닥으로 다가간다. 단, 지금보다 0.3m 이상 가까워지는 바닥이 있을 때만(같은 자리로 다가가기를 반복하던 문제).
                var stand = origin + direction * (edge - StepOut);
                var lip = origin + direction * edge;
                if (NavMesh.SamplePosition(stand, out var floor, 1.2f, areaMask) && Mathf.Abs(floor.position.y - origin.y) <= .45f
                    && Flat(floor.position - lip) < edge - .3f) {
                    point = floor.position;
                    return Plan.Approach;
                }
                // 물체 아래쪽이 넓게 튀어나와 더 다가갈 바닥이 없으면(기계·배관 받침) 이 자리에서 오른다.
                if (edge > MaxDirectReach) return Plan.None;
            }
            float inward = Mathf.Max(edge + .15f, Mathf.Min(edge + .45f, distance - .35f));
            // 넓은 윗면은 가장자리 안쪽, 좁은 윗면(배관·난간 위)은 대상이 선 자리나 가장자리 바로 위에 선다.
            if (!SurfaceAt(origin + direction * inward, top, self, out var land)
                && !(distance >= edge && SurfaceAt(origin + direction * distance, top, self, out land))
                && !SurfaceAt(origin + direction * (edge + .05f), top, self, out land)) return Plan.None;
            point = land.point;
            // 서서 뻗을 기둥(몸 두께), 가장자리를 넘는 공간, 윗면에 설 공간.
            var column = new Vector3(origin.x, top + LipClearance, origin.z);
            if (!BodyOpen(origin + Vector3.up * BodyLift, column, self) || !Open(column, point + Vector3.up * LipClearance, self)
                || !StandingRoom(point, self)) return Plan.None;
            return Plan.Climb;
        }

        /// <summary>
        /// 내려갈 바닥. toward가 있으면 그 근처(대상이 내려간 곳)를 먼저, 없으면 가장 가까운 아래 NavMesh, 그래도 없으면 8방향 가장자리 밖 바닥.
        /// 항상 아래쪽이며, 윗면 높이에서 착지점 위까지 수평으로 나간 뒤 수직으로 내려가는 공간이 몸 두께로 비어 있어야 한다.
        /// </summary>
        public static bool FindDrop(Vector3 from, Vector3? toward, int areaMask, Transform self, out Vector3 landing)
        {
            landing = default;
            if (toward.HasValue && Below(from, toward.Value, areaMask, self, out landing)) return true;
            if (Below(from, from, areaMask, self, out landing)) return true;
            float best = float.PositiveInfinity; bool found = false;
            foreach (var direction in DropDirections)
                foreach (float d in DropReach) {
                    var probe = from + direction * d + Vector3.up * .3f;
                    int count = Physics.RaycastNonAlloc(probe, Vector3.down, hits, MaxDropProbe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    float nearest = float.PositiveInfinity; Vector3 floor = default;
                    for (int i = 0; i < count; i++)
                        if (!Ignored(hits[i].collider, self) && hits[i].distance < nearest && hits[i].point.y < from.y - .3f) { nearest = hits[i].distance; floor = hits[i].point; }
                    if (float.IsPositiveInfinity(nearest) || nearest >= best) continue;
                    if (!NavMesh.SamplePosition(floor, out var hit, .5f, areaMask) || !DropClear(from, hit.position, self)) continue;
                    best = nearest; landing = hit.position; found = true;
                    break;
                }
            return found;
        }
        static readonly float[] DropReach = { 1f, 1.6f, 2.4f, 3.2f };
        const float MaxDropProbe = 30f;

        static bool Below(Vector3 from, Vector3 probe, int areaMask, Transform self, out Vector3 landing)
        {
            landing = default;
            if (!PursuitNavigation.TrySample(probe, areaMask, out var point)) return false;
            var flat = point - from; flat.y = 0;
            if (from.y - point.y < .3f || flat.magnitude > 3.5f) return false;
            if (!DropClear(from, point, self)) return false;
            landing = point;
            return true;
        }

        /// <summary>윗면에서 착지점 위까지 수평으로 나가고(머리·몸 높이) 거기서 착지점까지 내려갈 공간.</summary>
        public static bool DropClear(Vector3 from, Vector3 landing, Transform self)
        {
            var over = new Vector3(landing.x, from.y + 1.2f, landing.z);
            return Open(from + Vector3.up * 1.2f, over, self) && Open(from + Vector3.up * .5f, new Vector3(landing.x, from.y + .5f, landing.z), self)
                && BodyOpen(over, landing + Vector3.up * BodyLift, self);
        }

        /// <summary>몸 두께(<see cref="BodyCheckRadius"/>)로 두 점 사이가 비어 있는지. 시작점에서 이미 겹친 지형도 막힘으로 본다.</summary>
        public static bool BodyOpen(Vector3 from, Vector3 to, Transform self)
        {
            int inside = Physics.OverlapSphereNonAlloc(from, BodyCheckRadius, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < inside; i++) if (!Ignored(overlaps[i], self)) return false;
            var direction = to - from;
            float length = direction.magnitude;
            if (length < .001f) return true;
            int count = Physics.SphereCastNonAlloc(from, BodyCheckRadius, direction / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!Ignored(hits[i].collider, self)) return false;
            return true;
        }

        /// <summary>윗면 위 from에서 to 쪽으로 걸어 윗면이 끝나는 곳까지의 수평 거리(끝이 없으면 수평 거리 전체).</summary>
        public static float EdgeDistance(Vector3 from, Vector3 to, Transform self)
        {
            var flat = to - from; flat.y = 0;
            float span = flat.magnitude;
            if (span < .05f) return 0;
            var direction = flat / span;
            for (float d = .05f; d <= span; d += .05f)
                if (!SurfaceAt(from + direction * d, from.y, self, out _)) return d - .05f;
            return span;
        }

        /// <summary>height 근처(±0.3m)의 서 있을 수 있는 정적 윗면.</summary>
        public static bool SurfaceAt(Vector3 point, float height, Transform self, out RaycastHit surface)
        {
            surface = default;
            var from = new Vector3(point.x, height + .5f, point.z);
            int count = Physics.RaycastNonAlloc(from, Vector3.down, hits, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++) {
                var h = hits[i];
                if (Ignored(h.collider, self) || (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic)) continue;
                if (h.distance >= best) continue;
                best = h.distance; surface = h;
            }
            return surface.collider != null && surface.normal.y >= .65f && Mathf.Abs(surface.point.y - height) <= SurfaceTolerance;
        }

        /// <summary>두 점 사이에 지형이 없는지. 플레이어·추격자는 지형이 아니다.</summary>
        public static bool Open(Vector3 from, Vector3 to, Transform self)
        {
            var direction = to - from;
            float length = direction.magnitude;
            if (length < .001f) return true;
            int count = Physics.RaycastNonAlloc(from, direction / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!Ignored(hits[i].collider, self)) return false;
            return true;
        }

        public static bool StandingRoom(Vector3 feet, Transform self)
        {
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (BodyRadius + .1f), feet + Vector3.up * BodyHeight, BodyRadius,
                overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!Ignored(overlaps[i], self)) return false;
            return true;
        }

        internal static bool Ignored(Collider c, Transform self)
            => c == null || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<PursuitActor>() != null
               || c.GetComponentInParent<NetworkPlayer>() != null || c.GetComponentInParent<PlayerMover>() != null;
    }
}
