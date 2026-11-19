using CurtainCall.Network.PlayerSync;
using CurtainCall.Sound;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.EscapeRoute
{
    /// <summary>The host drives one pursuer from the Understage breach through Escape A.</summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed partial class EscapeUnderstageChase : MonoBehaviour
    {
        [SerializeField] EscapeUnderstageBreach breach;
        [SerializeField] BoxCollider lowPassage;
        [SerializeField] BoxCollider refuge;
        [SerializeField] Transform refugeApproach;
        [SerializeField] UnderstageCannonBypass bypass;
        [SerializeField] EscapePursuerRoute fullRoute;
        [SerializeField] EscapePursuerBoss boss;
        [SerializeField] EscapePursuerWalls walls;
        [SerializeField] Animation gait;
        [SerializeField] AnimationClip upright, crouched, runClip;
        [SerializeField] AudioSource footstep;
        [SerializeField] AudioClip alternateFootstep, bodyImpact;
        [SerializeField] AudioSource wallImpact;
        static CurtainCall.Settings.EscapeRouteSettings.PursuerTuning Tuning => CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer;
        float walkSpeed => Mathf.Max(.1f, Tuning.walkSpeed);
        float crouchSpeed => Mathf.Max(.1f, Tuning.crouchSpeed);
        float runSpeed => Mathf.Max(walkSpeed, Tuning.runSpeed);
        [SerializeField, Min(.1f)] float stoppingDistance = 2f;

        NavMeshAgent agent;
        CapsuleCollider body;
        AnimationState tall, low, fast;
        float nextRepath, stride, stepVolume, stepPitch;
        AudioSource impactVoice;
        double nextFootfall = double.NegativeInfinity;
        uint audibleFootfalls;
        Vector3 previousPosition;
        NetworkPlayer target;
        float duckWeight, runWeight;
        uint stepNumber;
        double openingRound = -1;
        double revealedAt;
        bool openingRoared;
        bool charging;
        internal bool InShowcase { get; set; }
        public static EscapeUnderstageChase Current { get; private set; }
        internal bool RunnerIsOnRoute(NetworkPlayer player) => fullRoute && fullRoute.IsRouteRunner(player);

        void OnEnable()
        {
            if (!boss || !boss.IsReinforcement) Current = this;
        }

        void Awake()
        {
            if (doll) CurtainCall.WorldIndicators.ActorShadowPresenter.AttachPursuer(this, doll.transform, true);
            agent = GetComponent<NavMeshAgent>();
            agent.autoBraking = false;
            agent.acceleration = 24f;
            agent.angularSpeed = 300f;
            routeAreas = agent.areaMask;
            if (cartLink) agent.autoTraverseOffMeshLink = false;
            body = GetComponent<CapsuleCollider>();
            FindCloseCaptureModel();
            tall = gait[upright.name];
            low = gait[crouched.name];
            fast = runClip ? gait[runClip.name] : null;
            stepVolume = footstep.volume;
            stepPitch = footstep.pitch;
            // Keep the 3D source for direction. The player-to-pursuer distance sets loudness.
            footstep.rolloffMode = AudioRolloffMode.Custom;
            footstep.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
            footstep.dopplerLevel = 0;
            // Impact pitch must not retune or interrupt the tail of a walking step.
            impactVoice = footstep.gameObject.AddComponent<AudioSource>();
            impactVoice.playOnAwake = false;
            impactVoice.outputAudioMixerGroup = footstep.outputAudioMixerGroup;
            impactVoice.spatialBlend = footstep.spatialBlend;
            impactVoice.minDistance = footstep.minDistance;
            impactVoice.maxDistance = footstep.maxDistance;
            impactVoice.spread = footstep.spread;
            impactVoice.priority = footstep.priority;
            impactVoice.dopplerLevel = 0;
            impactVoice.rolloffMode = AudioRolloffMode.Custom;
            impactVoice.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
        }

        void LateUpdate()
        {
            var net = NetworkManager.Singleton;
            var timer = EscapeRouteTimer.Current;
            Listen(net && net.IsListening && !net.IsServer ? net.CustomMessagingManager : null);
            if (!net || !net.IsListening || !timer || !timer.HasStarted || !EscapeCampaignContent.IsTraversalActive)
            {
                ClearAttack();
                ResetThreat();
                StopChase();
                return;
            }
            bool finale = !InShowcase && boss && boss.TakesOverChase;
            if (!finale && !breach.EntranceComplete) StopChase();
            if (!net.IsServer)
            {
                PresentRemote(timer);
                PresentRemoteCues(timer);
                PresentWallStrike();
                return;
            }
            if (finale)
            {
                FinishStalledCapture(timer.Clock);
                boss.AdvanceHost();
            }
            else if (breach.EntranceComplete)
            {
                FinishStalledCapture(timer.Clock);
                AdvanceHost();
            }
            if (bypass && (!boss || !boss.IsReinforcement)) bypass.SetShutter(usingBypass, transform.position, body);
            PresentAttack(attack, timer.Clock);
            PresentThreat(threat, timer.Clock);
            PresentWallStrike();
            Publish(net, timer);
        }

        void PresentRemoteCues(EscapeRouteTimer timer)
        {
            if (CurrentSnapshot(timer))
            {
                PresentAttack(latest.Attack, timer.Clock);
                PresentThreat(latest.Threat, timer.Clock);
                return;
            }
            // Presentation only: a client must not finish a captured player's authoritative state.
            // Invalidate these even if the clock's reset and restart both arrived between frames.
            HideChaseWorldUi();
            if (captureCable) captureCable.enabled = false;
            if (aimRing) aimRing.enabled = false;
            if (extendedForearm) extendedForearm.gameObject.SetActive(false);
            if (graspingHand) graspingHand.gameObject.SetActive(false);
            HideDebrisProps();
            if (threatAudio) threatAudio.Stop();
        }

        internal NetworkPlayer ChooseTarget()
        {
            if (!InShowcase && fullRoute && fullRoute.TeamNeedsPursuit) { target = null; return null; }
            NetworkPlayer best = null;
            float bestDistance = float.PositiveInfinity;
            bool bestSafe = true;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                if (!InShowcase && (fullRoute ? fullRoute.IsRouteRunner(player)
                    : bypass && (bypass.InTransit(player) || bypass.HasCrossed(player)))) continue;
                var feet = player.transform.position;
                bool safe = InsideRefuge(feet);
                // Keep the current runner unless another exposed runner is clearly closer.
                float distance = Vector3.Distance(transform.position, feet) - (player == target ? 1.5f : 0f);
                if (best && (safe && !bestSafe || safe == bestSafe &&
                    (distance > bestDistance || distance == bestDistance && player.OwnerClientId > best.OwnerClientId))) continue;
                best = player;
                bestDistance = distance;
                bestSafe = safe;
            }
            target = best;
            return target;
        }

        bool InsideRefuge(Vector3 feet) => refuge && (refuge.ClosestPoint(feet) - feet).sqrMagnitude < .001f;

        void AdvanceHost()
        {
            SampleConveyorWait();
            if (!InShowcase && fullRoute) fullRoute.SampleProgress(EscapeRouteTimer.Current.Clock);
            if (!InShowcase && AdvanceThreat())
            {
                RecordEncounterPressure(EscapeRouteTimer.Current.Clock);
                return;
            }
            if (!InShowcase && AdvanceOpening()) return;
            if (jumping) { AdvanceCartJump(Time.deltaTime); return; }
            if (!InShowcase && boss && boss.AdvanceHost()) return;
            double round = EscapeRouteTimer.Current.StartedAt;
            bool carrying = attack.Phase == AttackPhase.Grab || attack.Phase == AttackPhase.Throw;
            bool committed = attack.Phase != AttackPhase.None;
            if (!committed)
            {
                ChooseTarget();
                usingBypass = !InShowcase && !target && (fullRoute ? fullRoute.SelectTarget() != null && !fullRoute.TargetInHall : bypass && bypass.Active(round));
            }
            if (!InShowcase && fullRoute && fullRoute.Following) { AdvanceServiceRoute(Time.deltaTime); return; }
            bool divert = usingBypass;
            if (!divert && !target && !committed) { StopChase(); return; }
            if (!agent.enabled)
            {
                agent.enabled = true;
                previousPosition = transform.position;
            }
            if (!agent.isOnNavMesh) return;
            if (cartLink && agent.isOnOffMeshLink)
            {
                if (agent.currentOffMeshLinkData.owner == cartLink) BeginCartJump();
                else agent.CompleteOffMeshLink();
                return;
            }
            body.enabled = true;

            Vector3 chest = transform.position + Vector3.up;
            float clearance = lowPassage && lowPassage.enabled && lowPassage.gameObject.activeInHierarchy
                ? Vector3.Distance(chest, lowPassage.ClosestPoint(chest)) : float.PositiveInfinity;
            float duck = 1f - Mathf.SmoothStep(0f, 1f, clearance / 2f);
            bool hurry = false;
            Vector3 watch = Vector3.zero;
            var feet = carrying ? refugeApproach.position : committed ? attack.Aim : divert ? fullRoute ? fullRoute.Entry : bypass.Destination(transform.position, out hurry, out watch)
                : bypass && !InShowcase ? bypass.GunPosition(target) : target.transform.position;
            bool safe = !divert && (carrying || (committed ? attack.Reach == Reach.Boarding : InsideRefuge(feet) || bypass && bypass.AtGun(target)));
            var goal = safe ? refugeApproach.position : feet;
            if (!InShowcase && !committed && TryDebrisAttack(target)) return;
            if (!InShowcase && (bypass || fullRoute) && UpdateAttack(divert, safe, ref goal, ref watch))
            {
                if (attack.Phase != AttackPhase.None) ResetHallStrikeApproach();
                agent.ResetPath();
                if (watch.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(watch), 140f * Time.deltaTime);
                AdvanceGait(0f, duck, 0f, true, Time.deltaTime);
                previousPosition = transform.position;
                return;
            }
            Vector3 toRunner = Vector3.ProjectOnPlane(feet - transform.position, Vector3.up);
            bool atDoor = safe && (transform.position - refugeApproach.position).sqrMagnitude < .55f * .55f;
            bool atLookout = divert && watch.sqrMagnitude > .01f && (goal - transform.position).sqrMagnitude < .55f * .55f;
            agent.updateRotation = !atDoor && !atLookout;
            if (atDoor) watch = toRunner;
            if (!agent.updateRotation && watch.sqrMagnitude > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(watch), 75f * Time.deltaTime);
            bool burst = fast != null && (InShowcase || charging);
            agent.speed = Speed(duck, burst ? 1f : 0f) * PressureSpeed(target);
            SetBodyHeight(duck);

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + .15f;
                SetRouteAreas(goal, safe || divert);
                // Body visibility can be clear while a jamb blocks the actual hand. Keep a
                // reachable angle once found, including when the detour moves briefly away.
                bool hall = !InShowcase && !safe && !divert && !committed && target && (bypass || fullRoute);
                bool close = hall && toRunner.sqrMagnitude <= Tuning.contactRange * Tuning.contactRange;
                if (!hall) ResetHallStrikeApproach();
                Vector3 flankGoal = goal;
                bool flank = hall && TryHallStrikeApproach(target, feet, close, out flankGoal);
                agent.stoppingDistance = safe || divert || close || flank ? .2f : stoppingDistance;
                if (flank) agent.SetDestination(flankGoal);
                else if (EscapePursuerNavigation.TryNavApproach(agent, goal, out var approach))
                    agent.SetDestination(approach);
                // Keep a valid existing path while a moving/suspended target has no sample.
                // Clearing it every 150 ms stranded the pursuer at the first pole.
            }

            var now = transform.position;
            float distance = Vector3.Distance(now, previousPosition);
            previousPosition = now;
            float run = Mathf.InverseLerp(walkSpeed, runSpeed, agent.velocity.magnitude);
            AdvanceGait(distance, duck, run, agent.velocity.sqrMagnitude < .01f, Time.deltaTime);
            if (divert && fullRoute && fullRoute.TryEnter(transform.position)) agent.enabled = false;
        }

        bool AdvanceOpening()
        {
            var timer = EscapeRouteTimer.Current;
            if (openingRound != timer.StartedAt)
            {
                openingRound = timer.StartedAt;
                revealedAt = timer.Clock;
                openingRoared = charging = false;
            }
            if (openingRoared) return false;
            var runner = ChooseTarget();
            if (!runner && fullRoute) target = runner = fullRoute.SelectTarget();
            if (runner)
            {
                Vector3 facing = Vector3.ProjectOnPlane(PlayerFeet(runner) - transform.position, Vector3.up);
                if (facing.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 55f * Time.deltaTime);
            }
            body.enabled = true;
            SetBodyHeight(0);
            AdvanceGait(0, 0, 0, true, Time.deltaTime);
            if (timer.Clock - revealedAt >= Tuning.entranceRoarDelay)
            {
                openingRoared = true;
                BeginChargeWarning(runner);
            }
            return true;
        }

        void AdvanceServiceRoute(float dt)
        {
            routeDeltaTime = dt;
            agent.enabled = false;
            body.enabled = true;
            SetBodyHeight(0);
            usingBypass = true;
            var runner = fullRoute.SelectTarget();
            target = runner;
            bool returning = CanReturnToHall;
            if (returning) fullRoute.ApproachEncounter(0);
            if (AdvanceEncounter(runner, returning)) return;
            if (returning && TryResumeHall(runner)) return;
            if ((attack.Phase == AttackPhase.None || attack.Phase == AttackPhase.Grab) && fullRoute.TryJump(out var jumpFrom, out var jumpTo))
            {
                if (walls && !walls.PrepareJump(jumpFrom, jumpTo, EscapeRouteTimer.Current.Clock))
                { AdvanceGait(0, 0, 0, true, dt); return; }
                BeginServiceJump(jumpFrom, jumpTo);
                return;
            }
            Vector3 goal = transform.position, watch = Vector3.zero;
            if (!returning && CanAttackFromRoute && TryDebrisAttack(runner)) return;
            bool acting = !returning && UpdateAttack(true, runner && fullRoute.AtStation(runner), ref goal, ref watch);
            var before = transform.position;
            float run = 0;
            if (!acting && attack.Phase == AttackPhase.Grab)
            {
                fullRoute.CarryToNextBay(before, walkSpeed, 0, EscapeRouteTimer.Current.Clock);
                transform.position = AdvanceGroundRoute(before, walkSpeed, dt, EscapeRouteTimer.Current.Clock, out run);
            }
            else if (!acting && (runner || returning))
            {
                bool concealed = ConcealedFromRunners();
                float speed = concealed ? Mathf.Max(runSpeed, Tuning.concealedRunSpeed)
                    : fullRoute.FinalSprint ? Mathf.Max(runSpeed, Tuning.finaleRunSpeed) : runSpeed;
                transform.position = AdvanceGroundRoute(before, speed * (concealed ? 1f : PressureSpeed(runner)),
                    dt, EscapeRouteTimer.Current.Clock, out run);
            }
            Vector3 forward = acting ? watch : Vector3.ProjectOnPlane(transform.position - before, Vector3.up);
            if (forward.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward), 140f * dt);
            float distance = Vector3.Distance(before, transform.position);
            AdvanceGait(distance, 0, run, distance < .001f, dt);
            previousPosition = transform.position;
        }

        bool CanReturnToHall => fullRoute && fullRoute.ReturningToHall && !jumping
            && attack.Phase == AttackPhase.None && threat.Phase == ThreatPhase.None;

        bool TryResumeHall(NetworkPlayer runner)
        {
            if (!CanReturnToHall || !fullRoute.AtHallEntry(transform.position)) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = routeAreas };
            if (!NavMesh.SamplePosition(transform.position, out var entry, .2f, filter)) return false;
            if (runner)
            {
                // A piggyback rider can be above the floor sample radius. Use the
                // existing hall approach search radius before deciding it is disconnected.
                if (!NavMesh.SamplePosition(PlayerFeet(runner), out var goal, 2.5f, filter)
                    && !NavMesh.SamplePosition(PlayerFeet(runner), out goal, 8f, filter)) return false;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(entry.position, goal.position, filter, path)
                    || path.status != NavMeshPathStatus.PathComplete) return false;
            }
            // Re-enable only where the actor physically returned to the connected hall mesh.
            // Do not Warp or reset the round, pressure history, or player departure records.
            agent.areaMask = routeAreas;
            agent.enabled = true;
            if (!agent.isOnNavMesh || !fullRoute.TryLeaveAtEntry(transform.position, true))
            { agent.enabled = false; return false; }
            usingBypass = false; nextRepath = 0; previousPosition = transform.position;
            return true;
        }

        Vector3 AdvanceGroundRoute(Vector3 from, float speed, float dt, double now, out float run)
        {
            run = 0;
            float budget = Mathf.Max(0, speed * dt);
            // A long frame can cross several short route edges. Check each edge in order,
            // rather than sweeping one straight chord and then spending the budget around corners.
            for (int edge = 0; edge < fullRoute.Points.Count && budget > .00001f; edge++)
            {
                Vector3 nextAnchor = fullRoute.NextPosition;
                float requested = Mathf.Min(budget, Vector3.Distance(from, nextAnchor));
                if (requested < .00001f)
                {
                    fullRoute.AlignReachedAnchor(from);
                    break;
                }
                float allowed = walls ? walls.TravelLimit(from, nextAnchor, requested, now) : requested;
                if (allowed < .00001f) break;
                Vector3 next = fullRoute.Advance(from, walkSpeed, speed, allowed / speed, now, out float pace);
                float moved = Vector3.Distance(from, next);
                if (moved < .00001f) break; // A jump boundary, held finale beat, or reached target.
                from = next; budget = Mathf.Max(0, budget - moved); run = pace;
                if (allowed < requested - .00001f || moved < allowed - .00001f) break;
            }
            return from;
        }

        bool ConcealedFromRunners()
        {
            foreach (var player in NetworkPlayer.All)
                if (player && player.IsSpawned && player.State == PlayerState.Normal &&
                    ClearReach(transform.position + Vector3.up * 3f, PlayerFeet(player) + Vector3.up, player)) return false;
            return true;
        }

        internal void AdvanceGait(float distance, float duck, float run, bool resting, float dt)
        {
            bool wasResting = gaitResting;
            gaitResting = resting;
            // Stabilize only the displayed gait. Speed()/navigation keep their original
            // continuous pace; snapshots carry this same authored-clip choice to clients.
            float previousCycle = 0, distanceToChange = -1, phaseToChange = 0;
            if (AuthoredDoll)
            {
                float current = runWeight >= .5f ? 1 : 0;
                run = current > 0 ? (run <= .35f ? 0 : 1) : (run >= .65f ? 1 : 0);
                if (run != current && !wasResting && !resting)
                {
                    previousCycle = doll.CycleDistance(duck, current);
                    // Switch just after the next left/right plant, where the authored
                    // support-foot positions agree, instead of swapping mid-flight.
                    float nextPlant = (Mathf.Floor((stride - .05f) * 2) + 1) * .5f + .05f;
                    phaseToChange = nextPlant - stride;
                    distanceToChange = phaseToChange * previousCycle;
                    if (distance < distanceToChange) { run = current; distanceToChange = -1; }
                }
            }
            float cycleDistance = doll ? doll.CycleDistance(duck, run)
                : Mathf.Lerp(mascot ? mascot.walkCycleDistance : 2.4f, mascot ? mascot.runCycleDistance : 3.2f, run);
            // DollPose resolves the authoritative snapshot on clients as well as the host.
            if (AuthoredDoll && DollPose.Action == (int)EscapePursuerDoll.Motion.CarryWalk) cycleDistance = doll.walkCycleDistance;
            float cycles = resting ? 0 : Mathf.Max(0, distance) / Mathf.Max(.01f, cycleDistance);
            if (!resting && distanceToChange >= 0)
                cycles = phaseToChange + Mathf.Max(0, distance - distanceToChange) / Mathf.Max(.01f, cycleDistance);
            // Count actual half-cycle contact crossings. A separate distance accumulator
            // loses sync whenever the resting pose changes the animation phase.
            int contacts = Mathf.FloorToInt((stride + cycles) * 2f) - Mathf.FloorToInt(stride * 2f);
            stride = Mathf.Repeat(stride + cycles, 1f);
            if (contacts > 0)
            {
                stepNumber += (uint)contacts;
                PlayStep();
            }
            if (resting) stride = Mathf.MoveTowards(stride, stride < .5f ? 0f : 1f, dt * 2f) % 1f;
            SampleGait(stride, duck, run);
        }

        internal void SampleGait(float phase, float duck, float run = 0f)
        {
            stride = phase;
            if (InShowcase) gaitResting = false;
            duckWeight = duck;
            runWeight = fast != null ? run : 0f;
            tall.enabled = low.enabled = true;
            tall.speed = low.speed = 0f;
            tall.weight = (1f - duck) * (1f - runWeight);
            low.weight = duck;
            tall.normalizedTime = low.normalizedTime = phase;
            if (fast != null)
            {
                fast.enabled = true;
                fast.speed = 0f;
                fast.weight = (1f - duck) * runWeight;
                fast.normalizedTime = phase;
            }
            gait.Sample();
        }

        void StopChase()
        {
            HideRouteLandingWorldUi();
            if (!agent || !agent.enabled && !body.enabled) return;
            agent.enabled = false;
            jumping = false;
            jumpTime = 0f;
            agent.updatePosition = agent.updateRotation = true;
            body.enabled = false;
            nextRepath = 0f;
            RestPose();
        }

        void SetBodyHeight(float duck)
        {
            body.height = Mathf.Lerp(5f, 3.35f, duck);
            body.center = Vector3.up * (body.height * .5f);
        }

        internal float Speed(float duck, float run = 0f) => Mathf.Lerp(Mathf.Lerp(walkSpeed, runSpeed, run), crouchSpeed, duck);
        float Pressure(NetworkPlayer runner) => !InShowcase && fullRoute && EscapeRouteTimer.Current
            ? fullRoute.Escalation(runner, EscapeRouteTimer.Current.Clock) : 0f;
        internal float PressureSpeed(NetworkPlayer runner) => Mathf.Lerp(1f, Tuning.pressureMaxSpeed, Pressure(runner));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal float ReviewRouteProgress => fullRoute ? fullRoute.Progress : -1f;
        internal float ReviewPressure => Pressure(target);
        internal float ReviewEntranceTime => breach ? breach.SampleTime : 0;
        internal bool ReviewEntranceComplete => breach && breach.EntranceComplete;
        internal float ReviewRunWeight => runWeight;
        internal long ReviewTarget => target ? (long)target.OwnerClientId : -1;
        internal int ReviewGate => encounterIndex >= 0 ? encounters[encounterIndex].gate : -1;
        internal string ReviewAction => attack.Phase != AttackPhase.None ? attack.Phase.ToString() : threat.Phase.ToString();
        internal Vector3 ReviewAttackOrigin => attack.Origin;
        internal Vector3 ReviewAttackAim => attack.Aim;
        internal double ReviewAttackStarted => attack.Started;
        internal long ReviewAttackActor => attack.Phase != AttackPhase.None ? unchecked((long)attack.Actor)
            : threat.Phase != ThreatPhase.None ? unchecked((long)threat.Actor) : -1;
        internal bool ReviewConveyorWait(ulong actor) => WaitingForConveyor(actor);
