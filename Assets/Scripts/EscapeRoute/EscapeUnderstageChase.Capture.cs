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
        float PrepareTime => Mathf.Max(0f, Tuning.grabPrepareSeconds);
        static float ArmRadius => Mathf.Max(.05f, Tuning.armRadius);

        enum AttackPhase : byte { None, Warning, Strike, Retract, Grab, Throw, Preparing }
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
        static bool Targeting(AttackPhase phase) => phase == AttackPhase.Preparing || phase == AttackPhase.Warning || phase == AttackPhase.Strike;

        public bool TargetsPlayer(NetworkPlayer player)
            => TargetsPlayerAt(player, EscapeRouteTimer.Current, NetworkManager.Singleton && !NetworkManager.Singleton.IsServer);

        bool TargetsPlayerAt(NetworkPlayer player, EscapeRouteTimer timer, bool remote)
        {
            if (!isActiveAndEnabled || InShowcase || !player || !player.IsSpawned || player.State != PlayerState.Normal
                || !timer || !timer.HasStarted || !CanUseAttackHand || WaitingForConveyor(player.OwnerClientId)) return false;
            if (remote ? !CurrentSnapshot(timer) : attackRound != timer.StartedAt) return false;
            var cue = remote ? latest.Attack : attack;
            return Targeting(cue.Phase) && cue.Actor == player.OwnerClientId && cue.Started >= timer.StartedAt;
        }

        NetworkPlayer AttackActor(AttackCue cue)
        {
            if (victim && victim.IsSpawned && victim.OwnerClientId == cue.Actor) return victim;
            foreach (var player in NetworkPlayer.All)
                if (player && player.IsSpawned && player.OwnerClientId == cue.Actor) return player;
            return null;
        }
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
        AttackPhase soundedPhase;
        bool throwSounded;
        CurtainCall.Sound.SoundHandle attackSound;
        float previousExtension, previousTorsoFlight;
        double nextAim;
        double attackRound = -1;
        NetworkPlayer hallApproachTarget;
        Vector3 hallApproachFeet, hallApproachGoal;
        float nextHallSearch;
        bool hasHallApproach;
        Vector3 Hand => doll ? doll.HandPosition : grabTip.position - grabArm.up * .45f;
        bool CanUseAttackHand => !boss || !boss.CombatStarted || !boss.IsCut(3) && !boss.IsCut(7) && !boss.Defeated;
        float Warning(Reach reach) => Mathf.Max(.2f, boss && boss.InBattle ? Tuning.bossAttackWarning
            : reach == Reach.Hall ? Tuning.hallWarning : reach == Reach.Boarding ? Tuning.stationWarning : Tuning.wireWarning);
        bool InTransit(NetworkPlayer player) => fullRoute ? fullRoute.InTransit(player) : bypass && bypass.InTransit(player);
        Vector3 PlayerFeet(NetworkPlayer player) => fullRoute ? fullRoute.PlayerFeet(player) : bypass ? bypass.GunPosition(player) : player.transform.position;

        // Complete a committed strike, then reconsider the team's movement.
        bool UpdateAttack(bool divert, bool boarding, ref Vector3 goal, ref Vector3 watch, NetworkPlayer forcedRunner = null)
        {
            if (!CanUseAttackHand) { ClearAttack(); return false; }
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
                        if (SweepProjectile(from, to, TorsoProjectileRadius, attack.Actor, out var impact))
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

            // Body contact cannot be avoided by orbiting faster than the turn/windup,
            // or by remaining beside the model during a missed strike's recovery.
            if (TryCloseCapture(now, ref watch)) return true;
            if (attack.Phase != AttackPhase.None) return AdvanceArm(now, ref watch);
            if (now < nextAim) return false;
            if (!CanAttackFromRoute && !(boss && boss.InBattle)) return false;
            var runner = forcedRunner ? forcedRunner : divert ? fullRoute ? fullRoute.SelectTarget() : bypass.RearmostRunner() : target;
            var fallbackReach = fullRoute && fullRoute.Following ? Reach.Wire : forcedRunner ? Reach.Hall
                : boarding ? Reach.Boarding : divert ? Reach.Wire : Reach.Hall;
            var reach = AttackReach(runner, fallbackReach);
            // The nearest visible survivor remains attackable on poles, seats, piggyback and
            // flight, even if a different player currently owns the route's progress target.
            if (!AttackCandidate(runner, reach))
            {
                runner = null;
                float best = float.PositiveInfinity;
                foreach (var candidate in NetworkPlayer.All)
                    if (AttackCandidate(candidate, AttackReach(candidate, fallbackReach)))
                    {
                        float d = (PlayerFeet(candidate) - transform.position).sqrMagnitude;
                        if (d < best) { best = d; runner = candidate; reach = AttackReach(candidate, fallbackReach); }
                    }
            }
            if (!runner) return false;

            Vector3 aim = PlayerFeet(runner);
            var delta = aim - transform.position;
            watch = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (Vector3.Angle(transform.forward, watch) > 25f) return true;
            var mover = runner.GetComponent<PlayerMover>();
            Vector3 markedFeet = aim;
            aim = GrabAimPoint(markedFeet, mover);
            Vector3 origin = GrabWindupOrigin();
            if (!ClearReach(origin, aim, runner)) return false;
            if (TryPressureWarning(runner)) return true;
            victim = runner;
            if (fullRoute) fullRoute.MarkPressured(runner);
            previousExtension = 0f;
            attack = new AttackCue { Phase = AttackPhase.Preparing, Reach = reach, Actor = runner.OwnerClientId, Started = now,
                Origin = origin, Aim = aim, MarkedFeet = markedFeet };
            nextSend = 0;
            return true;
        }

        // Presentation and combat share the same live body aim, including posture height.
        static Vector3 GrabAimPoint(Vector3 feet, PlayerMover mover) => feet + Vector3.up * (mover.BodyHeight * .68f);

        Vector3 GrabWindupOrigin()
        {
            if (doll) return doll.SocketAt(EscapePursuerDoll.Motion.GrabStretch, doll.graspTime * .6f, false);
            var raised = grabArm.rotation * Quaternion.Euler(-60f, 0f, 0f) * Quaternion.Inverse(grabArm.rotation);
            return grabArm.position + raised * (Hand - grabArm.position);
        }

        System.Func<Vector3, bool> StrikePositionFilter(NetworkPlayer runner, Vector3 feet)
        {
            Vector3 aim = runner && runner.TryGetComponent(out PlayerMover mover)
                ? GrabAimPoint(feet, mover) : feet + Vector3.up;
            // Sample the authored hand once per search, then rotate its offset toward the
            // target at each candidate. A clear chest ray can leave the windup hand inside
            // a concrete fin and keep selecting the same unusable attack position.
            Vector3 handOffset = Quaternion.Inverse(transform.rotation) * (GrabWindupOrigin() - transform.position);
            return at =>
            {
                Vector3 forward = Vector3.ProjectOnPlane(feet - at, Vector3.up);
                if (forward.sqrMagnitude < .09f) return false;
                Vector3 hand = at + Quaternion.LookRotation(forward) * handOffset;
                return ClearReach(at + Vector3.up * 3, aim, runner) && ClearReach(hand, aim, runner);
            };
        }

        bool TryHallStrikeApproach(NetworkPlayer runner, Vector3 feet, bool close, out Vector3 approach)
        {
            if (hallApproachTarget != runner || (feet - hallApproachFeet).sqrMagnitude > .36f)
            {
                hallApproachTarget = runner;
                hallApproachFeet = feet;
                hasHallApproach = false;
                nextHallSearch = 0;
            }
            if (hasHallApproach && (hallApproachGoal - transform.position).sqrMagnitude < .16f)
                hasHallApproach = false;
            if (!hasHallApproach && close && Time.time >= nextHallSearch && runner.TryGetComponent<PlayerMover>(out _))
            {
                nextHallSearch = Time.time + .6f;
                var clearStrike = StrikePositionFilter(runner, feet);
                hasHallApproach = EscapePursuerNavigation.TryNavStrikePosition(agent, feet, Tuning.contactRange, at =>
                    clearStrike(at), out hallApproachGoal);
            }
            approach = hallApproachGoal;
            return hasHallApproach;
        }

        void ResetHallStrikeApproach()
        {
            hallApproachTarget = null;
            hasHallApproach = false;
            nextHallSearch = 0;
        }

        Reach AttackReach(NetworkPlayer player, Reach fallback)
        {
            if (!player) return fallback;
            ulong actor = player.OwnerClientId;
            if (HumanCannonPoleSystem.Current && HumanCannonPoleSystem.Current.HeldBy(actor) >= 0
                || TightropeHangSystem.Current && TightropeHangSystem.Current.IsHanging(player)) return Reach.Wire;
            if (HumanCannonSystem.Current && HumanCannonSystem.Current.IsSeated(actor)) return Reach.Boarding;
            return fallback;
        }

        // The first hanging pole is deliberately across the chasm, outside the ordinary
        // exposed-floor reach. Keep its full authored arm reach when the director takes over.
        float AttackRange(NetworkPlayer player) => player && HumanCannonPoleSystem.Current
            && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0 ? Tuning.armRange
            : fullRoute && fullRoute.Following ? Tuning.exposedGrabRange : Tuning.armRange;

        bool AttackCandidate(NetworkPlayer player, Reach reach)
        {
            if (!player || !player.IsSpawned || player.State != PlayerState.Normal || WaitingForConveyor(player.OwnerClientId)
                || !player.TryGetComponent(out PlayerMover mover)) return false;
            Vector3 feet = PlayerFeet(player), delta = feet - transform.position;
            float range = AttackRange(player);
            bool close = reach == Reach.Hall ? Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude < Tuning.contactRange * Tuning.contactRange
                && Mathf.Abs(delta.y) < range : delta.sqrMagnitude < range * range;
            return close && ClearReach(transform.position + Vector3.up * 3f, GrabAimPoint(feet, mover), player);
        }

        bool AdvanceArm(double now, ref Vector3 watch)
        {
            if (attack.Phase == AttackPhase.Preparing)
            {
                var runner = AttackActor(attack);
                var reach = AttackReach(runner, attack.Reach);
                if (!CanUseAttackHand || !AttackCandidate(runner, reach))
                { ClearAttack(); nextAim = now + MissRecovery; nextSend = 0; return true; }
                attack.MarkedFeet = PlayerFeet(runner);
                attack.Aim = GrabAimPoint(attack.MarkedFeet, runner.GetComponent<PlayerMover>());
                watch = Vector3.ProjectOnPlane(attack.Aim - transform.position, Vector3.up);
                if (now - attack.Started < PrepareTime) return true;
                // Commit the latest real position and current hand pose. No velocity prediction.
                var origin = GrabWindupOrigin();
                if (!ClearReach(origin, attack.Aim, runner))
                { ClearAttack(); nextAim = now + MissRecovery; nextSend = 0; return true; }
                attack.Origin = origin;
                attack.Reach = reach;
                attack.Phase = AttackPhase.Warning;
                attack.Started = now;
                previousExtension = 0;
                nextSend = 0;
                return true;
            }
            watch = Vector3.ProjectOnPlane(attack.Aim - transform.position, Vector3.up);
            float elapsed = (float)(now - attack.Started);
            if (attack.Phase == AttackPhase.Warning)
            {
                if (WaitingForConveyor(attack.Actor))
                {
                    attack.Phase = AttackPhase.Retract;
                    attack.Started = now;
                    attack.Release = attack.Origin;
                    return true;
                }
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
                    if (!player || !player.IsSpawned || player.State != PlayerState.Normal || WaitingForConveyor(player.OwnerClientId)) continue;
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
                // Loose torn limbs and severed pursuer parts on the floor never shield a runner.
                if (hit.GetComponentInParent<EscapePursuerDebris>() || CurtainCall.Damage.DismemberedLimb.Contains(reachHits[i].collider)) continue;
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

        void FinishStalledCapture(double now)
        {
            if (!HoldingRunner) return;
            // Give positioning a second hold window. A broken path or route switch
            // must not leave an already captured player pending at the ending.
            double limit = attack.Phase == AttackPhase.Grab ? 2 * HoldTime : SwingTime + FlightTime + HoldTime;
            if (now - attack.Started < limit) return;
            ClearAttack();
            nextAim = now + MissRecovery;
            nextSend = 0;
        }

        void ClearAttack()
        {
            attackSound.Stop();
            soundedAt = -1;
            soundedPhase = AttackPhase.None;
            throwSounded = false;
            if (grabTarget) grabTarget.Hide();
            if (attackVolume) attackVolume.Hide();
            if (torsoLanding) torsoLanding.Hide();
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
            if (doll) return doll.SocketAt(EscapePursuerDoll.Motion.Throw, doll.releaseTime * Mathf.Clamp01(time), true);
            var rotation = grabArm.rotation * Quaternion.Euler(ThrowPitch(time), 0, 0) * Quaternion.Inverse(grabArm.rotation);
            return grabArm.position + rotation * (Hand - grabArm.position);
        }

        // Uses the actual attack presentation while the player remains free to walk around a dummy.
        internal void ResetAttackPreview() { ClearAttack(); previewAttack = default; }
        internal bool SampleAttackPreview(float time, Vector3 marked, Vector3 side, Transform dummy, bool caught, out bool severed)
        {
            float warning = PrepareTime + Warning(Reach.Wire), impact = warning + StrikeTime;
            bool alreadyLocked = time >= previewAttackTime && previewAttack.Phase != AttackPhase.None
                && previewAttack.Phase != AttackPhase.Preparing;
            var origin = alreadyLocked ? previewAttack.Origin : GrabWindupOrigin();
            var feet = alreadyLocked ? previewAttack.MarkedFeet : marked;
            var aim = alreadyLocked ? previewAttack.Aim : marked + Vector3.up * (1.7f * .68f);
            var cue = new AttackCue { Actor = ulong.MaxValue, Reach = Reach.Wire, Origin = origin,
                MarkedFeet = feet, Aim = aim, Release = aim };
            cue.Phase = time < PrepareTime ? AttackPhase.Preparing : time < warning ? AttackPhase.Warning : time < impact ? AttackPhase.Strike :
                !caught ? AttackPhase.Retract : time < impact + HoldTime ? AttackPhase.Grab : AttackPhase.Throw;
            cue.Started = cue.Phase == AttackPhase.Preparing ? 0 : cue.Phase == AttackPhase.Warning ? PrepareTime : cue.Phase == AttackPhase.Strike ? warning :
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
            if (!caught) dummy.position += side * (2.1f * Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(PrepareTime + Warning(Reach.Wire) * .5f, PrepareTime + Warning(Reach.Wire) * .9f, time)));
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
            float elapsed = Mathf.Max(0f, (float)(clock - cue.Started));
            if (cue.Started != soundedAt || cue.Phase != soundedPhase)
            {
                soundedAt = cue.Started;
                soundedPhase = cue.Phase;
                throwSounded = false;
                if (elapsed < .3f && cue.Phase == AttackPhase.Warning)
                    attackSound = CurtainCall.Sound.SoundManager.PlayAt(CurtainCall.Sound.SoundCueIds.EscapeArmWarning, transform);
                else if (elapsed < .3f && cue.Phase == AttackPhase.Grab)
                    attackSound = CurtainCall.Sound.SoundManager.PlayAt(CurtainCall.Sound.SoundCueIds.EscapeGrab, cue.Aim);
            }
            if (cue.Phase == AttackPhase.Throw && !throwSounded && elapsed >= SwingTime)
            {
                throwSounded = true;
                if (elapsed < SwingTime + .3f)
                    attackSound = CurtainCall.Sound.SoundManager.PlayAt(CurtainCall.Sound.SoundCueIds.EscapeThrow, cue.Release);
            }
            bool aiming = cue.Phase == AttackPhase.Warning || cue.Phase == AttackPhase.Strike;
            bool stretching = cue.Phase == AttackPhase.Strike || cue.Phase == AttackPhase.Retract || cue.Phase == AttackPhase.Grab;
            bool volume = PresentAttackVolume(cue, elapsed);
            if (captureCable) captureCable.enabled = WorldUiActive && CurtainCall.Settings.GameSettings.WorldUi.showGrabPath && aiming && !volume;
            PresentAttackTarget(cue, elapsed);
            bool legacyThrow = PresentEndpointWorldUi(ref torsoLanding,
                cue.Phase == AttackPhase.Throw && !cue.Stopped && elapsed < SwingTime + FlightTime,
                cue.Drop, TorsoProjectileRadius, elapsed / (SwingTime + FlightTime), out var support);
            if (aimRing && legacyThrow && elapsed < SwingTime)
            {
                aimRing.enabled = true;
                aimRing.transform.SetPositionAndRotation(support.point + support.normal * .07f,
                    Quaternion.FromToRotation(Vector3.up, support.normal) * Quaternion.Euler(90, 0, 0));
            }
            if (extendedForearm) extendedForearm.gameObject.SetActive(stretching);
            if (graspingHand) graspingHand.gameObject.SetActive(stretching);
            // UI prefab/legacy renderer availability must never gate the arm or captured player.
            if (!grabArm || !grabTip || !extendedForearm || !graspingHand) return;
            if (cue.Phase == AttackPhase.None) return;
            if (cue.Phase == AttackPhase.Throw)
            {
                SampleGait(0, 0);
                grabArm.localRotation *= Quaternion.Euler(ThrowPitch(elapsed / SwingTime), 0, 0);
            }
            else
            {
                float lift = cue.Phase == AttackPhase.Preparing ? 0 : cue.Phase == AttackPhase.Warning ? Mathf.Clamp01(elapsed / .4f) : 1f;
                grabArm.localRotation *= Quaternion.Euler(-60f * lift, 0f, 0f);
            }
            if (cue.Phase == AttackPhase.Preparing) return;
            Vector3 hand = Hand;
            if (aiming)
            {
                if (captureCable && captureCable.enabled)
                {
                    captureCable.SetPosition(0, cue.Origin);
                    captureCable.SetPosition(1, cue.Aim);
                }
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
