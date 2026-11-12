using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        // 고정 목표(순회지·소리·복귀)는 목표가 바뀌거나 NavMesh가 바뀌었거나 끼였을 때만 1회 계산한다.
        // 추격은 대상이 충분히 움직였을 때만, 추격자마다 어긋난 주기로 다시 계산한다.
        const float GoalTolerance = .35f, ChaseRepathSeconds = .4f, RepathJitter = .25f;
        // 대상이 멀수록 작은 이동은 경로를 거의 바꾸지 않는다. 거리의 15%(0.3~3m) 이상 움직였을 때만 다시 계산한다.
        const float ChaseRepathMinMove = .3f, ChaseRepathMaxMove = 3f, ChaseRepathMoveFraction = .15f;
        const float PatrolRetrySeconds = 1f;
        // 순회지가 막혔을 때 조정 지점이 이보다 멀면 근방이 아니므로 그 순회지는 건너뛴다.
        const float RouteAdjustRadius = 5f;
        // 이동 의도 대비 실제 이동이 이 비율보다 적은 시간이 쌓이면 정지로 본다.
        const float StuckSpeedFraction = .25f, StuckRecoverSeconds = 1f, StuckGiveUpSeconds = 3f;
        const float PathCostSeconds = .75f;

        // NavMeshPath는 네이티브 객체라 MonoBehaviour 필드 초기화(생성자)에서 만들 수 없다. 처음 쓸 때 만든다.
        NavMeshPath navPathStore, costPathStore;
        NavMeshPath navPath => navPathStore ??= new NavMeshPath();
        NavMeshPath costPath => costPathStore ??= new NavMeshPath();
        readonly Dictionary<GameObject, (float until, float cost)> pathCosts = new();
        Vector3 requestedGoal, navGoal, stuckSample;
        bool hasNavGoal, navFailed, stuckRecovered, replanRequested, navDeferred;
        float stuckFor, patrolRetryAt;
        int patrolSkips, navVersion;

        /// <summary>현재 목표의 조정된 도달 지점. 막힌 목표는 가장 가까운 도달 가능 지점이다.</summary>
        public Vector3 NavigationGoal => navGoal;
        public bool NavigationFailed => navFailed;

        bool SameGoal(Vector3 p) => hasNavGoal && (p - requestedGoal).sqrMagnitude <= GoalTolerance * GoalTolerance;

        static float Jittered(float seconds) => seconds * Random.Range(1 - RepathJitter, 1 + RepathJitter);

        /// <summary>
        /// 동기 최단 경로로 이동한다. 도달할 지점이 없으면 false.
        /// 이번 프레임 계산 예산이 없으면 기존 경로를 유지하고 다음 프레임으로 미룬다(<see cref="navDeferred"/>).
        /// </summary>
        bool Navigate(Vector3 p, bool movingTarget = false)
        {
            navDeferred = false;
            bool same = SameGoal(p);
            bool plan = !hasNavGoal || replanRequested || navVersion != PursuitNavigation.Version;
            // 경계·청취 중 Stop()으로 지운 경로와 장애물 변경으로 낡은 경로는 같은 목표라도 다시 찾는다.
            if (!plan && !navFailed && !Reached(p)) plan = !agent.hasPath || agent.isPathStale;
            if (!plan && !movingTarget) plan = !same;
            if (!plan && movingTarget && Time.time >= repathAt) {
                // 가까울수록 작은 이동에도 다시 계산해 포획 거리 안에서 엉뚱한 곳을 향하지 않게 한다.
                float threshold = Mathf.Clamp(Vector3.Distance(transform.position, p) * ChaseRepathMoveFraction, ChaseRepathMinMove, ChaseRepathMaxMove);
                // 막혀 포기한 추격은 대상이 그대로여도 주기마다 다시 길을 찾는다.
                plan = navFailed || (p - requestedGoal).sqrMagnitude >= threshold * threshold;
            }
            if (!plan) return !navFailed;
            if (agent.isOnOffMeshLink) return true;
            if (!PursuitNavigation.HasBudget) { navDeferred = !same; return !navFailed || !same; }
            if (!same) { stuckFor = 0; stuckRecovered = false; }
            repathAt = movingTarget ? Time.time + Jittered(ChaseRepathSeconds) : 0;
            requestedGoal = p; hasNavGoal = true; replanRequested = false; navVersion = PursuitNavigation.Version;
            navFailed = !PursuitNavigation.TryPlan(agent.nextPosition, p, agent.areaMask, navPath, out navGoal);
            if (navFailed) { if (agent.hasPath) agent.ResetPath(); return false; }
            navFailed = !agent.SetPath(navPath);
            return !navFailed;
        }

        void ClearNavigation()
        {
            hasNavGoal = navFailed = stuckRecovered = replanRequested = navDeferred = false;
            stuckFor = 0; patrolSkips = 0; repathAt = patrolRetryAt = 0;
            pathCosts.Clear();
        }

        // 조정된 목표에 도착했는지. 근방으로 옮긴 순회지·소리 지점도 원래 좌표 대신 조정 지점으로 판정한다.
        bool Reached(Vector3 p)
        {
            var goal = SameGoal(p) && !navFailed ? navGoal : p;
            var delta = goal - transform.position;
            return Mathf.Abs(delta.y) < 1.2f && new Vector2(delta.x, delta.z).sqrMagnitude < .7f * .7f;
        }
        bool Unreachable(Vector3 p) => navFailed && SameGoal(p);
        bool AtPartialEnd
        {
            get {
                if (!hasNavGoal || navFailed || navPathStore == null || navPathStore.status != NavMeshPathStatus.PathPartial) return false;
                // 막힌 길 끝에는 여러 추격자가 몰리므로 끝점 근처에 모인 것도 도착으로 본다.
                var delta = navGoal - transform.position;
                return Mathf.Abs(delta.y) < 1.2f && new Vector2(delta.x, delta.z).sqrMagnitude < 1.5f * 1.5f;
            }
        }
        void Stop() { if (agent.enabled && agent.isOnNavMesh && agent.hasPath) agent.ResetPath(); }

        bool EnsureOnNavMesh()
        {
            if (agent.enabled) return agent.isOnNavMesh;
            if (!SafeNavMeshPoint(transform.position, out var point)) {
                // 가까이 안전한 NavMesh가 없으면(높은 윗면 위) 내려갈 바닥을 찾아 뛰어내린다.
                if (Time.time >= dropPlanAt) {
                    dropPlanAt = Time.time + .5f;
                    if (PursuitDirectClimb.FindDrop(transform.position, null, NavMesh.AllAreas, transform, out var landing)) StartDirect(landing, false);
                }
                return false;
            }
            transform.position = point; agent.enabled = true;
            return agent.isOnNavMesh;
        }

        static readonly float[] SafeSampleRadii = { .6f, 2f, 4f };

        /// <summary>
        /// 몸이 지나갈 수 있는 가까운 NavMesh 지점. 위로 0.6m 넘게 올라가거나(위층·천장 너머) 벽을 사이에 둔 지점으로는 옮기지 않는다.
        /// 예전에는 8m 안의 아무 NavMesh로 옮겨 탱크 위에서 위층으로, 벽 너머 방으로 순간이동했다.
        /// </summary>
        bool SafeNavMeshPoint(Vector3 position, out Vector3 point)
        {
            point = position;
            foreach (float radius in SafeSampleRadii) {
                if (!NavMesh.SamplePosition(position, out var hit, radius, NavMesh.AllAreas)) continue;
                var p = hit.position;
                if (p.y > position.y + .6f) continue;
                bool level = Mathf.Abs(p.y - position.y) < .6f;
                if (level ? !PursuitDirectClimb.Open(position + Vector3.up * .8f, p + Vector3.up * .8f, transform) : !PursuitDirectClimb.DropClear(position, p, transform)) continue;
                point = p;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 벽·소품·다른 추격자에 막혀 이동하지 못하면 NavMesh에 다시 올려 재경로하고, 그래도 막히면 목표를 포기한다.
        /// </summary>
        void TickStuck()
        {
            var position = transform.position;
            var moved = position - stuckSample; moved.y = 0;
            stuckSample = position;
            bool moving = agent.hasPath && !agent.pathPending && !agent.isOnOffMeshLink && agent.speed > .1f
                && agent.remainingDistance > agent.stoppingDistance + .4f;
            if (!moving) { stuckFor = 0; return; }
            float expected = agent.speed * Time.deltaTime;
            if (moved.magnitude >= expected * StuckSpeedFraction) { stuckFor = Mathf.Max(0, stuckFor - Time.deltaTime); return; }
            stuckFor += Time.deltaTime;
            if (stuckFor >= StuckGiveUpSeconds) {
                // 순회는 다음 지점, 소리·수색은 종료, 추격은 다음 재계산에서 다시 경로를 찾는다.
                navFailed = true; stuckFor = 0; stuckRecovered = false;
                agent.ResetPath();
                return;
            }
            if (stuckFor >= StuckRecoverSeconds && !stuckRecovered) {
                stuckRecovered = true;
                if (PursuitNavigation.TrySample(position, agent.areaMask, out var point)) agent.Warp(point);
                // 같은 목표를 유지해야 포기 단계까지 누적된다. 재계산만 요청한다.
                replanRequested = true;
            }
        }

        /// <summary>최인접 추격에 쓰는 NavMesh 경로 거리. 벽 너머 직선 거리 대신 실제 이동 거리로 대상을 고른다.</summary>
        internal float PathCost(GameObject player)
        {
            if (player == null) return float.PositiveInfinity;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return Vector3.Distance(transform.position, player.transform.position);
            bool hasCached = pathCosts.TryGetValue(player, out var cached);
            if (hasCached && Time.time < cached.until) return cached.cost;
            // 예산이 없으면 지난 값(없으면 직선 거리)으로 고르고 다음 프레임에 갱신한다.
            if (!PursuitNavigation.HasBudget) return hasCached ? cached.cost : Vector3.Distance(transform.position, player.transform.position);
            float cost = PursuitNavigation.PathCost(agent.nextPosition, player.transform.position, agent.areaMask, costPath);
            // NavMesh 밖(점프·높은 소품 위)이라 경로가 없어도 최인접 추격 대상에서는 빠지지 않고 가장 뒤 순위가 된다.
            if (float.IsPositiveInfinity(cost)) cost = 2 * PursuitNavigation.PartialPathPenalty + Vector3.Distance(transform.position, player.transform.position);
            pathCosts[player] = (Time.time + Jittered(PathCostSeconds), cost);
            return cost;
        }

        // 순회 복귀 지점은 직선이 아니라 경로 거리로 가장 가까운 순회지를 고른다.
        Vector3 NearestPatrolByPath(Transform[] points, out int index)
        {
            index = waypoint;
            var best = initialPosition;
            float bestCost = float.PositiveInfinity;
            if (points != null) for (int i = 0; i < points.Length; i++) {
                if (points[i] == null) continue;
                float cost = PursuitNavigation.PathCost(agent.nextPosition, points[i].position, agent.areaMask, costPath);
                if (cost >= bestCost) continue;
                bestCost = cost; best = points[i].position; index = i;
            }
            return best;
        }
    }
}
