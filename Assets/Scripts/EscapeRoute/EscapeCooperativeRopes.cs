using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Sound;
using CurtainCall.Tightrope;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>외줄 전체의 공유 전기 예고와 소실, 하부 작업자 한 명과 상부 복귀 버튼. 이동·반동은 원본 기물을 사용한다.</summary>
    [DefaultExecutionOrder(-70)] // 줄타기/균형 판정 전에 지지 소실과 조작 반환을 적용한다.
    public sealed class EscapeCooperativeRopes : MonoBehaviour
    {
        [SerializeField] Collider[] ropeSupports = Array.Empty<Collider>();
        [SerializeField] Renderer[] ropeVisuals = Array.Empty<Renderer>();
        [SerializeField] Material dissolveBurn;
        [Serializable]
        struct BurnRun
        {
            public ParticleSystem embers;
            public Light glow;
            public ParticleSystem nearContact, farContact, formingFilaments, closingSeam;
            public Vector3 start, end;
        }
        [SerializeField] BurnRun[] burnRuns = Array.Empty<BurnRun>();
        Material[] ropeMaterials;
        MaterialPropertyBlock[] originalBlocks;
        float[] burnOffsets;
        MaterialPropertyBlock ropeBlock;
        bool dissolving, contactsLive, filamentsLive, seamLive, vanishCued;
        float dissolveAmount;
        [SerializeField] LineRenderer[] warningArcs = Array.Empty<LineRenderer>();
        [Min(.1f), SerializeField] float warningSeconds = 2f;
        [SerializeField] Transform button;
        [SerializeField] GameObject returnGate;
        [SerializeField] CourseControlSwitcher switcher;
        [SerializeField] HumanCannonTrampoline trampoline;
        [SerializeField] Collider dropBounds;
        [SerializeField] Collider lowerWorkerBounds;
        [SerializeField] GameObject lowerEntryGate;
        [SerializeField] Transform overflowReturnPoint;
        [Min(.1f), SerializeField] float visibleSeconds = 4f;
        [Min(.1f), SerializeField] float hiddenSeconds = 3f;
        const string RequestName = "CurtainCall.Escape.CooperativeRopes.Request";
        const string StateName = "CurtainCall.Escape.CooperativeRopes.State";
        const int RequestSize = 13, StateSize = 21;
        readonly Dictionary<ulong, uint> requests = new();
        readonly RouteResetBarrier resetBarrier = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        uint revision, sequence;
        bool synchronized;
        bool[] initialSupports, initialVisuals;
        enum RopePhase { Visible, Flash, Hidden }
        RopePhase phase;
        const float SoundRange = 8f;
        SoundHandle ropeSound;
        Vector3 ropeSoundPoint;
        ulong workerOwner = CannonSeatState.Nobody;
        uint pendingOverflow, firstOverflowRequest;
        bool initialGate, initialLowerGate;
        int resetFrame = -1;
        double nextSnapshot, nextOverflowRequest;
        public static EscapeCooperativeRopes Current { get; private set; }
        public Transform Button => button;
        public Vector3 ButtonPoint => EscapeSwitchInteraction.Center(button);
        public HumanCannonTrampoline Trampoline => trampoline;
        public bool ReturnOpen { get; private set; }
        public bool IsReady => isActiveAndEnabled && synchronized && !resetBarrier.Waiting
            && session != null && session.GameState == GameSessionState.Playing;
        public bool RopesPresent => phase != RopePhase.Hidden;
        public bool WarningLit => phase == RopePhase.Flash;
        public ulong WorkerOwner => workerOwner;
        CharacterController passBody;
        readonly HashSet<Collider> ignoredFromBelow = new();
        CameraViewDirector sectionCamera;

        public bool IsTimedSupport(Collider surface) => Array.IndexOf(ropeSupports, surface) >= 0;

        void UpdateTraversal(NetworkPlayer player)
        {
            var body = player != null && player.State == PlayerState.Normal && player.TryGetComponent(out PlayerMover mover)
                ? mover.Body : null;
            UpdatePassThrough(body);
            // Keep one orbit preset through the stairs, bounce and rope-control handoff.
            var camera = body != null && lowerWorkerBounds != null && dropBounds != null ? CameraViewDirector.Current : null;
            if (camera != null)
            {
                var zone = lowerWorkerBounds.bounds;
                zone.Encapsulate(dropBounds.bounds);
                zone.Expand(new Vector3(6, 14, 4));
                if (!zone.Contains(body.transform.position) || camera.LocalPlayer != player
                    || TightropeCourse.Current?.UsesRopeRules(player) == true
                    || player.TryGetComponent(out HumanCannonPilot pilot) && pilot.IsHoldingPole) camera = null;
            }
            if (sectionCamera == camera) return;
            if (sectionCamera != null) sectionCamera.ReleaseView(this);
            sectionCamera = camera;
            if (camera != null && camera.Settings != null)
                camera.RequestView(this, camera.Settings.DefaultThirdPerson, 210);
        }

        void UpdatePassThrough(CharacterController body)
        {
            if (passBody != body)
            {
                if (passBody != null)
                    foreach (var support in ignoredFromBelow)
                        if (support != null) Physics.IgnoreCollision(passBody, support, false);
                ignoredFromBelow.Clear();
                passBody = body;
            }
            if (body == null || !body.enabled) return;
            var capsule = body.bounds;
            foreach (var support in ropeSupports)
            {
                if (support == null || !support.enabled || !support.gameObject.activeInHierarchy) continue;
                var bounds = support.bounds;
                float releaseHeight = body.velocity.y > 0f || ignoredFromBelow.Contains(support) ? bounds.max.y : bounds.min.y;
                bool below = capsule.min.y < releaseHeight - .025f && capsule.max.y > bounds.min.y - 2f
                    && capsule.min.x < bounds.max.x + .5f && capsule.max.x > bounds.min.x - .5f
                    && capsule.min.z < bounds.max.z + .5f && capsule.max.z > bounds.min.z - .5f;
                if (below && !Physics.GetIgnoreCollision(body, support))
                {
                    Physics.IgnoreCollision(body, support);
                    ignoredFromBelow.Add(support);
                }
                else if (!below && ignoredFromBelow.Remove(support))
                    Physics.IgnoreCollision(body, support, false);
            }
        }

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            initialSupports = new bool[ropeSupports.Length];
            for (int i = 0; i < ropeSupports.Length; i++) initialSupports[i] = ropeSupports[i] != null && ropeSupports[i].enabled;
            initialVisuals = new bool[ropeVisuals.Length];
            ropeMaterials = new Material[ropeVisuals.Length];
            ropeBlock = new MaterialPropertyBlock();
            originalBlocks = new MaterialPropertyBlock[ropeVisuals.Length];
            burnOffsets = new float[ropeVisuals.Length];
            for (int i = 0; i < ropeVisuals.Length; i++)
            {
                var visual = ropeVisuals[i];
                if (!visual) continue;
                ropeMaterials[i] = visual.sharedMaterial;
                var block = new MaterialPropertyBlock();
                visual.GetPropertyBlock(block);
                if (!block.isEmpty) originalBlocks[i] = block;
                if (burnRuns.Length == 0) continue;
                var span = burnRuns[0].end - burnRuns[0].start;
                burnOffsets[i] = Mathf.Clamp01(Vector3.Dot(visual.bounds.center - burnRuns[0].start, span)
                    / Mathf.Max(.001f, span.sqrMagnitude));
            }
            for (int i = 0; i < ropeVisuals.Length; i++) initialVisuals[i] = ropeVisuals[i] != null && ropeVisuals[i].enabled;
            phase = RopePhase.Visible;
            SetWarning(false);
            initialGate = returnGate != null && returnGate.activeSelf;
            initialLowerGate = lowerEntryGate != null && lowerEntryGate.activeSelf;
            NetworkPlayer.Restarted += Restart;
            SetGate(false); SetWorker(CannonSeatState.Nobody);
        }

        void Update()
        {
            var listener = NetworkPlayer.Local;
            if (ropeSound.IsPlaying && (!listener
                || (listener.transform.position + Vector3.up * 1.2f - ropeSoundPoint).sqrMagnitude > SoundRange * SoundRange
                || SoundBlocked(ropeSupports, listener.transform.position + Vector3.up * 1.2f, ropeSoundPoint, listener.transform)))
                ropeSound.Stop();
            Attach();
            var hazards = HumanCannonHazardCourse.Current;
            bool timed = synchronized && !resetBarrier.Waiting && hazards != null && hazards.IsSynchronized;
            double cycle = timed ? hazards.Elapsed % (visibleSeconds + hiddenSeconds) : 0;
            float burn = cycle < visibleSeconds ? Mathf.InverseLerp(visibleSeconds - DissolveWindow, visibleSeconds, (float)cycle) : 0;
            // 소실음은 눈에 보이는 소실 연출과 같은 시각에 시작한다. 같은 프레임의 경고 점멸음이 이를 끊지 않도록 먼저 처리한다.
            UpdateVanishCue(timed && dissolveBurn && burn > 0);
            PresentPhase(timed ? PhaseAt(hazards.Elapsed) : RopePhase.Visible, timed);
            UpdateDissolve(burn);
            UpdateTransitionParticles(timed ? hazards.Elapsed : -1, cycle);
            UpdateTraversal(NetworkPlayer.Local);
            if (session != null && session.IsHost && synchronized && !resetBarrier.Waiting
                && resetFrame != Time.frameCount) RefreshWorker();
            ReturnOverflow();
            if (session != null && session.IsHost && messaging != null && Time.unscaledTimeAsDouble >= nextSnapshot)
            { nextSnapshot = Time.unscaledTimeAsDouble + .5; Publish(); }
            var player = NetworkPlayer.Local;
            if (InputGate.IsBlocked || !CanUseButton(player) || !player.TryGetComponent(out PlayerInputReader reader)
                || !reader.isActiveAndEnabled || !reader.Current.InteractTapped || !CommonInteractionSelection.Consume(player, this)) return;
            RequestOpen();
            // 한 번의 F가 같은 프레임의 목마/다른 기물 조작으로도 소비되지 않게 한다.
            reader.ClearPendingInput();
        }

        public bool CanUseButton(NetworkPlayer player)
        {
            if (!isActiveAndEnabled || !synchronized || resetBarrier.Waiting || ReturnOpen || button == null
                || !button.gameObject.activeInHierarchy || player == null || !player.IsSpawned
                || player.State != PlayerState.Normal || session == null
                || session.GameState != GameSessionState.Playing) return false;
            return EscapeCampaignContent.CanUseRouteDevice(gameObject.scene, player.OwnerClientId)
                && EscapeSwitchInteraction.InRange(player, button);
        }

        RopePhase PhaseAt(double elapsed)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0d) return RopePhase.Visible;
            double visible = Math.Max(.1d, visibleSeconds);
            double t = elapsed % (visible + Math.Max(.1d, hiddenSeconds));
            if (t >= visible) return RopePhase.Hidden;
            double warning = Math.Clamp(warningSeconds, .1d, visible);
            double progress = (t - (visible - warning)) / warning;
            if (progress < 0d) return RopePhase.Visible;
            // Three pulses in the last part of the visible window. Collision stays on throughout.
            return (int)(progress * 6d) % 2 == 0 ? RopePhase.Flash : RopePhase.Visible;
        }

        float DissolveWindow => Mathf.Min(RopeDissolve.WindowSeconds, warningSeconds);

        void UpdateVanishCue(bool burning)
        {
            // ESC-012의 큰 방전 구간(약 0.15~1.0초)이 소실 연출(1.05초) 동안 겹치고, 줄이 다 사라질 때 잦아든다.
            if (burning && !vanishCued && IsReady && Application.isPlaying) PlayRopeCue(SoundCueIds.EscapeRopeDisappear);
            vanishCued = burning;
        }

        /// <summary>
        /// 화면의 외줄 조각이 이 지점에서 이미 다 타 없어졌는지. 지지면은 소실 연출 끝에 한꺼번에 꺼지지만,
        /// 매달린 사람은 자기 손 위 조각이 보이지 않게 되는 순간 놓아야 한다. 덮는 조각이 없으면 false.
        /// </summary>
        public bool IsRopeGoneAt(Vector3 point)
        {
            if (!isActiveAndEnabled || !dissolving || ropeMaterials == null) return false;
            bool covered = false;
            for (int i = 0; i < ropeVisuals.Length; i++)
            {
                var visual = ropeVisuals[i];
                if (!visual || !initialVisuals[i]) continue;
                var bounds = visual.bounds;
                bounds.Expand(GripReach);
                if (!bounds.Contains(point)) continue;
                covered = true;
                if (RopeDissolve.AtOffset(dissolveAmount, burnOffsets[i]) < 1f) return false;
            }
            return covered;
        }

        const float GripReach = .5f;

        void UpdateDissolve(float amount)
        {
            dissolveAmount = amount;
            if (!dissolveBurn || (amount <= 0 && !dissolving)) return;
            bool active = amount > 0;
            if (active && !dissolving) SetWarning(false);
            for (int i = 0; i < ropeVisuals.Length; i++)
            {
                var visual = ropeVisuals[i];
                if (!visual) continue;
                if (!active)
                {
                    visual.sharedMaterial = ropeMaterials[i];
                    visual.SetPropertyBlock(originalBlocks[i]);
                    continue;
                }
                float burn = RopeDissolve.AtOffset(amount, burnOffsets[i]);
                if (burn <= 0f)
                {
                    if (visual.sharedMaterial == dissolveBurn)
                    {
                        visual.sharedMaterial = ropeMaterials[i];
                        visual.SetPropertyBlock(originalBlocks[i]);
                    }
                    continue;
                }
                if (visual.sharedMaterial != dissolveBurn) visual.sharedMaterial = dissolveBurn;
                visual.GetPropertyBlock(ropeBlock);
                var cable = ropeMaterials[i];
                if (cable && cable.mainTexture) ropeBlock.SetTexture("_Base_Tex", cable.mainTexture);
                RopeDissolve.Apply(ropeBlock, burn);
                visual.SetPropertyBlock(ropeBlock);
            }
            foreach (var run in burnRuns)
            {
                if (!run.embers) continue;
                if (run.glow && active != dissolving) run.glow.enabled = active;
                if (active)
                {
                    run.embers.transform.position = Vector3.Lerp(run.start, run.end, amount);
                    if (!dissolving) run.embers.Play();
                }
                else run.embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            dissolving = active;
        }

        // The contact sparks hand off to the existing fuse. Cool filaments gather just before
        // support returns, then a short seam runs back along the restored cable.
        void UpdateTransitionParticles(double elapsed, double cycle)
        {
            double burnLength = Math.Min(1.05d, warningSeconds);
            double period = visibleSeconds + hiddenSeconds;
            bool contacts = elapsed >= 0 && cycle >= visibleSeconds - warningSeconds && cycle < visibleSeconds - burnLength;
            bool justReturned = elapsed >= period && cycle < .46d;
            bool filaments = elapsed >= 0 && (cycle >= period - Math.Min(.42d, hiddenSeconds) || justReturned && cycle < .14d);
            if (contacts == contactsLive && filaments == filamentsLive && justReturned == seamLive && !justReturned) return;
            foreach (var run in burnRuns)
            {
                if (contacts != contactsLive)
                {
                    SetSpray(run.nearContact, contacts);
                    SetSpray(run.farContact, contacts);
                }
                if (filaments != filamentsLive) SetSpray(run.formingFilaments, filaments);
                if (justReturned && run.closingSeam)
                    run.closingSeam.transform.position = Vector3.Lerp(run.end, run.start, (float)(cycle / .46d));
                if (justReturned != seamLive) SetSpray(run.closingSeam, justReturned);
            }
            contactsLive = contacts;
            filamentsLive = filaments;
            seamLive = justReturned;
        }

        static void SetSpray(ParticleSystem spray, bool emit)
        {
            if (!spray) return;
            if (emit) spray.Play(false);
            else spray.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        void SetWarning(bool lit)
        {
            for (int i = 0; i < warningArcs.Length; i++)
                if (warningArcs[i] != null)
                    warningArcs[i].enabled = lit && !dissolving && i < initialSupports.Length && initialSupports[i];
        }

        void SetPhase(RopePhase next) => PresentPhase(next, true);

        void PresentPhase(RopePhase next, bool sound)
        {
            if (!sound) ropeSound.Stop();
            if (phase == next) return;
            bool changeSupport = (phase == RopePhase.Hidden) != (next == RopePhase.Hidden);
            if (sound && IsReady && Application.isPlaying)
            {
                // 소실 연출이 있으면 소실음은 연출 시작(UpdateVanishCue)에 이미 났다. 연출 중 경고 점멸음으로 끊지 않는다.
                string cue = next == RopePhase.Hidden ? (dissolveBurn ? null : SoundCueIds.EscapeRopeDisappear)
                    : phase == RopePhase.Hidden ? SoundCueIds.EscapeRopeRestore
                    : next == RopePhase.Flash && !vanishCued ? SoundCueIds.EscapeRopeWarning : null;
                if (cue != null) PlayRopeCue(cue);
            }
            phase = next;
            if (session != null && session.IsHost) foreach(var support in ropeSupports) if(support!=null) CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(null,support,"VANISHING_ROPE",next.ToString(),support.transform.position,true);
            SetWarning(next == RopePhase.Flash);
            if (!changeSupport) return;
            var local = NetworkPlayer.Local;
            var mover = local != null ? local.GetComponent<PlayerMover>() : null;
            if (ApplyRopeVisibility(next != RopePhase.Hidden, mover)) switcher?.ReleaseForAuthoredDrop();
        }

        void PlayRopeCue(string cue)
        {
            ropeSound.Stop();
            if (!TryGetSoundPoint(out var point)) return;
            ropeSoundPoint = point;
            ropeSound = SoundManager.PlayAt(cue, point);
        }

        bool TryGetSoundPoint(out Vector3 point)
        {
            point = default;
            var player = NetworkPlayer.Local;
            return player && TryFindAudibleSupport(ropeSupports, player.transform.position + Vector3.up * 1.2f,
                SoundRange, player.transform, out point);
        }

        internal static bool TryFindAudibleSupport(Collider[] supports, Vector3 listener, float range,
            Transform listenerRoot, out Vector3 point)
        {
            point = default;
            float nearest = range * range;
            bool found = false;
            foreach (var support in supports)
            {
                if (!support || !support.gameObject.activeInHierarchy) continue;
                Vector3 contact;
                if (support is BoxCollider box)
                {
                    Vector3 local = box.transform.InverseTransformPoint(listener) - box.center;
                    Vector3 half = box.size * .5f;
                    contact = box.transform.TransformPoint(box.center + Vector3.Max(-half, Vector3.Min(half, local)));
                }
                else contact = support.enabled ? support.ClosestPoint(listener) : support.transform.position;
                contact += Vector3.up * .12f;
                Vector3 direction = contact - listener;
                if (direction.sqrMagnitude > nearest) continue;
                if (SoundBlocked(supports, listener, contact, listenerRoot)) continue;
                nearest = direction.sqrMagnitude;
                point = contact;
                found = true;
            }
            return found;
        }

        static bool SoundBlocked(Collider[] supports, Vector3 listener, Vector3 point, Transform listenerRoot)
        {
            Vector3 direction = point - listener;
            foreach (var hit in Physics.RaycastAll(listener, direction, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (Array.IndexOf(supports, hit.collider) >= 0 || hit.collider.GetComponentInParent<NetworkPlayer>() != null
                    || listenerRoot && hit.transform.IsChildOf(listenerRoot)) continue;
                return true;
            }
            return false;
        }

        bool ApplyRopeVisibility(bool present, PlayerMover mover)
        {
            var course = switcher != null ? switcher.GetComponent<TightropeCourse>() : null;
            bool supported = !present && course != null && course.TryGetSupportingRope(mover, out _);
            for (int i = 0; i < ropeSupports.Length; i++)
                if (ropeSupports[i] != null) ropeSupports[i].enabled = present && initialSupports[i];
            for (int i = 0; i < ropeVisuals.Length; i++)
                if (ropeVisuals[i] != null) ropeVisuals[i].enabled = present && initialVisuals[i];
            return supported && !course.TryGetSupportingRope(mover, out _);
        }

        void SetGate(bool open)
        {
            ReturnOpen = open;
            if (returnGate != null) returnGate.SetActive(!open);
            if (open) SetWorker(CannonSeatState.Nobody);
        }

        void SetWorker(ulong owner)
        {
            workerOwner = ReturnOpen ? CannonSeatState.Nobody : owner;
            if (lowerEntryGate != null) lowerEntryGate.SetActive(workerOwner != CannonSeatState.Nobody);
        }

        bool IsInsideLower(NetworkPlayer player) => lowerWorkerBounds != null && lowerWorkerBounds.enabled
            && lowerWorkerBounds.gameObject.activeInHierarchy && player != null && player.IsSpawned
            && player.State == PlayerState.Normal && lowerWorkerBounds.bounds.Contains(player.transform.position)
            && EscapeCampaignContent.CanUseRouteDevice(gameObject.scene, player.OwnerClientId);

        readonly RaycastHit[] lowerFloorHits = new RaycastHit[16];
        bool IsStandingInLower(NetworkPlayer player)
        {
            if (!IsInsideLower(player) || player.IsAirborne
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(player.OwnerClientId)) return false;
            int count = Physics.RaycastNonAlloc(player.transform.position + Vector3.up * .12f,
                Vector3.down, lowerFloorHits, .3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (lowerFloorHits[i].normal.y > .5f && lowerFloorHits[i].collider.gameObject.scene == gameObject.scene
                    && lowerFloorHits[i].collider.GetComponentInParent<NetworkPlayer>() == null
                    && !CurtainCall.Damage.DismemberedLimb.Contains(lowerFloorHits[i].collider)) return true;
            return false;
        }

        void RefreshWorker()
        {
            ulong next = CannonSeatState.Nobody;
            if (!ReturnOpen)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (!IsInsideLower(player)) continue;
                    if (player.OwnerClientId == workerOwner) { next = workerOwner; break; }
                    if (!IsStandingInLower(player)) continue;
                    if (next == CannonSeatState.Nobody || player.OwnerClientId < next) next = player.OwnerClientId;
                }
            }
            if (next == workerOwner) return;
            SetWorker(next); revision++; Publish();
        }

        bool CanReturnOverflow(NetworkPlayer player) => synchronized && !resetBarrier.Waiting && !ReturnOpen
            && overflowReturnPoint != null && workerOwner != CannonSeatState.Nobody
            && player != null && player.IsLocal && player.OwnerClientId != workerOwner && IsStandingInLower(player);

        void ReturnOverflow()
        {
            var local = NetworkPlayer.Local;
            if (!CanReturnOverflow(local) || messaging == null)
            { pendingOverflow = firstOverflowRequest = 0; return; }
            if (Time.unscaledTimeAsDouble < nextOverflowRequest) return;
            nextOverflowRequest = Time.unscaledTimeAsDouble + .25;
            pendingOverflow = ++sequence;
            if (firstOverflowRequest == 0) firstOverflowRequest = pendingOverflow;
            if (session.IsHost)
            {
                if (AuthorizeOverflow(local.OwnerClientId, revision, pendingOverflow)) ReturnLocalOverflow(local);
            }
            else SendRequest(2, pendingOverflow, 0);
        }

        bool AuthorizeOverflow(ulong actor, uint expected, uint request)
        {
            if (session == null || !session.IsHost || expected != revision || ReturnOpen
                || workerOwner == CannonSeatState.Nobody || actor == workerOwner || request == 0
                || requests.TryGetValue(actor, out uint previous) && request <= previous) return false;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All) if (candidate.OwnerClientId == actor) { player = candidate; break; }
            if (!IsStandingInLower(player)) return false;
            requests[actor] = request;
            // A local-only pilot reset leaves the authoritative flight occupied. Reuse the
            // original host release message before acknowledging this player's safe return.
            HumanCannonSystem.Current?.ServerReleaseForInspection(actor);
            return true;
        }

        void ReturnLocalOverflow(NetworkPlayer local)
        {
            if (!CanReturnOverflow(local) || !local.TryGetComponent(out PlayerMover mover)) return;
            local.GetComponent<HumanCannonPilot>()?.ClearMotionForInspection();
            switcher?.ReleaseForAuthoredDrop();
            mover.Relocate(overflowReturnPoint.position, overflowReturnPoint.rotation);
            mover.ResetExternalMotion(false);
            pendingOverflow = firstOverflowRequest = 0;
        }

        void Attach()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                && (next.ConnectionState == SessionConnectionState.Host || next.ConnectionState == SessionConnectionState.Client)
                ? next.NetworkManager.CustomMessagingManager : null;
            if (messaging == manager) return;
            Detach(); session = next; messaging = manager;
            if (messaging == null) return;
            if (session.IsHost)
            {
                synchronized = true;
                messaging.RegisterNamedMessageHandler(RequestName, ReadRequest);
            }
            else
            {
                messaging.RegisterNamedMessageHandler(StateName, ReadState);
                SendRequest(0, 0, 0);
            }
        }

        void RequestOpen()
        {
            var player = NetworkPlayer.Local;
            if (!CanUseButton(player) || messaging == null) return;
            uint request = ++sequence;
            if (session.IsHost) OpenByHost(player.OwnerClientId, revision, request);
            else SendRequest(1, request, 0);
        }

        void SendRequest(byte action, uint request, int token)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
            writer.WriteValueSafe(action); writer.WriteValueSafe(revision); writer.WriteValueSafe(request); writer.WriteValueSafe(token);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out uint expected);
            reader.ReadValueSafe(out uint request); reader.ReadValueSafe(out int token);
            if (action == 0 && token >= 0) SendState(sender, token);
            else if (action == 1) { OpenByHost(sender, expected, request); SendState(sender, 0); }
            else if (action == 2)
                SendState(sender, 0, AuthorizeOverflow(sender, expected, request) ? request : 0);
        }

        void OpenByHost(ulong actor, uint expected, uint request)
        {
            if (!session.IsHost || expected != revision || request == 0
                || requests.TryGetValue(actor, out uint previous) && request <= previous) return;
            requests[actor] = request;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All) if (candidate.OwnerClientId == actor) { player = candidate; break; }
            if (!CanUseButton(player)) return;
            CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(player,button,"RETURN_LEVER","ACTIVATED",ButtonPoint,true);
            SetGate(true); revision++; Publish();
        }

        void Publish()
        {
            if (messaging == null || session == null || !session.IsHost) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, 0);
        }

        void SendState(ulong client, int token, uint overflowAck = 0)
        {
            using var writer = new FastBufferWriter(StateSize, Allocator.Temp);
            writer.WriteValueSafe(revision); writer.WriteValueSafe(ReturnOpen); writer.WriteValueSafe(workerOwner);
            writer.WriteValueSafe(token); writer.WriteValueSafe(overflowAck);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId
                || reader.Length - reader.Position != StateSize) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out bool open); reader.ReadValueSafe(out ulong owner);
            reader.ReadValueSafe(out int token);
            reader.ReadValueSafe(out uint overflowAck);
            ApplyState(incoming, open, owner, token, overflowAck);
        }

        bool ApplyState(uint incoming, bool open, ulong owner, int token, uint overflowAck)
        {
            if (incoming < revision || token < 0
                || synchronized && incoming == revision && (open != ReturnOpen || owner != workerOwner)
                || !resetBarrier.Accept(token)) return false;
            revision = incoming; synchronized = true; SetGate(open); SetWorker(owner);
            if (firstOverflowRequest != 0 && overflowAck >= firstOverflowRequest && overflowAck <= pendingOverflow)
                ReturnLocalOverflow(NetworkPlayer.Local);
            return true;
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null || messaging == null) return;
            if (session.IsHost)
            {
                if (resetFrame == Time.frameCount) return;
                resetFrame = Time.frameCount;
                SetGate(false); SetWorker(CannonSeatState.Nobody); PresentPhase(RopePhase.Visible, false);
                pendingOverflow = firstOverflowRequest = 0; nextOverflowRequest = 0; revision++; Publish();
            }
            else if (player.IsLocal)
            {
                synchronized = false; SetGate(false); SetWorker(CannonSeatState.Nobody); PresentPhase(RopePhase.Visible, false);
                pendingOverflow = firstOverflowRequest = 0; nextOverflowRequest = 0;
                SendRequest(0, 0, resetBarrier.Begin());
            }
        }

        void Detach()
        {
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(RequestName);
                messaging.UnregisterNamedMessageHandler(StateName);
            }
            messaging = null; session = null; synchronized = false;
            revision = sequence = 0; requests.Clear(); resetBarrier.Clear();
            pendingOverflow = firstOverflowRequest = 0; nextOverflowRequest = 0;
            SetGate(false); SetWorker(CannonSeatState.Nobody); PresentPhase(RopePhase.Visible, false);
        }

        void OnDisable()
        {
            ropeSound.Stop();
            if (Current != this) return;
            UpdateDissolve(0);
            UpdateTransitionParticles(-1, 0);
            UpdateTraversal(null);
            NetworkPlayer.Restarted -= Restart; Detach();
            if (returnGate != null) returnGate.SetActive(initialGate);
            if (lowerEntryGate != null) lowerEntryGate.SetActive(initialLowerGate);
            Current = null;
        }
    }
}
