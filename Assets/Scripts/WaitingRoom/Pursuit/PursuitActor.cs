using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public enum PursuitMotion : byte { Display, Twitch, Awaken, Walk, Run, Grab, Tear }

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
        public bool HasLook => target != null || Victim != null || remoteLookValid || sensingLook;
        public Vector3 LookPoint => Victim != null ? Victim.transform.position : remote ? remoteLook : target != null ? lastSeen : scanLook;
        public void Initialize(PursuitDirector owner, PursuitRoom room, PursuitPlacement placement = null)
        {
            director = owner; Room = room; Placement = placement;
            initialPosition = transform.position; initialRotation = transform.rotation;
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = .28f; agent.height = 1.8f; agent.acceleration = 28; agent.angularSpeed = 300;
            agent.stoppingDistance = .25f;
            agent.enabled = false;
            if (Motion == null) Motion = GetComponentInChildren<Animation>();
            if (Motion != null && Clips != null)
                for (int i = 0; i < Clips.Length; i++) if (Clips[i] != null) Motion.AddClip(Clips[i], ((PursuitMotion)i).ToString());
            SetMotion(PursuitMotion.Display, true);
            InitializeBrain();
        }
        public void ResetActor()
        {
            if (agent != null) agent.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            remote = false; remoteLookValid = false; remoteTarget = null; waypoint = 0;
            Victim = null; target = null; CaptureElapsed = 0; CaptureFinished = false; unseen = 0;
            ResetBrain();
            SetMotion(PursuitMotion.Display, true);
        }
        public void SetMotion(PursuitMotion next, bool force = false)
        {
            if (State == next && !force) return;
            State = next;
            if (Motion != null && Motion.GetClip(next.ToString()) != null) Motion.CrossFade(next.ToString(), .12f);
        }
        public void ServerTick(float elapsed, bool warning)
        {
            if (elapsed < 0) { SetMotion(warning ? PursuitMotion.Twitch : PursuitMotion.Display); return; }
            var rules = GameSettings.WaitingRoom.Pursuit;
            if (Victim != null) {
                CaptureElapsed += Time.deltaTime;
                SetMotion(CaptureElapsed < rules.KnockdownSeconds ? PursuitMotion.Grab : PursuitMotion.Tear);
                director.AdvanceCapture(this);
                return;
            }
            if (elapsed < rules.AwakeningSeconds) { SetMotion(PursuitMotion.Awaken); return; }
            if (agent == null) return;
            if (!agent.enabled) {
                if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) return;
                transform.position = hit.position; agent.enabled = true;
            }
            if (!agent.isOnNavMesh) return;
            TickBrain(director.CurrentStageRules);
        }
        void Navigate(Vector3 p)
        {
            if (Time.time < repathAt) return;
            repathAt = Time.time + .2f;
            if (NavMesh.SamplePosition(p, out var hit, 2f, agent.areaMask)) agent.SetDestination(hit.position);
        }
        public void Capture(GameObject victim, float elapsed)
        {
            Victim = victim; CaptureElapsed = elapsed;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            if (agent != null) agent.enabled = false;
            captureStart = transform.position;
            captureRotation = transform.rotation;
            Behaviour = PursuitBehaviour.Capture;
            target = victim != null ? victim.transform : null;
            CaptureFinished = false;
        }
        public void FinishCapture()
        {
            Victim = null; target = null; CaptureFinished = true; CaptureElapsed = 0;
        }
        public void Receive(Vector3 p, Quaternion r, PursuitMotion motion, GameObject victim, float capture, Vector3 look, bool hasLook, GameObject chasedPlayer = null)
        {
            if (agent != null) agent.enabled = false;
            if (!remote || Vector3.Distance(transform.position, p) > 3) transform.SetPositionAndRotation(p, r);
            remote = true; remotePosition = p; remoteRotation = r; remoteLook = look; remoteLookValid = hasLook;
            SetMotion(motion);
            Victim = victim; CaptureElapsed = capture; remoteTarget = chasedPlayer;
        }
        void Update()
        {
            if (remote) {
                transform.position = Vector3.Lerp(transform.position, remotePosition, 1 - Mathf.Exp(-18 * Time.deltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation, remoteRotation, 1 - Mathf.Exp(-18 * Time.deltaTime));
            }
            // Undo only this script's look offset before the next animation evaluation.
            if (chestAdjusted && Chest != null) Chest.localRotation = chestBase;
            chestAdjusted = false;
        }
        void LateUpdate()
        {
            if (Victim != null) { AlignCapture(); return; }
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
