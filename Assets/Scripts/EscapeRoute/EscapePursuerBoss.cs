using System;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>The same pursuer starts the final fire and contests the existing cable-cutting exit.</summary>
    [DefaultExecutionOrder(900)]
    public sealed partial class EscapePursuerBoss : MonoBehaviour
    {
        public enum Phase : byte { Waiting, Ignition, Chasing, LeapWarning, Leaping, Landing, Battle, DashWarning, Dashing, Hit, Dying, Dead }
        [SerializeField] EscapeUnderstageChase chase;
        [SerializeField] EscapePursuerRoute route;
        [SerializeField] EscapePursuitFire fire;
        [SerializeField] HumanCannonPole[] finalePoles = Array.Empty<HumanCannonPole>();
        [SerializeField] Transform ignitionLever, landing, rig;
        [SerializeField] BoxCollider arena;
        [SerializeField] int ignitionRoutePoint = 62;
        [SerializeField] int leapRoutePoint = 71;
        [SerializeField] LineRenderer landingRing;
        [SerializeField] Font labelFont;
        static CurtainCall.Settings.EscapeRouteSettings.PursuerTuning Tuning => CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer;
        public static EscapePursuerBoss Current { get; private set; }
        public Phase State => state.Phase;
        public int Cuts => CutCount(state.Mask);
        public bool Defeated => state.Phase == Phase.Dying || state.Phase == Phase.Dead;
        public bool InBattle => state.Phase >= Phase.LeapWarning && !Defeated;
        public bool CombatStarted => state.Phase >= Phase.LeapWarning;
        public bool LimitsFinalFire => arena != null;
        public Transform ArenaFrame => arena ? arena.transform : transform;
        public float SpeedScale => MovementScale(state.Mask, Tuning.bossSlowPerCut);
        bool fireRequested;
        double nextDash;
        uint observedShots;
        Vector3 rigRest;
        Quaternion rigRotation;

        void Awake() { if (rig) { rigRest = rig.localPosition; rigRotation = rig.localRotation; } }
        void OnEnable()
        {
            Current = this;
            NetworkPlayer.Restarted += Restart;
            EscapeExitSaw.ShotObserved += ObserveShot;
        }
        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restart;
            EscapeExitSaw.ShotObserved -= ObserveShot;
            Detach();
            if (Current == this) Current = null;
        }
        void Restart(NetworkPlayer _) { state = default; state.Round = -1; fireRequested = false; nextDash = 0; observedShots = 0; }
        public static int CutCount(byte mask) { int count = 0; for (; mask != 0; mask &= (byte)(mask - 1)) count++; return count; }
        public static float MovementScale(byte mask, float loss) => Mathf.Max(.4f, 1f - CutCount(mask) * Mathf.Clamp(loss, 0, .12f));
        public bool InsideArena(Vector3 feet) => arena && arena.bounds.Contains(feet + Vector3.up);
        public bool BeyondFireBoundary(Vector3 feet) => arena && arena.transform.InverseTransformPoint(feet).x >= -arena.size.x * .5f;
        Vector3 ClampArena(Vector3 position)
        {
            var p = arena.transform.InverseTransformPoint(position);
            p.x = Mathf.Clamp(p.x, -arena.size.x * .5f + 1.6f, arena.size.x * .5f - 1.6f);
            p.z = Mathf.Clamp(p.z, -arena.size.z * .5f + 1.6f, arena.size.z * .5f - 1.6f);
            p.y = 0;
            return arena.transform.TransformPoint(p);
        }
        NetworkPlayer Nearest()
        {
            NetworkPlayer best = null; float distance = float.PositiveInfinity;
            foreach (var p in NetworkPlayer.All)
                if (p && p.IsSpawned && p.State == PlayerState.Normal && InsideArena(p.transform.position))
                {
                    float d = (p.transform.position - transform.position).sqrMagnitude;
                    if (d < distance) { best = p; distance = d; }
                }
            return best;
        }
        bool FinaleReached()
        {
            var poles = HumanCannonPoleSystem.Current;
            foreach (var p in NetworkPlayer.All)
            {
                if (!p || !p.IsSpawned || p.State != PlayerState.Normal) continue;
                int held = poles ? poles.HeldBy(p.OwnerClientId) : -1;
                if (held >= 0 && Array.IndexOf(finalePoles, poles.Pole(held)) >= 0) return true;
                // Dev entries and a teammate already across the last bridge must not skip the encounter.
                if (route.Project(route.PlayerFeet(p), out _) >= ignitionRoutePoint + 2) return true;
            }
            return false;
        }
        bool LauncherTaken()
        {
            foreach (var saw in EscapeExitSaw.Devices)
                if (saw && saw.FinaleBoss == this && saw.Carrier != CannonSeatState.Nobody) return true;
            return false;
        }
        void Enter(Phase phase, double now, Vector3 goal = default)
        {
            state.Phase = phase; state.Started = now; state.From = transform.position; state.Goal = goal; state.Revision++;
        }
        internal bool AdvanceHost()
        {
            var timer = EscapeRouteTimer.Current;
            if (!timer || !timer.HasStarted || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return false;
            if (state.Round != timer.StartedAt) { Restart(null); state.Round = timer.StartedAt; }
            double now = timer.Clock;
            float age = Mathf.Max(0, (float)(now - state.Started));
            if (state.Phase == Phase.Waiting)
            {
                fireRequested |= FinaleReached();
                if (!fireRequested || !route.Following || route.Progress < ignitionRoutePoint - .1f) return false;
                chase.CancelBossAttack(); Enter(Phase.Ignition, now); age = 0;
            }
            if (state.Phase == Phase.Chasing)
            {
                if (!LauncherTaken() || route.Progress < leapRoutePoint
                    || Vector3.Distance(transform.position, landing.position) > 65) return false;
                chase.CancelBossAttack(); Enter(Phase.LeapWarning, now, landing.position); age = 0;
            }
            chase.BossBody(!Defeated);
            var before = transform.position;
            switch (state.Phase)
            {
                case Phase.Ignition:
                    if (age >= .65f && fire && !fire.IsArmed) fire.ServerIgniteFromPursuer(this);
                    if (age >= 1.4f) Enter(Phase.Chasing, now);
                    break;
                case Phase.LeapWarning:
                    Face(state.Goal - transform.position);
                    if (age >= Tuning.bossLeapWarning) Enter(Phase.Leaping, now, state.Goal);
                    break;
                case Phase.Leaping:
                    float t = Mathf.Clamp01(age / Tuning.bossLeapSeconds);
                    transform.position = Vector3.Lerp(state.From, state.Goal, t) + Vector3.up * (4 * Tuning.bossLeapHeight * t * (1-t));
                    if (t >= 1) { Enter(Phase.Landing, now); chase.PlayStep(.6f); }
                    break;
                case Phase.Landing:
                    if (age >= Tuning.bossLandingRecovery) { Enter(Phase.Battle, now); nextDash = now + 2; }
                    break;
                case Phase.DashWarning:
                    if (age >= Tuning.bossDashWarning) Enter(Phase.Dashing, now, state.Goal);
                    break;
                case Phase.Dashing:
                    transform.position = Vector3.Lerp(state.From, state.Goal, Mathf.SmoothStep(0, 1, age / Tuning.bossDashSeconds));
                    if (age >= Tuning.bossDashSeconds) Enter(Phase.Battle, now);
                    break;
                case Phase.Hit:
                    if (age >= Tuning.bossHitRecovery) Enter(Phase.Battle, now);
                    break;
                case Phase.Dying:
                    if (age >= 2) Enter(Phase.Dead, now);
                    break;
                case Phase.Battle:
                    var target = Nearest();
                    bool attacking = chase.BossAttack(target);
                    if (target)
                    {
                        if (!attacking)
                        {
                            Vector3 toward = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
                            Face(toward);
                            if (toward.magnitude > 2.8f)
                            {
                                Vector3 to = ClampArena(transform.position + toward.normalized * (Tuning.bossMoveSpeed * SpeedScale * Time.deltaTime));
                                if (ClearMovement(transform.position, to)) transform.position = to;
                            }
                        }
                    }
                    break;
            }
            float moved = Vector3.Distance(before, transform.position);
            chase.AdvanceGait(moved, 0, state.Phase == Phase.Battle || state.Phase == Phase.Dashing ? 1 : 0, moved < .001f, Time.deltaTime);
            return true;
        }
        void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 300 * Time.deltaTime);
        }
        void ObserveShot(EscapeExitSaw saw, ulong actor, string key, string kind, Vector3 origin, Vector3 direction, string result)
        {
            if (kind != "FIRED" || saw.FinaleBoss != this || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            observedShots++;
            double now = EscapeRouteTimer.Current.Clock;
            if (state.Phase != Phase.Battle || chase.Capturing || now < nextDash || observedShots % 3 != 0) return;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            if ((observedShots / 3) % 2 == 0) side = -side;
            Vector3 end = ClampArena(transform.position + side * Tuning.bossDashDistance * SpeedScale);
            if ((end - transform.position).sqrMagnitude < 1 || !ClearMovement(transform.position, end)) return;
            chase.CancelBossAttack(); nextDash = now + Tuning.bossDashCooldown;
            Enter(Phase.DashWarning, now, end);
        }
        bool ClearMovement(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            foreach (var hit in Physics.CapsuleCastAll(from + Vector3.up * 1.6f, from + Vector3.up * 3.6f, 1.4f,
                delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.GetComponentInParent<NetworkPlayer>()) return false;
            return true;
        }
        void LateUpdate()
        {
            Attach();
            if (chase.InShowcase) return;
            var timer = EscapeRouteTimer.Current;
            if (!timer || !timer.HasStarted || state.Round != timer.StartedAt) return;
            if (NetworkManager.Singleton && NetworkManager.Singleton.IsServer) Publish(timer.Clock);
            Present(timer.Clock);
        }
    }
}
