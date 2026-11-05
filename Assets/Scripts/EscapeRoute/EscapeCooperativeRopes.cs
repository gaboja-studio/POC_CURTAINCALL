using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
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
        [Tooltip("버튼 중심의 구형 상호작용 반경(m). 모든 방향에 같은 거리를 사용한다."), Min(.1f), SerializeField]
        float buttonDistance = EscapeSwitchInteraction.DefaultRadius;
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

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            initialSupports = new bool[ropeSupports.Length];
            for (int i = 0; i < ropeSupports.Length; i++) initialSupports[i] = ropeSupports[i] != null && ropeSupports[i].enabled;
            initialVisuals = new bool[ropeVisuals.Length];
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
            Attach();
            var hazards = HumanCannonHazardCourse.Current;
            bool timed = synchronized && !resetBarrier.Waiting && hazards != null && hazards.IsSynchronized;
            SetPhase(timed ? PhaseAt(hazards.Elapsed) : RopePhase.Visible);
            if (session != null && session.IsHost && synchronized && !resetBarrier.Waiting
                && resetFrame != Time.frameCount) RefreshWorker();
            ReturnOverflow();
            if (session != null && session.IsHost && messaging != null && Time.unscaledTimeAsDouble >= nextSnapshot)
            { nextSnapshot = Time.unscaledTimeAsDouble + .5; Publish(); }
            var player = NetworkPlayer.Local;
            if (InputGate.IsBlocked || !CanUseButton(player) || !player.TryGetComponent(out PlayerInputReader reader)
                || !reader.isActiveAndEnabled || !reader.Current.InteractTapped) return;
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
            return EscapeSwitchInteraction.InRange(player, button, buttonDistance);
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

        void SetWarning(bool lit)
        {
            for (int i = 0; i < warningArcs.Length; i++)
                if (warningArcs[i] != null)
                    warningArcs[i].enabled = lit && i < initialSupports.Length && initialSupports[i];
        }

        void SetPhase(RopePhase next)
        {
            if (phase == next) return;
            bool changeSupport = (phase == RopePhase.Hidden) != (next == RopePhase.Hidden);
            phase = next;
            if (session != null && session.IsHost) foreach(var support in ropeSupports) if(support!=null) CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(null,support,"VANISHING_ROPE",next.ToString(),support.transform.position,true);
            SetWarning(next == RopePhase.Flash);
            if (!changeSupport) return;
            var local = NetworkPlayer.Local;
            var mover = local != null ? local.GetComponent<PlayerMover>() : null;
            if (ApplyRopeVisibility(next != RopePhase.Hidden, mover)) switcher?.ReleaseForAuthoredDrop();
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
            && player.State == PlayerState.Normal && lowerWorkerBounds.bounds.Contains(player.transform.position);

        readonly RaycastHit[] lowerFloorHits = new RaycastHit[16];
        bool IsStandingInLower(NetworkPlayer player)
        {
            if (!IsInsideLower(player) || player.IsAirborne
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsFlying(player.OwnerClientId)) return false;
            int count = Physics.RaycastNonAlloc(player.transform.position + Vector3.up * .12f,
                Vector3.down, lowerFloorHits, .3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (lowerFloorHits[i].normal.y > .5f
                    && lowerFloorHits[i].collider.GetComponentInParent<NetworkPlayer>() == null) return true;
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
                SetGate(false); SetWorker(CannonSeatState.Nobody); SetPhase(RopePhase.Visible);
                pendingOverflow = firstOverflowRequest = 0; nextOverflowRequest = 0; revision++; Publish();
            }
            else if (player.IsLocal)
            {
                synchronized = false; SetGate(false); SetWorker(CannonSeatState.Nobody); SetPhase(RopePhase.Visible);
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
            SetGate(false); SetWorker(CannonSeatState.Nobody); SetPhase(RopePhase.Visible);
        }

        void OnDisable()
        {
            if (Current != this) return;
            NetworkPlayer.Restarted -= Restart; Detach();
            if (returnGate != null) returnGate.SetActive(initialGate);
            if (lowerEntryGate != null) lowerEntryGate.SetActive(initialLowerGate);
            Current = null;
        }
    }
}
