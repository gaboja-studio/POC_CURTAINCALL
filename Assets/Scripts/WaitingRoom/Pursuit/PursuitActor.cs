using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    // 새 값은 끝에 붙인다(스냅샷이 숫자로 보내고 Clips 배열이 순서로 대응한다).
    public enum PursuitMotion : byte { Display, Twitch, Awaken, Walk, Run, Grab, Tear, Climb, Drop }

    [DefaultExecutionOrder(1100)]
    public sealed partial class PursuitActor : MonoBehaviour
    {
        public Animation Motion;
        public Transform Chest;
        public AnimationClip[] Clips;
        public PursuitRoom Room { get; private set; }
        public PursuitMotion State { get; private set; }
        public GameObject Victim { get; private set; }
        public float CaptureElapsed { get; private set; }
        public bool CaptureFinished { get; private set; }
        public PursuitPlacement Placement { get; private set; }
        public int ActivationStage => Placement != null ? Placement.ResolvedActivationStage : 1;
        public bool IsActivated => director != null && director.Elapsed >= 0
            && (director.CurrentStageIndex + 1 >= ActivationStage || Victim != null);
        float activatedAt = -1;
        bool IsAwakening => activatedAt < 0 || Time.time - activatedAt < GameSettings.WaitingRoom.Pursuit.AwakeningSeconds;
        public GameObject ChasedPlayer => remote ? remoteTarget : target != null ? target.gameObject : null;
        GameObject remoteTarget;
        NavMeshAgent agent;
        PursuitDirector director;
        Transform target;
        float unseen, repathAt;
        Vector3 lastSeen;
        int waypoint;
        Quaternion chestBase;
        bool chestAdjusted;
        Vector3 initialPosition;
        Quaternion initialRotation;
        bool remote, remoteLookValid;
        Vector3 remotePosition, remoteLook;
        Quaternion remoteRotation;
        readonly PursuitPoseBuffer poseBuffer = new();
        internal double ContactPoseTime { get; private set; } = -1;
        double contactPoseReceivedAt = double.NegativeInfinity;
        internal bool HasFreshContactPose => ContactPoseTime >= 0
            && Time.realtimeSinceStartupAsDouble - contactPoseReceivedAt <= PursuitContactRules.MaxPoseSilence;
        PursuitAppearance appearance;
        bool advancedMotion;
        public bool HasLook => target != null || Victim != null || remoteLookValid || sensingLook;
        public Vector3 LookPoint => Victim != null ? Victim.transform.position : remote ? remoteLook : target != null ? lastSeen : scanLook;
        public void Initialize(PursuitDirector owner, PursuitRoom room, PursuitPlacement placement = null)
        {
            director = owner; Room = room; Placement = placement;
            appearance = GetComponent<PursuitAppearance>();
            if (placement != null) placement.ApplyAppearance(appearance);
            initialPosition = transform.position; initialRotation = transform.rotation;
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = .28f; agent.height = 1.8f; agent.acceleration = 28; agent.angularSpeed = 300;
            agent.stoppingDistance = .25f;
            // 경로는 PursuitActor가 동기 계산으로 직접 갱신한다. 자동 재경로는 비동기 대기열에서 pathPending 정지를 만든다.
            agent.autoRepath = false;
            // 오르기 통로는 직접 움직인다(PursuitActor.Climb). 낮은 계단 통로도 같은 경로로 걸어서 지난다.
            agent.autoTraverseOffMeshLink = false;
            agent.enabled = false;
            if (Motion == null) Motion = GetComponentInChildren<Animation>();
            if (Motion != null && Clips != null)
                for (int i = 0; i < Clips.Length; i++) if (Clips[i] != null) Motion.AddClip(Clips[i], ((PursuitMotion)i).ToString());
            if (Motion != null && appearance != null) {
                if (appearance.AdvancedWalk != null) Motion.AddClip(appearance.AdvancedWalk, "AdvancedWalk");
                if (appearance.AdvancedRun != null) Motion.AddClip(appearance.AdvancedRun, "AdvancedRun");
            }
            climbIk = new PursuitClimbIk(transform, GetComponentInChildren<SkinnedMeshRenderer>());
            SetMotion(PursuitMotion.Display, true);
            InitializeBrain();
        }
        public void ResetActor()
        {
            if (agent != null) agent.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            remote = false; remoteLookValid = false; remoteTarget = null; waypoint = 0;
            poseBuffer.Clear();
            ContactPoseTime = -1; contactPoseReceivedAt = double.NegativeInfinity;
            appearance?.SetCaptureCount(0);
            Victim = null; target = null; CaptureElapsed = 0; CaptureFinished = false; unseen = 0;
            activatedAt = -1;
            ResetClimb();
            ResetBrain();
            SetMotion(PursuitMotion.Display, true);
        }
        public void SetMotion(PursuitMotion next, bool force = false)
        {
            bool advanced = IsActivated && director.CurrentStageIndex + 1 >= GameSettings.WaitingRoom.Pursuit.AdvancedAppearanceStage;
            if (State == next && !force && advancedMotion == advanced) return;
            advancedMotion = advanced;
            if (!IsTraversalMotion(next)) { climbIk?.Clear(); remoteTraversal = false; }
            State = next;
            if (!remote) director?.ActorStateChanged();
            string clip = advanced && (next == PursuitMotion.Walk || next == PursuitMotion.Run) ? "Advanced" + next : next.ToString();
            if (Motion != null) {
                if (Motion.GetClip(clip) == null) clip = next.ToString();
                if (Motion.GetClip(clip) != null) {
                    if (IsTraversalMotion(next)) {
                        var state = Motion[clip];
                        var rules = GameSettings.WaitingRoom.Pursuit;
                        // 높은 곳은 오르는 시간이 늘어난다(TraversalSeconds). 통과 중이 아니면 기본 시간.
                        float duration = climbing ? climbDuration : next == PursuitMotion.Climb ? rules.ClimbSeconds : rules.DropSeconds;
                        state.wrapMode = WrapMode.ClampForever;
                        state.speed = state.length / Mathf.Max(.1f, duration);
                        state.time = 0;
                    }
                    Motion.CrossFade(clip, IsTraversalMotion(next) ? .06f : .12f);
                }
            }
        }
        public void ServerTick(float elapsed, bool warning)
        {
            if (elapsed < 0 || !IsActivated) {
                if (activatedAt >= 0) {
                    ForgetPlayers();
                    if (agent != null) agent.enabled = false;
                    activatedAt = -1;
                }
                SetMotion(elapsed < 0 && warning && ActivationStage == 1 ? PursuitMotion.Twitch : PursuitMotion.Display);
                return;
            }
            var rules = GameSettings.WaitingRoom.Pursuit;
            if (Victim != null) {
                CaptureElapsed += Time.deltaTime;
                SetMotion(CaptureElapsed < rules.KnockdownSeconds ? PursuitMotion.Grab : PursuitMotion.Tear);
                director.AdvanceCapture(this);
                return;
            }
            if (activatedAt < 0) activatedAt = Time.time;
            if (IsAwakening) { SetMotion(PursuitMotion.Awaken); return; }
            if (agent == null) return;
            // NavMesh 밖 소품 위로 직접 오르내리는 중이거나 그 위에 서 있으면 길찾기 대신 직접 처리한다.
            if (TickDirect()) return;
            if (!EnsureOnNavMesh()) { SetMotion(PursuitMotion.Awaken); return; }
            ApplyClimbArea();
            if (TickClimb()) return;
            TickBrain(director.CurrentStageRules);
            TickStuck();
        }
        public void Capture(GameObject victim, float elapsed)
        {
            if (climbing) FinishClimb();
            Victim = victim; CaptureElapsed = elapsed;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            if (agent != null) { agent.enabled = false; ClearNavigation(); }
            captureStart = transform.position;
            captureRotation = transform.rotation;
            Behaviour = PursuitBehaviour.Capture;
            target = victim != null ? victim.transform : null;
            CaptureFinished = false;
            SetMotion(PursuitMotion.Grab);
        }
        public void FinishCapture()
        {
            if (Victim != null && appearance != null) appearance.SetCaptureCount(appearance.CaptureCount + 1);
            Victim = null; target = null; CaptureFinished = true; CaptureElapsed = 0;
        }
        public void Receive(Vector3 p, Quaternion r, PursuitMotion motion, GameObject victim, float capture, Vector3 look, bool hasLook, GameObject chasedPlayer = null)
        {
            if (agent != null) agent.enabled = false;
            if (!remote) transform.SetPositionAndRotation(p, r);
            remote = true; remotePosition = p; remoteRotation = r; remoteLook = look; remoteLookValid = hasLook;
            SetMotion(motion);
            if (victim != null && Victim != victim) { captureStart = transform.position; captureRotation = transform.rotation; }
            Victim = victim; CaptureElapsed = capture; remoteTarget = chasedPlayer;
        }
        public void ReceivePose(double timestamp, Vector3 position, Quaternion rotation)
        {
            if (!poseBuffer.Push(timestamp, position, rotation)) return;
            ContactPoseTime = timestamp; contactPoseReceivedAt = Time.realtimeSinceStartupAsDouble;
        }
        void Update()
        {
            // 다음 Legacy Animation 평가에 전 프레임 IK가 누적되지 않도록 되돌린다.
            climbIk?.Restore();
            if (remote) {
                if (poseBuffer.Sample(Time.unscaledDeltaTime, out var p, out var q)) { remotePosition = p; remoteRotation = q; }
                // A delayed burst can never trigger the old 3m teleport. ResetActor is the explicit warp path.
                transform.position = Vector3.MoveTowards(transform.position, Vector3.Lerp(transform.position, remotePosition, 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime)), 18 * Time.unscaledDeltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, remoteRotation, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
                if (Victim != null) CaptureElapsed += Time.deltaTime;
            }
            // Undo only this script's look offset before the next animation evaluation.
            if (chestAdjusted && Chest != null) Chest.localRotation = chestBase;
            chestAdjusted = false;
        }
        void LateUpdate()
        {
            if (remote && Victim == null) director?.ReportLocalContact(this);
            if (Victim != null) { AlignCapture(); return; }
            if (IsTraversalMotion(State)) {
                if (climbing || remoteTraversal) climbIk?.Apply(TraversalProgress, GameSettings.WaitingRoom.Pursuit);
                return;
            }
            if (Chest == null || !HasLook) return;
            var p = LookPoint;
            var d = transform.InverseTransformDirection(p - transform.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -90, 90);
            chestBase = Chest.localRotation;
            // Imported chest Y is its bone axis; rotate around character up in world space.
            Chest.rotation = Quaternion.AngleAxis(yaw, transform.up) * Chest.rotation;
            chestAdjusted = true;
        }
    }
}
