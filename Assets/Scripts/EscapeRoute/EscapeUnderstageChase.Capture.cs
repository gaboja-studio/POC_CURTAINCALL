using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        [SerializeField] Transform grabArm, grabTip;
        [SerializeField] LineRenderer captureCable;
        [SerializeField] Transform extendedForearm, graspingHand;
        [SerializeField] LineRenderer aimRing;
        float SeverTime => Mathf.Max(.05f, Tuning.severSeconds);
        float HoldTime => Mathf.Max(SeverTime, Tuning.holdSeconds);
        float SwingTime => Mathf.Max(.05f, Tuning.throwSwingSeconds) + Tuning.torsoThrowWarning;
        float FlightTime => Mathf.Max(.05f, Tuning.throwFlightSeconds);
        float StrikeTime => Mathf.Max(.05f, Tuning.armStrikeSeconds);
        float RetractTime => Mathf.Max(.05f, Tuning.armRetractSeconds);
        float MissRecovery => Mathf.Max(.05f, Tuning.missRecoverySeconds);
        static float ArmRadius => Mathf.Max(.05f, Tuning.armRadius);

        enum AttackPhase : byte { None, Warning, Strike, Retract, Grab, Throw }
        enum Reach : byte { Hall, Boarding, Wire }

        struct AttackCue : INetworkSerializeByMemcpy
        {
            public AttackPhase Phase;
            public Reach Reach;
            public ulong Actor;
            public double Started;
            public Vector3 Origin, Aim, MarkedFeet, Release, Drop, StopPosition;
            public bool Stopped;
            public ulong ThrowTarget;
        }

        AttackCue attack;
        NetworkPlayer victim;
        internal bool HoldingRunner => attack.Phase == AttackPhase.Grab || attack.Phase == AttackPhase.Throw;
        public bool HasReleasedCaptured(NetworkPlayer player)
        {
            if (!player || player.State != PlayerState.EscapeCaptured) return false;
            var net = NetworkManager.Singleton;
            var cue = net && !net.IsServer && hasSnapshot ? latest.Attack : attack;
            var timer = EscapeRouteTimer.Current;
            return timer && cue.Actor == player.OwnerClientId && cue.Phase == AttackPhase.Throw
                && timer.Clock - cue.Started >= SwingTime;
        }
        public ulong CapturedThrowTarget
        {
            get
            {
                var net = NetworkManager.Singleton;
                var cue = net && !net.IsServer && hasSnapshot ? latest.Attack : attack;
                return cue.Phase == AttackPhase.Throw ? cue.ThrowTarget : ulong.MaxValue;
            }
        }
        bool usingBypass;
        double soundedAt = -1;
        float previousExtension, previousTorsoFlight;
        double nextAim;
        double attackRound = -1;
        Vector3 Hand => grabTip.position - grabArm.up * .45f;
        float Warning(Reach reach) => Mathf.Max(.2f, boss && boss.InBattle ? Tuning.bossAttackWarning
            : reach == Reach.Hall ? Tuning.hallWarning : reach == Reach.Boarding ? Tuning.stationWarning : Tuning.wireWarning);
        bool InTransit(NetworkPlayer player) => fullRoute ? fullRoute.InTransit(player) : bypass && bypass.InTransit(player);
        Vector3 PlayerFeet(NetworkPlayer player) => fullRoute ? fullRoute.PlayerFeet(player) : bypass ? bypass.GunPosition(player) : player.transform.position;

        // Complete a committed strike, then reconsider the team's movement.
        bool UpdateAttack(bool divert, bool boarding, ref Vector3 goal, ref Vector3 watch, NetworkPlayer forcedRunner = null)
        {
            double now = EscapeRouteTimer.Current.Clock;
            if (attackRound != EscapeRouteTimer.Current.StartedAt)
            {
                ClearAttack(); nextAim = 0; soundedAt = -1;
                attackRound = EscapeRouteTimer.Current.StartedAt;
            }
            if (attack.Phase == AttackPhase.Grab || attack.Phase == AttackPhase.Throw)
            {
                if (!victim || !victim.IsSpawned || victim.State != PlayerState.EscapeCaptured) { ClearAttack(); return true; }
                float elapsed = (float)(now - attack.Started);
                if (attack.Phase == AttackPhase.Throw)
                {
                    watch = Vector3.ProjectOnPlane(attack.Drop - transform.position, Vector3.up);
                    if (elapsed >= SwingTime && !attack.Stopped)
                    {
                        if (previousTorsoFlight < 0)
                        {
                            SampleGait(0, 0);
                            attack.Release = ThrowHandPosition(1f) - Vector3.up * .65f;
                            previousTorsoFlight = 0;
                            nextSend = 0;
                        }
                        float t = Mathf.Clamp01((elapsed - SwingTime) / FlightTime);
                        var from = ThrowPosition(attack.Release, attack.Drop, previousTorsoFlight) + Vector3.up * .8f;
                        var to = ThrowPosition(attack.Release, attack.Drop, t) + Vector3.up * .8f;
                        if (SweepProjectile(from, to, .55f, attack.Actor, out var impact))
                        { attack.Stopped = true; attack.StopPosition = impact - Vector3.up * .8f; nextSend = 0; }
                        previousTorsoFlight = t;
                    }
                    if (elapsed >= SwingTime + FlightTime) { victim.ServerFinishEscapeThrow(); ClearAttack(); }
                    return true;
                }
                if (elapsed < HoldTime)
                {
                    victim.ServerSeverCapturedLimbs(Mathf.Clamp(Mathf.FloorToInt(elapsed / SeverTime * 4f), 0, 4));
                    goal = refugeApproach.position;
                    // Tear first, then keep the torso visibly in hand while advancing.
                    return elapsed < SeverTime;
                }
                victim.ServerSeverCapturedLimbs();
                if (!divert && Vector3.Distance(transform.position, refugeApproach.position) > .65f
                    && !FindTorsoTarget(Hand, out _, out _))
                {
                    goal = refugeApproach.position;
                    return false;
                }
                // A normal overhand throw: release from the real wrist, never a distant
                // route marker reached by stretching the arm.
                SampleGait(0, 0);
                Vector3 release = ThrowHandPosition(1f) - Vector3.up * .65f;
                Vector3 preferred = divert && fullRoute ? fullRoute.ThrowEnd : bypass.CannonDrop;
                bool targeted = FindTorsoTarget(release, out var drop, out ulong throwTarget);
                if (!targeted && !TryFindThrow(release, preferred, out drop))
                    drop = preferred; // The swept projectile stops on scenery if this discard hits a wall.
                attack.Phase = AttackPhase.Throw;
                attack.Started = now;
                attack.Aim = Hand;
                attack.Release = release;
                attack.Drop = drop;
                attack.ThrowTarget = throwTarget;
                attack.Stopped = false;
                previousTorsoFlight = -1;
                return true;
            }

            if (attack.Phase != AttackPhase.None) return AdvanceArm(now, ref watch);
            if (now < nextAim) return false;
            if (!CanAttackFromRoute && !(boss && boss.InBattle)) return false;
            var runner = forcedRunner ? forcedRunner : divert ? fullRoute ? fullRoute.SelectTarget() : bypass.RearmostRunner() : target;
            var reach = fullRoute && fullRoute.Following ? Reach.Wire : forcedRunner ? Reach.Hall
                : boarding ? Reach.Boarding : divert ? Reach.Wire : Reach.Hall;
            // The nearest visible survivor remains attackable on poles, seats, piggyback and
            // flight, even if a different player currently owns the route's progress target.
            if (!AttackCandidate(runner, reach))
            {
                runner = null;
                float best = float.PositiveInfinity;
                foreach (var candidate in NetworkPlayer.All)
                    if (AttackCandidate(candidate, reach))
                    {
                        float d = (PlayerFeet(candidate) - transform.position).sqrMagnitude;
                        if (d < best) { best = d; runner = candidate; }
                    }
            }
            if (!runner) return false;

            Vector3 aim = PlayerFeet(runner);
            var delta = aim - transform.position;
            watch = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (Vector3.Angle(transform.forward, watch) > 25f) return true;
            var mover = runner.GetComponent<PlayerMover>();
            if (reach == Reach.Hall)
                aim += Vector3.ClampMagnitude(mover.GroundVelocity * Tuning.aimLeadSeconds, 1.2f);
            Vector3 markedFeet = aim;
            aim += Vector3.up * (mover.BodyHeight * .68f);
            var raised = grabArm.rotation * Quaternion.Euler(-60f, 0f, 0f) * Quaternion.Inverse(grabArm.rotation);
            var origin = grabArm.position + raised * (Hand - grabArm.position);
            if (!ClearReach(origin, aim, runner)) return false;
            victim = runner;
            if (fullRoute) fullRoute.MarkPressured(runner);
            previousExtension = 0f;
            attack = new AttackCue { Phase = AttackPhase.Warning, Reach = reach, Actor = runner.OwnerClientId, Started = now,
                Origin = origin, Aim = aim, MarkedFeet = markedFeet };
            return true;
        }

        bool AttackCandidate(NetworkPlayer player, Reach reach)
        {
            if (!player || !player.IsSpawned || player.State != PlayerState.Normal || !player.TryGetComponent(out PlayerMover mover)) return false;
            Vector3 feet = PlayerFeet(player), delta = feet - transform.position;
            float range = fullRoute && fullRoute.Following ? Tuning.exposedGrabRange : Tuning.armRange;
            bool close = reach == Reach.Hall ? Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude < Tuning.contactRange * Tuning.contactRange
                && Mathf.Abs(delta.y) < range : delta.sqrMagnitude < range * range;
            return close && ClearReach(transform.position + Vector3.up * 3f, feet + Vector3.up * (mover.BodyHeight * .68f), player);
        }

        bool AdvanceArm(double now, ref Vector3 watch)
        {
            watch = Vector3.ProjectOnPlane(attack.Aim - transform.position, Vector3.up);
            float elapsed = (float)(now - attack.Started);
            if (attack.Phase == AttackPhase.Warning)
            {
                if (elapsed < Warning(attack.Reach)) return true;
                attack.Phase = AttackPhase.Strike;
                attack.Started = now;
                elapsed = 0f;
            }
            if (attack.Phase == AttackPhase.Retract)
            {
                if (elapsed >= RetractTime) { ClearAttack(); nextAim = now + MissRecovery; }
                return true;
            }
            float extension = Mathf.Clamp01(elapsed / StrikeTime);
            Vector3 from = Vector3.Lerp(attack.Origin, attack.Aim, previousExtension);
            Vector3 tip = Vector3.Lerp(attack.Origin, attack.Aim, extension);
            previousExtension = extension;
            bool clear = ClearReach(from, tip, victim);
            if (clear)
            {
                NetworkPlayer touched = null;
                float nearest = float.PositiveInfinity;
                foreach (var player in NetworkPlayer.All)
                {
                    if (!player || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                    var mover = player.GetComponent<PlayerMover>();
                    Vector3 feet = PlayerFeet(player);
                    float distance = (feet - from).sqrMagnitude;
                    if (distance < nearest && SweptHandTouches(from, tip, feet, mover.BodyHeight, mover.BodyRadius))
                    { touched = player; nearest = distance; }
                }
                if (touched && touched.ServerBeginEscapeCapture())
                {
                    victim = touched;
                    attack.Actor = touched.OwnerClientId;
                    Vector3 capturedFeet = PlayerFeet(touched);
                    ReleaseDevices(attack.Actor);
                    attack.Phase = AttackPhase.Grab;
                    attack.Started = now;
                    attack.Aim = capturedFeet;
                    attack.Release = tip;
                    return true;
                }
            }
            if (!clear || extension >= 1f)
            {
                attack.Phase = AttackPhase.Retract;
                attack.Started = now;
                attack.Release = clear ? tip : from;
            }
            return true;
        }

        // Closest distance between the swept hand and an upright capsule's axis. If the closest
        // horizontal point lies above or below that axis, one of the two end spheres is closest.
        internal static bool SweptHandTouches(Vector3 a, Vector3 b, Vector3 feet, float height, float radius)
            => SweptSphereTouches(a, b, feet, height, radius, ArmRadius);

        internal static bool SweptSphereTouches(Vector3 a, Vector3 b, Vector3 feet, float height, float radius, float projectileRadius)
        {
            float cap = Mathf.Min(radius, height * .5f);
            Vector3 low = feet + Vector3.up * cap, high = feet + Vector3.up * (height - cap);
            Vector3 ab = b - a;
            var flat = Vector3.ProjectOnPlane(ab, Vector3.up);
            float t = Mathf.Clamp01(Vector3.Dot(low - a, flat) / Mathf.Max(.000001f, flat.sqrMagnitude));
            var closest = a + ab * t;
            var center = new Vector3(feet.x, Mathf.Clamp(closest.y, low.y, high.y), feet.z);
            float reach = (cap + projectileRadius) * (cap + projectileRadius);
            return (closest - center).sqrMagnitude <= reach || EndDistance(low) <= reach || EndDistance(high) <= reach;

            float EndDistance(Vector3 point)
            {
                float along = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(.000001f, ab.sqrMagnitude));
                return (a + ab * along - point).sqrMagnitude;
            }
        }

        readonly RaycastHit[] reachHits = new RaycastHit[32];
        bool ClearReach(Vector3 start, Vector3 end, NetworkPlayer runner)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics.SphereCastNonAlloc(start, ArmRadius, delta.normalized, reachHits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == reachHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = reachHits[i].transform;
                if (hit.IsChildOf(transform) || hit.GetComponentInParent<NetworkPlayer>()) continue;
                if (runner)
                {
                    var poles = HumanCannonPoleSystem.Current;
                    int heldPole = poles ? poles.HeldBy(runner.OwnerClientId) : -1;
                    if (heldPole >= 0 && hit.IsChildOf(poles.Pole(heldPole).transform)) continue;
                    var cannon = HumanCannonSystem.Current;
                    var seat = cannon ? cannon.SeatedStation(runner.OwnerClientId) : null;
                    if (seat && hit.IsChildOf(seat.transform)) continue;
                }
                // A held launcher's coarse chassis collider must not turn its carrier into a safe spot.
                // The finale grab reaches over the carried machine; fixed scenery still blocks it.
                bool heldCutter = false;
                if (boss && boss.InBattle && runner)
                    foreach (var saw in EscapeExitSaw.Devices)
                        if (saw && saw.FinaleBoss == boss && saw.Carrier == runner.OwnerClientId
                            && hit.IsChildOf(saw.CarryRoot)) { heldCutter = true; break; }
                if (!heldCutter) return false;
            }
            return true;
        }

        void ClearAttack()
        {
            if (victim && victim.IsSpawned && victim.IsServer && victim.State == PlayerState.EscapeCaptured)
                victim.ServerFinishEscapeThrow();
            victim = null;
            attack = default;
            if (captureCable) captureCable.enabled = false;
            if (aimRing) aimRing.enabled = false;
            if (extendedForearm) extendedForearm.gameObject.SetActive(false);
            if (graspingHand) graspingHand.gameObject.SetActive(false);
        }

        internal static Vector3 ThrowPosition(Vector3 start, Vector3 end, float t)
        {
            t = Mathf.Clamp01(t);
            float arc = Mathf.Clamp(Vector3.Distance(start, end) * .04f, .35f, 1.6f);
            return Vector3.Lerp(start, end, t) + Vector3.up * (4f * arc * t * (1f - t));
        }

        internal static float ThrowPitch(float time)
        {
            if (time < .72f) return Mathf.Lerp(-60f, -145f, Mathf.SmoothStep(0, 1, time / .72f));
            if (time <= 1f) return Mathf.Lerp(-145f, -35f, Mathf.SmoothStep(0, 1, (time - .72f) / .28f));
            return Mathf.Lerp(-35f, 25f, Mathf.SmoothStep(0, 1, (time - 1f) / .2f));
        }

        Vector3 ThrowHandPosition(float time)
        {
            var rotation = grabArm.rotation * Quaternion.Euler(ThrowPitch(time), 0, 0) * Quaternion.Inverse(grabArm.rotation);
            return grabArm.position + rotation * (Hand - grabArm.position);
        }

        // Uses the actual attack presentation while the player remains free to walk around a dummy.
        internal void ResetAttackPreview() { ClearAttack(); previewAttack = default; }
        internal bool SampleAttackPreview(float time, Vector3 marked, Vector3 side, Transform dummy, bool caught, out bool severed)
        {
            float warning = Warning(Reach.Wire), impact = warning + StrikeTime;
            var raised = grabArm.rotation * Quaternion.Euler(-60f, 0, 0) * Quaternion.Inverse(grabArm.rotation);
            var origin = grabArm.position + raised * (Hand - grabArm.position);
            var cue = new AttackCue { Actor = ulong.MaxValue, Reach = Reach.Wire, Origin = origin,
                MarkedFeet = marked, Aim = marked + Vector3.up, Release = marked + Vector3.up };
            cue.Phase = time < warning ? AttackPhase.Warning : time < impact ? AttackPhase.Strike :
                !caught ? AttackPhase.Retract : time < impact + HoldTime ? AttackPhase.Grab : AttackPhase.Throw;
            cue.Started = cue.Phase == AttackPhase.Warning ? 0 : cue.Phase == AttackPhase.Strike ? warning :
                cue.Phase == AttackPhase.Throw ? impact + HoldTime : impact;
            if (cue.Phase == AttackPhase.Throw)
            {
                cue.Release = ThrowHandPosition(1f) - Vector3.up * .65f;
                cue.Drop = marked + side * 5 - Vector3.up * 8;
            }
            previewAttack = cue;
            previewAttackTime = time;
            PresentAttack(cue, time);
            severed = caught && time >= impact + SeverTime;
            dummy.position = marked;
            if (!caught) dummy.position += side * (2.1f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(warning * .5f, warning * .9f, time)));
            else if (time >= impact)
            {
                float t = time - impact;
                dummy.position = t < HoldTime ? Vector3.Lerp(marked, Hand - Vector3.up * .65f, Mathf.SmoothStep(0, 1, t / SeverTime))
                    : t < HoldTime + SwingTime ? Hand - Vector3.up * .65f : ThrowPosition(cue.Release, cue.Drop, (t - HoldTime - SwingTime) / FlightTime);
                if (t < HoldTime) StretchHand(Hand, dummy.position, marked - origin);
            }
            return time >= impact + (caught ? HoldTime + SwingTime + FlightTime : RetractTime) + .8f;
        }

        void PresentAttack(AttackCue cue, double clock)
        {
            if (!captureCable || !grabArm || !grabTip || !aimRing || !extendedForearm || !graspingHand) return;
            bool aiming = cue.Phase == AttackPhase.Warning || cue.Phase == AttackPhase.Strike;
            bool stretching = cue.Phase == AttackPhase.Strike || cue.Phase == AttackPhase.Retract || cue.Phase == AttackPhase.Grab;
            captureCable.enabled = aimRing.enabled = aiming;
            extendedForearm.gameObject.SetActive(stretching);
            graspingHand.gameObject.SetActive(stretching);
            if (cue.Phase == AttackPhase.None) return;
            if (cue.Phase == AttackPhase.Throw && clock - cue.Started < SwingTime)
            {
                aimRing.enabled = true;
                aimRing.transform.SetPositionAndRotation(cue.Drop + Vector3.up * .07f, Quaternion.Euler(90, 0, 0));
                captureCable.enabled = true; captureCable.SetPosition(0, Hand); captureCable.SetPosition(1, cue.Drop);
            }
            float elapsed = Mathf.Max(0f, (float)(clock - cue.Started));
            if (cue.Phase == AttackPhase.Throw)
            {
                SampleGait(0, 0);
                grabArm.localRotation *= Quaternion.Euler(ThrowPitch(elapsed / SwingTime), 0, 0);
            }
            else
            {
                float lift = cue.Phase == AttackPhase.Warning ? Mathf.Clamp01(elapsed / .4f) : 1f;
                grabArm.localRotation *= Quaternion.Euler(-60f * lift, 0f, 0f);
            }
            Vector3 hand = Hand;
            if (aiming)
            {
                if (cue.Phase == AttackPhase.Warning && soundedAt != cue.Started) { soundedAt = cue.Started; PlayStep(.65f); }
                captureCable.SetPosition(0, cue.Origin);
                captureCable.SetPosition(1, cue.Aim);
                aimRing.transform.SetPositionAndRotation(cue.MarkedFeet + Vector3.up * .07f, Quaternion.Euler(90f, 0f, 0f));
                aimRing.widthMultiplier = .065f + .02f * Mathf.Sin(elapsed * 13f);
                if (cue.Phase == AttackPhase.Warning) return;
            }
            if (cue.Phase == AttackPhase.Strike || cue.Phase == AttackPhase.Retract)
            {
                var tip = cue.Phase == AttackPhase.Strike ? Vector3.Lerp(cue.Origin, cue.Aim, elapsed / StrikeTime)
                    : Vector3.Lerp(cue.Release, hand, Mathf.SmoothStep(0f, 1f, elapsed / RetractTime));
                StretchHand(hand, tip, cue.Aim - cue.Origin);
                return;
            }
            var actor = victim;
            if (!actor || actor.OwnerClientId != cue.Actor)
            {
                actor = null;
                foreach (var player in NetworkPlayer.All)
                    if (player && player.IsSpawned && player.OwnerClientId == cue.Actor) { actor = player; break; }
            }
            if (!actor) return;
            Vector3 position = cue.Phase == AttackPhase.Grab
                ? Vector3.Lerp(cue.Aim, hand - Vector3.up * .65f, Mathf.SmoothStep(0f, 1f, elapsed / SeverTime))
                : elapsed < SwingTime ? hand - Vector3.up * .65f
                : cue.Stopped ? cue.StopPosition : ThrowPosition(cue.Release, cue.Drop, (elapsed - SwingTime) / FlightTime);
            if (cue.Phase == AttackPhase.Grab)
                StretchHand(hand, Vector3.Lerp(cue.Release, hand, Mathf.SmoothStep(0f, 1f, elapsed / SeverTime)), cue.Release - cue.Origin);
            // The player's normal owner-authoritative NetworkTransform carries the body to observers.
            if (actor.IsOwner && actor.State == PlayerState.EscapeCaptured)
            {
                var mover = actor.GetComponent<PlayerMover>();
                mover.Simulated = false;
                actor.transform.position = position;
            }
        }

        void StretchHand(Vector3 hand, Vector3 tip, Vector3 direction)
        {
            Vector3 segment = tip - hand;
            var facing = Quaternion.LookRotation(segment.sqrMagnitude > .0001f ? segment : direction.sqrMagnitude > .0001f ? direction : transform.forward);
            extendedForearm.SetPositionAndRotation((hand + tip) * .5f, facing);
            extendedForearm.localScale = new Vector3(.38f, .34f, Mathf.Max(.01f, segment.magnitude));
            graspingHand.SetPositionAndRotation(tip, facing);
        }
    }
}
