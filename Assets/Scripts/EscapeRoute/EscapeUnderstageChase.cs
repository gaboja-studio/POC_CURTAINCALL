using CurtainCall.Network.PlayerSync;
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
        [SerializeField] Animation gait;
        [SerializeField] AnimationClip upright, crouched, runClip;
        [SerializeField] AudioSource footstep;
        static CurtainCall.Settings.EscapeRouteSettings.PursuerTuning Tuning => CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer;
        float walkSpeed => Mathf.Max(.1f, Tuning.walkSpeed);
        float crouchSpeed => Mathf.Max(.1f, Tuning.crouchSpeed);
        float runSpeed => Mathf.Max(walkSpeed, Tuning.runSpeed);
        [SerializeField, Min(.1f)] float stoppingDistance = 2f;

        NavMeshAgent agent;
        CapsuleCollider body;
        AnimationState tall, low, fast;
        float nextRepath, stride, stepProgress, stepVolume, stepPitch;
        Vector3 previousPosition;
        NetworkPlayer target;
        float duckWeight, runWeight, runUntil, nextRun;
        uint stepNumber;
        double firstWalkRound = -1;
        float firstWalkMetres;
        bool firstChargeFinished;
        internal bool InShowcase { get; set; }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            routeAreas = agent.areaMask;
            if (cartLink) agent.autoTraverseOffMeshLink = false;
            body = GetComponent<CapsuleCollider>();
            tall = gait[upright.name];
            low = gait[crouched.name];
            fast = runClip ? gait[runClip.name] : null;
            stepVolume = footstep.volume;
            stepPitch = footstep.pitch;
            // Keep the 3D source for direction. The player-to-pursuer distance sets loudness.
            footstep.rolloffMode = AudioRolloffMode.Custom;
            footstep.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
        }

        void LateUpdate()
        {
            var net = NetworkManager.Singleton;
            var timer = EscapeRouteTimer.Current;
            Listen(net && net.IsListening && !net.IsServer ? net.CustomMessagingManager : null);
            if (!net || !net.IsListening || !timer || !timer.HasStarted || !EscapeCampaignContent.IsTraversalActive)
            {
                ClearAttack();
                StopChase();
                return;
            }
            if (!breach.EntranceComplete) StopChase();
            if (!net.IsServer)
            {
                PresentRemote(timer);
                if (hasSnapshot && latest.Round == timer.StartedAt) PresentAttack(latest.Attack, timer.Clock);
                return;
            }
            if (breach.EntranceComplete) AdvanceHost();
            if (bypass) bypass.SetShutter(usingBypass, transform.position, body);
            PresentAttack(attack, timer.Clock);
            Publish(net, timer);
        }

        internal NetworkPlayer ChooseTarget()
        {
            NetworkPlayer best = null;
            float bestDistance = float.PositiveInfinity;
            bool bestSafe = true;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                if (!InShowcase && (fullRoute ? fullRoute.InTransit(player) || fullRoute.HasDeparted(player)
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
            if (!InShowcase && boss && boss.AdvanceHost()) return;
            if (jumping) { AdvanceCartJump(Time.deltaTime); return; }
            double round = EscapeRouteTimer.Current.StartedAt;
            bool carrying = attack.Phase == AttackPhase.Grab || attack.Phase == AttackPhase.Throw;
            bool committed = attack.Phase != AttackPhase.None;
            if (!committed)
            {
                ChooseTarget();
                usingBypass = !InShowcase && !target && (fullRoute ? fullRoute.Rearmost() != null : bypass && bypass.Active(round));
            }
            if (!InShowcase && fullRoute && fullRoute.Following) { AdvanceServiceRoute(); return; }
            bool divert = usingBypass;
            if (!divert && !target && !committed) { StopChase(); return; }
            if (round != firstWalkRound) { firstWalkRound = round; firstWalkMetres = 0f; firstChargeFinished = false; }
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
            float clearance = Vector3.Distance(chest, lowPassage.ClosestPoint(chest));
            float duck = 1f - Mathf.SmoothStep(0f, 1f, clearance / 2f);
            bool hurry = false;
            Vector3 watch = Vector3.zero;
            var feet = carrying ? refugeApproach.position : committed ? attack.Aim : divert ? fullRoute ? fullRoute.Entry : bypass.Destination(transform.position, out hurry, out watch)
                : bypass && !InShowcase ? bypass.GunPosition(target) : target.transform.position;
            bool safe = !divert && (carrying || (committed ? attack.Reach == Reach.Boarding : InsideRefuge(feet) || bypass && bypass.AtGun(target)));
            var goal = safe ? refugeApproach.position : feet;
            if (!InShowcase && (bypass || fullRoute) && UpdateAttack(divert, safe, ref goal, ref watch))
            {
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
            // A periodic destination refresh can briefly leave the current path pending.
            bool hasRoute = agent.pathPending ? runUntil > 0f : agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete;
            bool clear = !safe && clearance > 2.5f && Vector3.Distance(transform.position, refugeApproach.position) > 3f &&
                hasRoute && Vector3.Dot(transform.forward, toRunner.normalized) > .7f;
            bool burst = fast != null && (InShowcase || firstWalkMetres >= Tuning.firstWalkMetres) &&
                (divert ? hurry && agent.hasPath : WantsRun(Time.time, toRunner.magnitude, clear));
            agent.speed = Speed(duck, burst ? 1f : 0f);
            SetBodyHeight(duck);

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + .15f;
                SetRouteAreas(goal, safe || divert);
                agent.stoppingDistance = safe || divert ? .2f : stoppingDistance;
                if (NavMesh.SamplePosition(goal, out var hit, 2.5f, agent.areaMask))
                    agent.SetDestination(hit.position);
                else agent.ResetPath();
            }

            var now = transform.position;
            float distance = Vector3.Distance(now, previousPosition);
            if (!InShowcase) firstWalkMetres += distance;
            previousPosition = now;
            float run = Mathf.InverseLerp(walkSpeed, runSpeed, agent.velocity.magnitude);
            AdvanceGait(distance, duck, run, agent.velocity.sqrMagnitude < .01f, Time.deltaTime);
            if (divert && fullRoute && fullRoute.TryEnter(transform.position)) agent.enabled = false;
        }

        void AdvanceServiceRoute()
        {
            agent.enabled = false;
            body.enabled = true;
            SetBodyHeight(0);
            usingBypass = true;
            if ((attack.Phase == AttackPhase.None || attack.Phase == AttackPhase.Grab) && fullRoute.TryJump(out var jumpFrom, out var jumpTo))
            {
                BeginServiceJump(jumpFrom, jumpTo);
                return;
            }
            var runner = fullRoute.Rearmost();
            Vector3 goal = transform.position, watch = Vector3.zero;
            bool acting = UpdateAttack(true, runner && fullRoute.AtStation(runner), ref goal, ref watch);
            var before = transform.position;
            float run = 0;
            if (!acting && attack.Phase == AttackPhase.Grab)
                transform.position = fullRoute.CarryToNextBay(before, walkSpeed, Time.deltaTime, EscapeRouteTimer.Current.Clock);
            else if (!acting && runner)
                transform.position = fullRoute.Advance(before, walkSpeed,
                    fullRoute.FinalSprint ? Tuning.finaleRunSpeed : runSpeed, Time.deltaTime, EscapeRouteTimer.Current.Clock, out run);
            Vector3 forward = acting ? watch : Vector3.ProjectOnPlane(transform.position - before, Vector3.up);
            if (forward.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward), 140f * Time.deltaTime);
            float distance = Vector3.Distance(before, transform.position);
            AdvanceGait(distance, 0, run, distance < .001f, Time.deltaTime);
            previousPosition = transform.position;
        }

        bool WantsRun(float now, float gap, bool clear)
        {
            if (!firstChargeFinished && !InShowcase)
            {
                if (gap > Tuning.runStopGap) return clear;
                firstChargeFinished = true;
                nextRun = now + Tuning.runRestSeconds;
            }
            if (runUntil > 0f)
            {
                if (clear && gap > Tuning.runStopGap && now < runUntil) return true;
                runUntil = 0f;
                nextRun = now + Tuning.runRestSeconds;
            }
            if (!clear || gap < Tuning.runStartGap || now < nextRun) return false;
            runUntil = now + Tuning.runBurstSeconds;
            return true;
        }

        internal void AdvanceGait(float distance, float duck, float run, bool resting, float dt)
        {
            float cycles = distance / Mathf.Lerp(2.4f, 3.2f, run);
            stride = Mathf.Repeat(stride + cycles, 1f);
            stepProgress += cycles * 2f;
            if (stepProgress >= 1f)
            {
                stepProgress %= 1f;
                stepNumber++;
                PlayStep();
            }
            if (resting) stride = Mathf.MoveTowards(stride, stride < .5f ? 0f : 1f, dt * 2f) % 1f;
            SampleGait(stride, duck, run);
        }

        internal void SampleGait(float phase, float duck, float run = 0f)
        {
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
        internal bool Capturing => attack.Phase == AttackPhase.Grab || attack.Phase == AttackPhase.Throw;
        internal void BossBody(bool alive, float lower = 0)
        {
            agent.enabled = false; body.enabled = alive;
            body.height = Mathf.Max(2.8f, 5f - lower); body.center = Vector3.up * (body.height * .5f);
        }
        internal void CancelBossAttack() => ClearAttack();
        internal bool BossAttack(NetworkPlayer runner)
        {
            if (!runner && attack.Phase == AttackPhase.None) return false;
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
        internal void SampleAction(AnimationClip clip, float time) => clip.SampleAnimation(gait.gameObject, time);

        internal void PlayStep(float pitchScale = 1f)
        {
            var player = NetworkPlayer.Local;
            if (!player) return;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            float referenceDistance = Mathf.Max(.01f, footstep.minDistance);
            float fade = 1f - Mathf.Clamp01(distance / Mathf.Max(referenceDistance, footstep.maxDistance));
            footstep.volume = stepVolume * fade * referenceDistance / (referenceDistance + distance);
            footstep.pitch = stepPitch * pitchScale;
            footstep.Play();
        }

        internal void RestPose()
        {
            runUntil = nextRun = stride = stepProgress = 0f;
            if (tall == null) return;
            footstep.Stop();
            SampleGait(0f, 0f);
            gait.Stop();
        }

        void OnDisable()
        {
            ClearAttack();
            usingBypass = false;
            if (bypass) bypass.SetShutter(false, transform.position);
            Listen(null);
            StopChase();
            target = null;
            stepNumber = 0;
            landingNumber = 0;
            nextSend = 0;
        }
    }
}
