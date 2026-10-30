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
    /// <summary>외줄 한 줄씩의 공유 소실, 하부 작업자 한 명과 상부 복귀 버튼. 이동·반동은 원본 기물을 사용한다.</summary>
    [DefaultExecutionOrder(-70)] // 줄타기/균형 판정 전에 지지 소실과 조작 반환을 적용한다.
    public sealed class EscapeCooperativeRopes : MonoBehaviour
    {
        [SerializeField] Collider[] ropeSupports = Array.Empty<Collider>();
        [SerializeField] Renderer[] ropeVisuals = Array.Empty<Renderer>();
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
        int[] visualSupports, timedSupports;
        int hiddenRope = -1, selectedRope = -1;
        long selectedCycle = -1;
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
        public bool RopesPresent => hiddenRope < 0;
        public int HiddenRopeIndex => hiddenRope;
        public ulong WorkerOwner => workerOwner;

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            initialSupports = new bool[ropeSupports.Length];
            for (int i = 0; i < ropeSupports.Length; i++) initialSupports[i] = ropeSupports[i] != null && ropeSupports[i].enabled;
            initialVisuals = new bool[ropeVisuals.Length];
            for (int i = 0; i < ropeVisuals.Length; i++) initialVisuals[i] = ropeVisuals[i] != null && ropeVisuals[i].enabled;
            var active = new List<int>();
            for (int i = 0; i < initialSupports.Length; i++) if (initialSupports[i]) active.Add(i);
            timedSupports = active.ToArray();
            MapRopeVisuals();
            hiddenRope = selectedRope = -1; selectedCycle = -1;
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
            SetHiddenRope(timed ? HiddenRopeAt(hazards.Elapsed) : -1);
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

        int HiddenRopeAt(double elapsed)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0 || timedSupports == null || timedSupports.Length == 0) return -1;
            double on = Math.Max(.1, visibleSeconds), period = on + Math.Max(.1, hiddenSeconds);
            double cycle = Math.Floor(elapsed / period);
            if (!double.IsFinite(cycle) || cycle >= long.MaxValue || elapsed % period < on) return -1;
            if (selectedCycle != (long)cycle)
            {
                selectedCycle = (long)cycle;
                selectedRope = timedSupports[SelectRope(selectedCycle, timedSupports.Length)];
            }
            return selectedRope;
        }

        // Each block uses every rope once. Swapping only the first two entries preserves the
        // previous block's last entry, so late joiners need no history to avoid boundary repeats.
        static int SelectRope(long cycle, int count)
        {
            if (count <= 1) return 0;
            if (count == 2) return (int)(cycle & 1L);
            long block = cycle / count;
            var order = RopeOrder(block, count);
            if (block > 0 && order[0] == RopeOrder(block - 1, count)[count - 1])
                (order[0], order[1]) = (order[1], order[0]);
            return order[(int)(cycle % count)];
        }

        static int[] RopeOrder(long block, int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            ulong state = unchecked((ulong)block + 0x9E3779B97F4A7C15UL);
            for (int i = count - 1; i > 0; i--)
            {
                state = unchecked(state + 0x9E3779B97F4A7C15UL);
                ulong value = state;
                value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
                value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
                int other = (int)((value ^ (value >> 31)) % (ulong)(i + 1));
                (order[i], order[other]) = (order[other], order[i]);
            }
            return order;
        }

        void MapRopeVisuals()
        {
            visualSupports = new int[ropeVisuals.Length];
            for (int i = 0; i < ropeVisuals.Length; i++)
            {
                visualSupports[i] = -1;
                var visual = ropeVisuals[i];
                if (visual == null) continue;
                float nearest = float.PositiveInfinity;
                for (int rope = 0; rope < ropeSupports.Length; rope++)
                {
                    var support = ropeSupports[rope];
                    if (support == null) continue;
                    if (visual.transform.IsChildOf(support.transform)
                        || visual.transform.parent == support.transform.parent
                        && visual.name.StartsWith(support.name + " ", StringComparison.Ordinal))
                    { visualSupports[i] = rope; break; }
                    // CompletionRope's sockets are siblings with a common name, not children.
                    float distance = VisualDistance(support, visual.bounds.center);
                    if (distance >= nearest) continue;
                    nearest = distance; visualSupports[i] = rope;
                }
            }
        }

        static float VisualDistance(Collider support, Vector3 point)
        {
            // Disabled BoxColliders still own their sockets; Collider.ClosestPoint cannot
            // locate them after a support has been authored disabled.
            if (support is BoxCollider box)
            {
                var local = box.transform.InverseTransformPoint(point) - box.center;
                var half = box.size * .5f;
                local = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y),
                    Mathf.Clamp(local.z, -half.z, half.z));
                return (box.transform.TransformPoint(box.center + local) - point).sqrMagnitude;
            }
            return support.enabled && support.gameObject.activeInHierarchy
                ? (support.ClosestPoint(point) - point).sqrMagnitude : float.PositiveInfinity;
        }

        void SetHiddenRope(int index)
        {
            if (hiddenRope == index) return;
            var local = NetworkPlayer.Local;
            var mover = local != null ? local.GetComponent<PlayerMover>() : null;
            if (ApplyRopeVisibility(index, mover)) switcher?.ReleaseForAuthoredDrop();
        }

        bool ApplyRopeVisibility(int index, PlayerMover mover)
        {
            var course = switcher != null ? switcher.GetComponent<TightropeCourse>() : null;
            bool supported = index >= 0 && course != null && course.TryGetSupportingRope(mover, out _);
            hiddenRope = index;
            for (int i = 0; i < ropeSupports.Length; i++)
                if (ropeSupports[i] != null) ropeSupports[i].enabled = i != index && initialSupports[i];
            for (int i = 0; i < ropeVisuals.Length; i++)
                if (ropeVisuals[i] != null) ropeVisuals[i].enabled = (index < 0 || visualSupports[i] != index) && initialVisuals[i];
            // Query the actual support before and after removal. Someone on a different live
            // rope (or an overlapping platform) keeps their controls, even inside dropBounds.
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
            if (incoming < revision || !resetBarrier.Accept(token)) return false;
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
                SetGate(false); SetWorker(CannonSeatState.Nobody); SetHiddenRope(-1);
                pendingOverflow = firstOverflowRequest = 0; nextOverflowRequest = 0; revision++; Publish();
            }
            else if (player.IsLocal)
            {
                synchronized = false; SetGate(false); SetWorker(CannonSeatState.Nobody); SetHiddenRope(-1);
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
            SetGate(false); SetWorker(CannonSeatState.Nobody); SetHiddenRope(-1);
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
