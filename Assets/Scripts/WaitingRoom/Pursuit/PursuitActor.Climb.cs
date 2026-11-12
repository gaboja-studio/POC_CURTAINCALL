using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 오르기 통로(<see cref="PursuitClimbLinks"/>) 이동. 자동 통과 대신 오르기(수직 → 가장자리 넘기)·뛰어내리기를 직접 움직인다.
    /// 오르내리는 동안은 <see cref="PursuitMotion.Climb"/>이며 포획하지 않는다.
    /// </summary>
    public sealed partial class PursuitActor
    {
        PursuitClimbIk climbIk;
        PursuitClimbPath climbPath;
        bool remoteTraversal;
        public Vector3 TraversalFrom => climbFrom;
        public Vector3 TraversalTo => climbTo;
        public float TraversalDuration => climbDuration;
        public float TraversalProgress => Mathf.Clamp01((Time.time - climbStarted) / Mathf.Max(.1f, climbDuration));
        public int ClimbIkContacts => climbIk?.ActiveContacts ?? 0;
        public float ClimbIkError => climbIk?.MaxContactError ?? 0;

        // 상태 패킷의 구간/진행률로 늦게 합류한 복제본도 같은 접점을 찾는다. 루트 보간은 기존 포즈 버퍼가 담당한다.
        public void ReceiveTraversal(Vector3 from, Vector3 to, float progress, float duration)
        {
            if (!remote || !IsTraversalMotion(State)) return;
            if (!float.IsFinite(duration) || !float.IsFinite(progress) || duration < .1f) return;
            bool changed = !remoteTraversal || (climbFrom - from).sqrMagnitude > .001f || (climbTo - to).sqrMagnitude > .001f;
            climbFrom = from; climbTo = to; climbUp = State == PursuitMotion.Climb;
            climbDuration = duration; climbStarted = Time.time - Mathf.Clamp01(progress) * duration;
            remoteTraversal = true;
            if (changed) BeginTraversalIk();
            var state = Motion != null ? Motion[State.ToString()] : null;
            if (state != null) { state.speed = state.length / duration; state.normalizedTime = ClipProgress(progress); }
        }

        // 내려오기에서 가장자리까지 걷는 비율. 호스트는 경로에서, 원격은 같은 충돌체·구간·시간으로 다시 계산한다.
        float climbWalk;

        void BeginTraversalIk()
        {
            var rules = GameSettings.WaitingRoom.Pursuit;
            if (climbUp) { climbWalk = 0; climbIk?.Begin(climbFrom, climbTo, true, rules); return; }
            float edge = PursuitDirectClimb.EdgeDistance(climbFrom, climbTo, transform);
            climbWalk = climbDuration > .1f ? Mathf.Clamp01(edge / PursuitClimbPath.DropWalkSpeed / climbDuration) : 0;
            var flat = Vector3.ProjectOnPlane(climbTo - climbFrom, Vector3.up).normalized;
            // 손발 접점은 걸어간 가장자리에서 뛰어내리는 구간 기준.
            climbIk?.Begin(climbFrom + flat * edge, climbTo, false, rules, climbWalk);
        }

        /// <summary>통과 진행률 → 클립 정규 시간. 오르기는 벽 타기 구간, 내려오기는 가장자리까지 걷는 구간을 뺀다.</summary>
        float ClipProgress(float progress) => climbUp ? PursuitClimbPath.ClipTime(progress, climbTo.y - climbFrom.y) : PursuitClimbPath.DropPhase(progress, climbWalk);

        // 벽 타기(2.5m 초과 오르기)·가장자리 걷기(내려오기)가 있으면 클립을 일정 속도로 틀지 않고 진행률에 맞춘 클립 시간으로 직접 둔다(호스트·원격 같음).
        void SyncClimbClip()
        {
            if (!IsTraversalMotion(State) || !(climbing || remoteTraversal) || Motion == null) return;
            if (climbUp ? PursuitClimbPath.WallFraction(climbTo.y - climbFrom.y) <= 0 : climbWalk <= 0) return;
            var state = Motion[State.ToString()];
            if (state == null) return;
            state.speed = 0;
            state.normalizedTime = ClipProgress(TraversalProgress);
        }

        bool climbing, climbUp;
        float climbStarted, climbDuration;
        Vector3 climbFrom, climbTo;
        int climbMask = NavMesh.AllAreas;
        // 직접 오르기: NavMesh 링크 없이 소품 윗면으로 오르내림(agent 꺼짐). perched는 NavMesh 밖 윗면에 서 있는 상태.
        bool directTraversal, perched;
        float directPlanAt, perchSeenAt, perchIdleFor;
        bool hasDirectApproach;
        Vector3 directApproach;
        public bool IsClimbing => climbing;
        public bool IsPerched => perched;
        public static bool IsTraversalMotion(PursuitMotion motion) => motion == PursuitMotion.Climb || motion == PursuitMotion.Drop;
        internal int AgentTypeID => agent != null ? agent.agentTypeID : 0;
        /// <summary>Blender 오르기 클립을 맞춘 기준 높이. 더 높은 곳은 같은 속도로 보이게 시간을 늘린다.</summary>
        public const float ClimbReferenceHeight = 1.5f;
        /// <summary>
        /// 오르기: 높이에 비례(2.5m 초과분은 같은 속도의 벽 타기, <see cref="PursuitClimbPath.WallFraction"/>).
        /// 내려오기: 2.5m까지 높이에 비례, 그 위는 낙하처럼 높이의 제곱근에 비례해 높은 곳에서 느리게 떠 보이지 않게 한다.
        /// </summary>
        public static float TraversalSeconds(PursuitSettings rules, bool up, float height)
        {
            float h = Mathf.Abs(height), scale = Mathf.Max(1, h / ClimbReferenceHeight);
            if (!up && h > PursuitClimbPath.WallClimbFrom)
                scale = PursuitClimbPath.WallClimbFrom / ClimbReferenceHeight * Mathf.Sqrt(h / PursuitClimbPath.WallClimbFrom);
            return Mathf.Max(.1f, up ? rules.ClimbSeconds : rules.DropSeconds) * scale;
        }
        bool ClimbAllowed {
            get {
                int from = GameSettings.WaitingRoom.Pursuit.ClimbFromStage;
                return from > 0 && director.CurrentStageIndex + 1 >= from;
            }
        }

        /// <summary>현재 단계에 맞춰 오르기 구역 사용을 켜고 끈다. 바뀌면 경로·대상 거리를 다시 계산한다.</summary>
        void ApplyClimbArea()
        {
            bool allowed = ClimbAllowed;
            int mask = allowed ? NavMesh.AllAreas : NavMesh.AllAreas & ~PursuitClimbLinks.AreaMask;
            if (climbMask == mask && agent.areaMask == mask) return;
            climbMask = mask; agent.areaMask = mask;
            replanRequested = true; pathCosts.Clear();
        }

        /// <summary>오르기 통로 위면 이동을 진행하고 true. 이번 틱에는 행동 판단·포획을 하지 않는다.</summary>
        bool TickClimb()
        {
            if (!climbing) {
                if (!agent.isOnOffMeshLink) return false;
                var link = agent.currentOffMeshLinkData;
                // 낮은 계단 통로(씬의 StepLink 등)는 기존처럼 걸어서 지난다.
                if (!IsClimbLink(link)) {
                    WalkLink(link); return false;
                }
                var rules = GameSettings.WaitingRoom.Pursuit;
                climbFrom = transform.position; climbTo = link.endPos;
                climbUp = climbTo.y > climbFrom.y;
                BeginTraversal(rules);
                SetMotion(climbUp ? PursuitMotion.Climb : PursuitMotion.Drop, true);
            }
            float t = Mathf.Clamp01((Time.time - climbStarted) / climbDuration);
            transform.position = TraversalPose(t);
            Face(climbTo);
            if (t < 1) return true;
            FinishClimb();
            return false;
        }

        Vector3 TraversalPose(float t) => climbPath != null ? climbPath.Pose(t) : ClimbPose(climbFrom, climbTo, climbUp, t);

        // climbFrom/To/Up을 정한 뒤 경로·시간·손발 접점을 준비한다. 내려오기는 가장자리까지 걷는 시간이 더해진다.
        void BeginTraversal(PursuitSettings rules)
        {
            float seconds = TraversalSeconds(rules, climbUp, climbTo.y - climbFrom.y);
            climbPath = climbUp ? PursuitClimbPath.Build(climbFrom, climbTo, transform) : PursuitClimbPath.BuildDrop(climbFrom, climbTo, transform, seconds);
            climbDuration = climbUp ? seconds : climbPath.Seconds;
            climbStarted = Time.time; climbing = true;
            BeginTraversalIk();
        }

        void FinishClimb()
        {
            climbing = false; climbPath = null;
            climbIk?.Clear();
            transform.position = climbTo;
            if (directTraversal) { directTraversal = false; Land(); return; }
            if (agent.enabled && agent.isOnOffMeshLink) agent.CompleteOffMeshLink();
            replanRequested = true; stuckFor = 0;
        }

        void ResetClimb()
        {
            climbing = false; remoteTraversal = false; climbPath = null;
            directTraversal = perched = hasDirectApproach = hasDropLanding = false;
            perchIdleFor = noDropFor = dropPlanAt = 0; climbWalk = 0;
            ResetDetour();
            climbIk?.Clear();
            climbMask = NavMesh.AllAreas;
        }

        // ---- 직접 오르기(NavMesh 밖 소품 윗면) ----

        /// <summary>
        /// 추격 중 대상이 길로 닿지 않는 소품 위에 서 있으면 가장자리로 다가가 직접 오른다. 올랐으면 true.
        /// 가장자리가 멀면 <see cref="directApproach"/>를 다음 이동 목표로 둔다.
        /// </summary>
        bool TryDirectClimb(bool routed)
        {
            if (target == null || !ClimbAllowed || agent.isOnOffMeshLink) { hasDirectApproach = false; return false; }
            var goal = target.position;
            float rise = goal.y - transform.position.y;
            // 길로 대상 발밑까지 닿거나(링크 포함) 이미 잡을 수 있으면 직접 오르지 않는다.
            var gap = navGoal - goal; float gapY = gap.y; gap.y = 0;
            bool pathReaches = routed && !navFailed && Mathf.Abs(gapY) < .5f && gap.magnitude < 1.2f;
            if (rise < .45f || pathReaches || WithinCaptureReach(goal)) { hasDirectApproach = false; return false; }
            if (Time.time < directPlanAt) return false;
            directPlanAt = Time.time + .15f;
            var plan = PursuitDirectClimb.Find(transform.position, goal, transform, GameSettings.WaitingRoom.Pursuit.ResolvedClimbMaxHeight, agent.areaMask, out var point);
            // 우회 중이거나 이미 막혔던 가장자리 앞이면 그쪽으로 다시 가지 않는다(바로 오를 수 있으면 오른다).
            hasDirectApproach = plan == PursuitDirectClimb.Plan.Approach && !hasDetour && !IsBlocked(point);
            if (hasDirectApproach) directApproach = point;
            if (plan != PursuitDirectClimb.Plan.Climb) return false;
            StartDirect(point, true);
            return true;
        }

        void StartDirect(Vector3 to, bool up)
        {
            if (agent.enabled) { if (agent.isOnNavMesh && agent.hasPath) agent.ResetPath(); agent.enabled = false; }
            climbFrom = transform.position; climbTo = to; climbUp = up;
            directTraversal = true; perched = false; hasDirectApproach = false;
            BeginTraversal(GameSettings.WaitingRoom.Pursuit);
            SetMotion(up ? PursuitMotion.Climb : PursuitMotion.Drop, true);
        }

        // 착지점이 NavMesh면 길찾기로 돌아가고, 아니면 소품 위에 머문다.
        void Land()
        {
            if (NavMesh.SamplePosition(climbTo, out var hit, .45f, agent.areaMask) && Mathf.Abs(hit.position.y - climbTo.y) < .3f) {
                perched = false;
                transform.position = hit.position;
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(hit.position);
                ClearNavigation();
                return;
            }
            perched = true; perchSeenAt = Time.time; perchIdleFor = 0;
        }

        /// <summary>agent를 끈 직접 오르내리기·소품 위 추격. 이번 틱을 처리했으면 true.</summary>
        bool TickDirect()
        {
            if (climbing && directTraversal) {
                float t = Mathf.Clamp01((Time.time - climbStarted) / climbDuration);
                transform.position = TraversalPose(t);
                Face(climbTo);
                if (t >= 1) FinishClimb();
                return true;
            }
            if (!perched) return false;
            TickPerch();
            return true;
        }

        // 소품 위: 대상이 같은 윗면에 있으면 따라 걸으며 잡고, 내려갔거나 놓쳤으면 뛰어내린다.
        void TickPerch()
        {
            rule = director.CurrentStageRules;
            var next = director.NearestVisible(this, rule);
            visible = next != null;
            if (visible) { target = next.transform; lastSeen = target.position; unseen = 0; } else unseen += Time.deltaTime;
            bool keep = target != null && director.IsAvailable(target.gameObject)
                && (rule.AlwaysNearest || unseen <= GameSettings.WaitingRoom.Pursuit.LoseTargetSeconds);
            if (keep) {
                var delta = target.position - transform.position; float rise = delta.y; delta.y = 0;
                // 짧은 점프는 같은 윗면으로 본다.
                if (rise > -.5f && rise < 1.2f && delta.magnitude < 6) perchSeenAt = Time.time;
                if (Time.time - perchSeenAt < .6f && perchIdleFor < 1.5f) {
                    EnterBehaviour(PursuitBehaviour.Chase);
                    bool moved = StepOnSurface(target.position, rule.ResolvedChaseSpeed);
                    perchIdleFor = moved || WithinCaptureReach(target.position) ? 0 : perchIdleFor + Time.deltaTime;
                    SetMotion(moved ? PursuitMotion.Run : PursuitMotion.Awaken);
                    Face(target.position);
                    director.CaptureInReach(this, target.gameObject);
                    return;
                }
            }
            if (!keep) target = null;
            if (Time.time >= dropPlanAt) {
                dropPlanAt = Time.time + .3f;
                hasDropLanding = PursuitDirectClimb.FindDrop(transform.position, keep ? target.position : (Vector3?)null, NavMesh.AllAreas, transform, out dropLanding);
            }
            if (hasDropLanding) {
                noDropFor = 0;
                // 윗면 가장자리까지 먼저 걸어간 뒤 뛰어내린다(가장자리가 멀면 걷기 동작으로).
                var flat = Vector3.ProjectOnPlane(dropLanding - transform.position, Vector3.up).normalized;
                float edge = PursuitDirectClimb.EdgeDistance(transform.position, dropLanding, transform);
                if (edge > .6f && StepOnSurface(transform.position + flat * (edge + .4f), Mathf.Max(1.5f, rule.ResolvedChaseSpeed))) {
                    SetMotion(PursuitMotion.Run); Face(dropLanding);
                    return;
                }
                hasDropLanding = false;
                StartDirect(dropLanding, false);
                return;
            }
            // 내려갈 곳이 없으면 바로 가까운 NavMesh로 옮기지 않는다(위층·벽 너머로 순간이동하던 문제). 잠시 기다리며 다시 찾는다.
            noDropFor += Time.deltaTime;
            SetMotion(PursuitMotion.Awaken);
            if (noDropFor >= NoDropGiveUpSeconds) { noDropFor = 0; perched = false; }
        }

        const float NoDropGiveUpSeconds = 4f;
        float dropPlanAt, noDropFor;
        bool hasDropLanding;
        Vector3 dropLanding;

        // 윗면을 벗어나거나 벽으로는 걷지 않는다.
        bool StepOnSurface(Vector3 goal, float speed)
        {
            var delta = goal - transform.position; delta.y = 0;
            float distance = delta.magnitude;
            if (distance < .45f) return false;
            var next = transform.position + delta / distance * Mathf.Min(distance - .4f, Mathf.Max(.5f, speed) * Time.deltaTime);
            if (!PursuitDirectClimb.SurfaceAt(next, transform.position.y, transform, out var hit)) return false;
            var p = transform.position;
            if (!PursuitDirectClimb.Open(p + Vector3.up * .5f, hit.point + Vector3.up * .5f, transform)
                || !PursuitDirectClimb.Open(p + Vector3.up * 1.3f, hit.point + Vector3.up * 1.3f, transform)) return false;
            transform.position = hit.point;
            return true;
        }

        static bool IsClimbLink(OffMeshLinkData link) => Mathf.Abs(link.endPos.y - link.startPos.y) >= PursuitClimbLinks.MinHeight - .05f;

        // 계단 통로(자동 통과를 끈 뒤): 일정 속도로 바로 걸어 지난다.
        void WalkLink(OffMeshLinkData link)
        {
            var next = Vector3.MoveTowards(transform.position, link.endPos, Mathf.Max(1, agent.speed) * Time.deltaTime);
            transform.position = next;
            if ((next - link.endPos).sqrMagnitude < .0004f) agent.CompleteOffMeshLink();
        }

        /// <summary>오르기: 앞 60% 동안 아래쪽 끝에서 가장자리 높이까지 올라가고 나머지 동안 넘어간다. 내려오기: 작은 포물선.</summary>
        public static Vector3 ClimbPose(Vector3 from, Vector3 to, bool up, float t)
        {
            if (up) {
                const float rise = .6f;
                if (t < rise) return new Vector3(from.x, Mathf.Lerp(from.y, to.y + .1f, Mathf.SmoothStep(0, 1, t / rise)), from.z);
                var lip = new Vector3(from.x, to.y + .1f, from.z);
                return Vector3.Lerp(lip, to, Mathf.SmoothStep(0, 1, (t - rise) / (1 - rise)));
            }
            var p = Vector3.Lerp(from, to, t);
            p.y = Mathf.Lerp(from.y, to.y, t * t) + Mathf.Sin(t * Mathf.PI) * .25f;
            return p;
        }
    }
}
