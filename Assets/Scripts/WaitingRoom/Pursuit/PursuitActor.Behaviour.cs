using CurtainCall.Settings;
using Unity.Behavior;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public enum PursuitBehaviour { Patrol, Listen, Investigate, Prepare, Chase, Search, Capture, Return }

    public sealed partial class PursuitActor
    {
        public PursuitBehaviour Behaviour { get; private set; }
        public bool GraphReady => brain != null && brain.Graph != null;
        BehaviorGraphAgent brain;
        PursuitStage rule;
        bool visible, sensingLook, investigating, searching, restartBrain;
        internal bool ContactTargetVisible => visible;
        Vector3 sensingPoint, scanLook;
        float prepareUntil, noiseUntil, searchUntil, nextNoiseAt, scanStarted;
        Vector3 captureStart;
        Quaternion captureRotation;

        void InitializeBrain()
        {
            brain = gameObject.AddComponent<BehaviorGraphAgent>();
            // Only the director's authority tick runs this graph, never the replica's Unity Update.
            brain.enabled = false;
            if (director.BehaviourGraph == null) { Debug.LogError("Pursuit Behavior graph is not assigned", director); return; }
            brain.Graph = director.BehaviourGraph;
            brain.Init();
        }
        void ResetBrain()
        {
            brain?.End();
            restartBrain = true;
            RestoreRenderers();
            sensingLook = investigating = searching = false;
            returningToPatrol = false;
            noiseUntil = searchUntil = prepareUntil = nextNoiseAt = repathAt = 0;
            ClearNavigation();
            Behaviour = PursuitBehaviour.Patrol;
        }
        void TickBrain(PursuitStage stage)
        {
            rule = stage;
            TickPatrolBoundary();
            var next = returningToPatrol ? null : director.NearestVisible(this, rule);
            visible = next != null;
            if (visible) {
                if (target == null) prepareUntil = Time.time + Mathf.Max(0, rule.AlertSeconds);
                target = next.transform; lastSeen = target.position; unseen = 0;
                searching = investigating = false; noiseUntil = 0;
            } else unseen += Time.deltaTime;
            // 방 밖 추격이 꺼진 추격자는 대상이 방을 나가면 놓친다. 최인접 단계에서도 방 밖 마지막 지점에 서서 달리지 않는다.
            if (target != null && (!director.IsAvailable(target.gameObject)
                || (!Room.RoamAcrossRooms && !Room.Contains(target.position)) || (!rule.AlwaysNearest &&
                (unseen > GameSettings.WaitingRoom.Pursuit.LoseTargetSeconds || Vector3.Distance(transform.position, target.position) > Mathf.Max(rule.SightRadius, rule.TrackingRadius))))) {
                target = null; searching = true; sensingPoint = scanLook = lastSeen;
                searchUntil = Time.time + Mathf.Max(0, rule.SearchSeconds) + Mathf.Max(.1f, rule.InvestigateSeconds);
                scanStarted = -1;
            }
            sensingLook = target == null && (searching || Time.time < noiseUntil);
            if (GraphReady) {
                if (restartBrain) { brain.Graph.Restart(); restartBrain = false; }
                brain.Update();
            }
        }
        public void Hear(Vector3 position, float normalizedDistance)
        {
            if (!IsActivated || IsAwakening || !director.PlayerRecognitionEnabled || Victim != null || target != null || returningToPatrol || Time.time < nextNoiseAt) return;
            var stage = director.CurrentStageRules;
            if (!WithinPatrolLeash(position, stage)) return;
            if (!Room.RoamAcrossRooms && !Room.Contains(position)) return;
            // A sound is a remembered point, never a live player Transform.
            sensingPoint = scanLook = position; investigating = normalizedDistance <= stage.InvestigateFraction;
            noiseUntil = Time.time + Mathf.Max(.1f, investigating ? stage.InvestigateSeconds : stage.ListenSeconds);
            nextNoiseAt = Time.time + .8f; searching = false; scanStarted = Time.time;
            sensingLook = true; repathAt = 0;
        }
        internal void ForgetPlayers()
        {
            // Existing captures continue. Keep the actor's position and patrol waypoint.
            if (Victim != null) return;
            target = null;
            visible = sensingLook = investigating = searching = false;
            unseen = noiseUntil = searchUntil = prepareUntil = nextNoiseAt = repathAt = 0;
            brain?.End(); restartBrain = true;
            if (agent != null) { Stop(); ClearNavigation(); }
            EnterBehaviour(returningToPatrol ? PursuitBehaviour.Return : PursuitBehaviour.Patrol);
            SetMotion(IsActivated ? PursuitMotion.Awaken : PursuitMotion.Display);
        }
        public bool ActPrepare()
        {
            if (target == null || Time.time >= prepareUntil) return false;
            EnterBehaviour(PursuitBehaviour.Prepare); Stop(); Face(lastSeen); SetMotion(PursuitMotion.Awaken); return true;
        }
        public bool ActChase()
        {
            if (target == null) return false;
            EnterBehaviour(PursuitBehaviour.Chase); agent.speed = rule.ResolvedChaseSpeed;
            // 닿을 경로가 없거나 막힌 경로의 끝에 섰으면 제자리 달리기 대신 대상을 노려보며 대기하고 재계산을 기다린다.
            if (Navigate(lastSeen, true) && !AtPartialEnd && !(!visible && Reached(lastSeen))) SetMotion(PursuitMotion.Run);
            else { SetMotion(PursuitMotion.Awaken); Face(lastSeen); }
            if (director.RequiresOwnerContact(target.gameObject)) return true;
            if (visible && Vector3.Distance(transform.position, target.position) <= GameSettings.WaitingRoom.Pursuit.CaptureRadius
                && director.ClearLine(transform.position + Vector3.up, target.position + Vector3.up, target)) director.BeginCapture(this, target.gameObject);
            return true;
        }
        public bool ActInvestigate()
        {
            if (!investigating || Time.time >= noiseUntil) return false;
            EnterBehaviour(PursuitBehaviour.Investigate);
            agent.speed = rule.PatrolSpeed; SetMotion(PursuitMotion.Walk); Navigate(sensingPoint);
            if (Reached(sensingPoint) || Unreachable(sensingPoint)) { investigating = false; noiseUntil = Time.time + rule.ListenSeconds; scanStarted = Time.time; }
            return true;
        }
        public bool ActListen()
        {
            if (investigating || Time.time >= noiseUntil) return false;
            EnterBehaviour(PursuitBehaviour.Listen); Scan(); return true;
        }
        public bool ActSearch()
        {
            if (!searching) return false;
            if (Time.time >= searchUntil) { searching = sensingLook = false; return false; }
            EnterBehaviour(PursuitBehaviour.Search);
            if (scanStarted < 0 && !Reached(sensingPoint) && !Unreachable(sensingPoint)) {
                agent.speed = rule.PatrolSpeed; SetMotion(PursuitMotion.Walk); Navigate(sensingPoint);
            } else {
                if (scanStarted < 0) { scanStarted = Time.time; searchUntil = Time.time + rule.SearchSeconds; }
                Scan();
            }
            return true;
        }
        public bool ActPatrol()
        {
            if (returningToPatrol) {
                EnterBehaviour(PursuitBehaviour.Return); agent.speed = Mathf.Max(0, rule.PatrolSpeed);
                SetMotion(PursuitMotion.Walk); Navigate(patrolReturnPoint); return true;
            }
            EnterBehaviour(PursuitBehaviour.Patrol); sensingLook = false; agent.speed = Mathf.Max(0, rule.PatrolSpeed);
            var points = PatrolRoute;
            if (points == null || points.Length == 0) { Stop(); SetMotion(PursuitMotion.Awaken); return true; }
            // 모든 순회지가 막혀 있으면 한 바퀴 건너뛴 뒤 잠시 멈춰 대기하고 문 개폐 후 다시 찾는다.
            if (patrolSkips >= points.Length) {
                Stop(); SetMotion(PursuitMotion.Awaken);
                if (Time.time >= patrolRetryAt) { patrolSkips = 0; hasNavGoal = false; }
                return true;
            }
            waypoint %= points.Length;
            var point = points[waypoint];
            if (point == null) { SkipWaypoint(points.Length); return true; }
            // 순회지가 소품·벽 안이면 근방 NavMesh로, 막힌 곳이면 가장 가까운 도달 지점으로 조정해 이동한다.
            bool routed = Navigate(point.position);
            if (navDeferred) return true;
            if (!routed || (navGoal - point.position).sqrMagnitude > RouteAdjustRadius * RouteAdjustRadius) { SkipWaypoint(points.Length); return true; }
            SetMotion(PursuitMotion.Walk);
            if (Reached(point.position)) { waypoint = (waypoint + 1) % points.Length; patrolSkips = 0; }
            return true;
        }
        void EnterBehaviour(PursuitBehaviour next)
        {
            if (Behaviour == next) return;
            Behaviour = next; repathAt = 0;
            director.ActorStateChanged();
        }
        void SkipWaypoint(int count)
        {
            waypoint = (waypoint + 1) % count; patrolSkips++;
            hasNavGoal = false;
            if (patrolSkips >= count) patrolRetryAt = Time.time + PatrolRetrySeconds;
        }
        void Face(Vector3 p)
        {
            var direction = Vector3.ProjectOnPlane(p - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 200 * Time.deltaTime);
        }
        void Scan()
        {
            Stop(); SetMotion(PursuitMotion.Awaken);
            var direction = Vector3.ProjectOnPlane(sensingPoint - transform.position, Vector3.up).normalized;
            if (direction.sqrMagnitude < .001f) direction = initialRotation * Vector3.forward;
            scanLook = transform.position + Quaternion.AngleAxis(Mathf.Sin((Time.time - scanStarted) * 3) * 55, Vector3.up) * direction * 3;
            Face(scanLook);
        }
        void AlignCapture()
        {
            var pose = Victim.GetComponent<PursuitVictimPose>();
            if (pose == null || !pose.IsReady) return;
            var goal = pose.AttackerPosition;
            var rotation = Quaternion.LookRotation(pose.HeadDirection, Vector3.up);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(CaptureElapsed / Mathf.Max(.1f, GameSettings.WaitingRoom.Pursuit.KnockdownSeconds)));
            transform.SetPositionAndRotation(Vector3.Lerp(captureStart, goal, t), Quaternion.Slerp(captureRotation, rotation, t));
        }
    }
}