#endif
        internal bool Capturing => attack.Phase == AttackPhase.Grab || attack.Phase == AttackPhase.Throw;
        internal void BossBody(bool alive, float lower = 0)
        {
            agent.enabled = false; body.enabled = alive;
            body.height = Mathf.Max(2.8f, 5f - lower); body.center = Vector3.up * (body.height * .5f);
        }
        internal void CancelBossAttack()
        {
            ClearAttack();
            // A hit/dash/ignition replaces the wind-up pose. Do not release a projectile
            // from an invisible throw, but keep anything that has already left the hand.
            if (threat.Phase == ThreatPhase.ChargeWarning || threat.Phase == ThreatPhase.DebrisWarning)
            {
                double cooldown = nextDebris;
                ResetThreat();
                var timer = EscapeRouteTimer.Current;
                nextDebris = System.Math.Max(cooldown, (timer ? timer.Clock : 0) + Tuning.debrisCooldown);
            }
            nextSend = 0;
        }
        internal bool BossAttack(NetworkPlayer runner)
        {
            target = runner;
            if (!runner && attack.Phase == AttackPhase.None) return false;
            if (TryDebrisAttack(runner, bossVolley: true)) return true;
            Vector3 goal = transform.position, watch = Vector3.zero;
            bool busy = UpdateAttack(true, false, ref goal, ref watch, runner);
            if (watch.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(watch), 220f * Time.deltaTime);
            return busy;
        }
        internal void PresentIgnition(Vector3 point)
        {
            grabArm.localRotation *= Quaternion.Euler(-55, 0, 0);
            extendedForearm.gameObject.SetActive(true); graspingHand.gameObject.SetActive(true);
            StretchHand(Hand, point, point - Hand);
        }
        internal void SampleAction(AnimationClip clip, float time)
        {
            clip.SampleAnimation(gait.gameObject, time);
            sampledAction = clip == jumpClip ? 7 : clip == landingClip ? 8 : 0;
            sampledTime = time / Mathf.Max(.01f, clip.length);
            sampledFrame = Time.frameCount;
        }

        internal void PlayStep(float pitchScale = 1f)
        {
            string cue = Mathf.Approximately(pitchScale, 1f) ? SoundCueIds.EscapeFootstep : SoundCueIds.EscapeLanding;
            if (SoundCatalog.Current != null && SoundCatalog.Current.Find(cue) != null)
            {
                if (Mathf.Approximately(pitchScale, 1f))
                {
                    if (Time.timeAsDouble < nextFootfall) return;
                    nextFootfall = Time.timeAsDouble + .45;
                }
                SoundManager.PlayAt(cue, transform);
                return;
            }
            var player = NetworkPlayer.Local;
            if (!player || !footstep || !footstep.clip) return;
            bool impact = !Mathf.Approximately(pitchScale, 1f);
            // Hidden service travel can run at 20 m/s. Coalesce its cues instead of
            // restarting a bass transient every 0.15 s; normal gait contacts are intact.
            if (!impact && Time.timeAsDouble < nextFootfall) return;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            float referenceDistance = Mathf.Max(.01f, footstep.minDistance);
            float fade = 1f - Mathf.Clamp01(distance / Mathf.Max(referenceDistance, footstep.maxDistance));
            float gain = Mathf.Min(1f, stepVolume * 1.65f) * fade * Mathf.Sqrt(referenceDistance / (referenceDistance + distance));
            var voice = impact && impactVoice ? impactVoice : footstep;
            var clip = impact && bodyImpact ? bodyImpact
                : !impact && alternateFootstep && (audibleFootfalls & 1) != 0 ? alternateFootstep : footstep.clip;
            voice.volume = 1f;
            // The authored impact already carries the low body; avoid sub-audible pitch.
            voice.pitch = impact ? (bodyImpact ? Mathf.Lerp(.9f, 1f, Mathf.InverseLerp(.55f, .8f, pitchScale)) : stepPitch * pitchScale) : stepPitch;
            voice.PlayOneShot(clip, gain);
            if (!impact) { nextFootfall = Time.timeAsDouble + .45; audibleFootfalls++; }
        }

        internal void PlayWallImpact(Vector3 contact)
        {
            if (SoundCatalog.Current != null && SoundCatalog.Current.Find(SoundCueIds.EscapeWallCollapse) != null)
            {
                SoundManager.PlayAt(SoundCueIds.EscapeWallCollapse, contact);
                return;
            }
            if (!wallImpact || !wallImpact.clip) { PlayStep(.55f); return; }
            wallImpact.transform.position = contact;
            wallImpact.time = 2.3f;
            wallImpact.Play();
            wallImpact.SetScheduledEndTime(AudioSettings.dspTime + .8);
        }

        internal void RestPose()
        {
            stride = 0f;
            nextFootfall = double.NegativeInfinity;
            audibleFootfalls = 0;
            if (tall == null) return;
            footstep.Stop();
            if (impactVoice) impactVoice.Stop();
            SampleGait(0f, 0f);
            gait.Stop();
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            ClearAttack();
            ResetThreat();
            ResetEncounters();
            threatRound = -1;
            usingBypass = false;
            if (bypass && (!boss || !boss.IsReinforcement)) bypass.SetShutter(false, transform.position);
            Listen(null);
            StopChase();
            target = null;
            stepNumber = 0;
            landingNumber = 0;
            nextSend = 0;
        }
    }
}
