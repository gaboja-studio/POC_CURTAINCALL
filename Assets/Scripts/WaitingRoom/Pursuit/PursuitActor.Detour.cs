using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격 경로가 막혀(난간·좁은 윗면·오를 수 없는 가장자리 등) 대상에 닿지 못한 채 제자리에 머물면
    /// 그 접근 지점을 잠시 막힘으로 기록하고 대상 주변의 다른 접근 지점(다른 쪽 가장자리·계단 위 등)으로 돌아간다.
    /// 2026-11-12 사용자 보고: 계단 난간에 막혀 벽에 비비며 계속 달림.
    /// </summary>
    public sealed partial class PursuitActor
    {
        /// <summary>이 시간 동안 이 거리보다 적게 움직이고 대상도 잡지 못하면 막힌 것으로 본다.</summary>
        public const float StallSeconds = 1.5f, StallMove = .5f;
        /// <summary>막힌 접근 지점을 피하는 시간·반경, 우회 지점을 유지하는 최대 시간.</summary>
        const float BlockSeconds = 12f, BlockRadius = 1.2f, DetourSeconds = 8f;
        static readonly float[] DetourRadii = { 1.2f, 2.2f, 3.4f };
        const int DetourDirections = 10, DetourMaxPaths = 14;
        readonly List<(Vector3 point, float until)> blocked = new();
        Transform stallTarget;
        Vector3 stallAnchor, detour;
        float stallFor, detourUntil;
        bool hasDetour;
        NavMeshPath detourPathStore;
        public bool HasDetour => hasDetour;
        public Vector3 DetourPoint => detour;
        public int BlockedApproaches { get { Prune(); return blocked.Count; } }

        void ResetDetour() { blocked.Clear(); stallTarget = null; stallFor = 0; hasDetour = false; }

        void Prune() { for (int i = blocked.Count - 1; i >= 0; i--) if (Time.time >= blocked[i].until) blocked.RemoveAt(i); }

        bool IsBlocked(Vector3 p)
        {
            Prune();
            foreach (var (point, _) in blocked) {
                var d = p - point;
                if (Mathf.Abs(d.y) < 1.5f && new Vector2(d.x, d.z).sqrMagnitude < BlockRadius * BlockRadius) return true;
            }
            return false;
        }

        void Block(Vector3 p) => blocked.Add((p, Time.time + BlockSeconds));

        /// <summary>이번 틱 추격 목표. 우회 중이면 우회 지점, 아니면 fallback.</summary>
        Vector3 ChaseGoal(Vector3 fallback)
        {
            if (target != stallTarget) { ResetDetour(); stallTarget = target; stallAnchor = transform.position; }
            if (hasDetour && (Time.time > detourUntil || Reached(detour))) { hasDetour = false; stallAnchor = transform.position; stallFor = 0; }
            return hasDetour ? detour : fallback;
        }

        /// <summary>
        /// 대상에 닿지 못한 채 제자리면 현재 접근 지점을 막힘으로 기록하고 다른 접근 지점을 고른다. 이번 틱에 막힘을 처리했으면 true.
        /// </summary>
        bool TickStall()
        {
            if (target == null || WithinCaptureReach(target.position)) { stallFor = 0; stallAnchor = transform.position; return false; }
            var moved = transform.position - stallAnchor;
            if (moved.sqrMagnitude > StallMove * StallMove) { stallAnchor = transform.position; stallFor = 0; return false; }
            stallFor += Time.deltaTime;
            if (stallFor < StallSeconds) return false;
            stallFor = 0; stallAnchor = transform.position;
            Block(transform.position);
            if (hasNavGoal) Block(navGoal);
            if (hasDirectApproach) Block(directApproach);
            if (hasDetour) Block(detour);
            hasDirectApproach = false;
            hasDetour = FindDetour(target.position, out detour);
            if (hasDetour) { detourUntil = Time.time + DetourSeconds; replanRequested = true; }
            return true;
        }

        /// <summary>
        /// 대상 주변(여러 반경·방향)의 NavMesh 지점 중 막히지 않았고 끝까지 이어지는 경로가 있는 가장 가까운 곳.
        /// 대상과 같은 높이(계단 위·통로)면 우선, 아래 바닥이면 거기서 다시 오르기를 시도한다.
        /// </summary>
        bool FindDetour(Vector3 goal, out Vector3 best)
        {
            best = default;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            return FindDetour(agent.nextPosition, goal, agent.areaMask, IsBlocked, transform, detourPathStore ??= new NavMeshPath(), out best);
        }

        public static bool FindDetour(Vector3 from, Vector3 goal, int areaMask, System.Func<Vector3, bool> blocked, Transform self, NavMeshPath path, out Vector3 best)
        {
            best = default;
            float bestCost = float.PositiveInfinity;
            int paths = 0;
            foreach (float radius in DetourRadii)
                for (int k = 0; k < DetourDirections && paths < DetourMaxPaths; k++) {
                    var direction = Quaternion.Euler(0, k * 360f / DetourDirections + radius * 17, 0) * Vector3.forward;
                    if (!Ground(goal + direction * radius, goal.y, areaMask, self, out var p)) continue;
                    if ((blocked != null && blocked(p)) || (p - from).sqrMagnitude < 1) continue;
                    paths++;
                    if (!NavMesh.CalculatePath(from, p, areaMask, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float cost = PursuitNavigation.Length(path.corners) + Mathf.Abs(goal.y - p.y) * 2 + radius;
                    if (cost >= bestCost) continue;
                    bestCost = cost; best = p;
                }
            return !float.IsPositiveInfinity(bestCost);
        }

        static readonly RaycastHit[] groundHits = new RaycastHit[8];

        // 대상 높이 근처 NavMesh, 없으면 그 아래 첫 바닥의 NavMesh(오를 가장자리 앞). 대상보다 1m 넘게 높은 곳은 쓰지 않는다.
        static bool Ground(Vector3 probe, float height, int areaMask, Transform self, out Vector3 point)
        {
            point = default;
            if (NavMesh.SamplePosition(probe, out var hit, .8f, areaMask) && hit.position.y <= height + 1) { point = hit.position; return true; }
            int count = Physics.RaycastNonAlloc(probe + Vector3.up * .5f, Vector3.down, groundHits, 12, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (groundHits[i].distance < nearest && !PursuitDirectClimb.Ignored(groundHits[i].collider, self)) { nearest = groundHits[i].distance; point = groundHits[i].point; }
            if (float.IsPositiveInfinity(nearest) || !NavMesh.SamplePosition(point, out hit, .6f, areaMask)) return false;
            point = hit.position;
            return true;
        }
    }
}
