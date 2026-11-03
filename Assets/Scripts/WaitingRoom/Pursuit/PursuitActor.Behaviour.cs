using CurtainCall.Settings;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

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
            if (target != null && (!director.IsAvailable(target.gameObject) || (!rule.AlwaysNearest &&
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
            if (Victim != null || target != null || returningToPatrol || director.Elapsed < GameSettings.WaitingRoom.Pursuit.AwakeningSeconds || Time.time < nextNoiseAt) return;
            var stage = director.CurrentStageRules;
            if (!WithinPatrolLeash(position, stage)) return;
            if (!Room.RoamAcrossRooms && !Room.Contains(position)) return;
            // A sound is a remembered point, never a live player Transform.
            sensingPoint = scanLook = position; investigating = normalizedDistance <= stage.InvestigateFraction;
            noiseUntil = Time.time + Mathf.Max(.1f, investigating ? stage.InvestigateSeconds : stage.ListenSeconds);
            nextNoiseAt = Time.time + .8f; searching = false; scanStarted = Time.time;
            sensingLook = true; repathAt = 0;
        }
        public bool ActPrepare()
        {
            if (target == null || Time.time >= prepareUntil) return false;
            EnterBehaviour(PursuitBehaviour.Prepare); Stop(); Face(lastSeen); SetMotion(PursuitMotion.Awaken); return true;
        }
        public bool ActChase()
        {
            if (target == null) return false;
            EnterBehaviour(PursuitBehaviour.Chase); agent.speed = rule.ResolvedChaseSpeed; SetMotion(PursuitMotion.Run); Navigate(lastSeen);
            if (visible && Vector3.Distance(transform.position, target.position) <= GameSettings.WaitingRoom.Pursuit.CaptureRadius
                && director.ClearLine(transform.position + Vector3.up, target.position + Vector3.up, target)) director.BeginCapture(this, target.gameObject);
            return true;
        }
        public bool ActInvestigate()
        {
            if (!investigating || Time.time >= noiseUntil) return false;
            EnterBehaviour(PursuitBehaviour.Investigate);
            agent.speed = rule.PatrolSpeed; SetMotion(PursuitMotion.Walk); Navigate(sensingPoint);
            if (Reached(sensingPoint) || Unreachable()) { investigating = false; noiseUntil = Time.time + rule.ListenSeconds; scanStarted = Time.time; }
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
            if (scanStarted < 0 && !Reached(sensingPoint) && !Unreachable()) {
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
            var points = Placement != null ? Placement.Route(Room) : Room.PatrolPoints;
            if (points == null || points.Length == 0) { Stop(); SetMotion(PursuitMotion.Awaken); return true; }
            var point = points[waypoint % points.Length];
            if (point == null) { waypoint = (waypoint + 1) % points.Length; return true; }
            SetMotion(PursuitMotion.Walk); Navigate(point.position);
            if (Reached(point.position) || Unreachable()) waypoint = (waypoint + 1) % points.Length;
            return true;
        }
        void EnterBehaviour(PursuitBehaviour next)
        {
            if (Behaviour == next) return;
            Behaviour = next; repathAt = 0;
        }
        bool Reached(Vector3 p) => !agent.pathPending && Vector3.Distance(transform.position, p) < .7f;
        bool Unreachable() => !agent.pathPending && agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete && agent.remainingDistance < .7f;
        void Stop() { if (agent.enabled && agent.isOnNavMesh) agent.ResetPath(); }
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
            transform.SetPositionAndRotation(remote ? goal : Vector3.Lerp(captureStart, goal, t), remote ? rotation : Quaternion.Slerp(captureRotation, rotation, t));
        }
    }
}
