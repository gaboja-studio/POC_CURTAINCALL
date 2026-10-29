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
    /// <summary>원본 외줄 네 줄의 주기 소실과 상부 복귀 버튼. 이동·반동은 공용 플레이어/트램폴린이 맡는다.</summary>
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
        [Min(.1f), SerializeField] float visibleSeconds = 4f;
        [Min(.1f), SerializeField] float hiddenSeconds = 3f;
        [Min(.1f), SerializeField] float buttonDistance = 2.5f;
        const string RequestName = "CurtainCall.Escape.CooperativeRopes.Request";
        const string StateName = "CurtainCall.Escape.CooperativeRopes.State";
        const int RequestSize = 13, StateSize = 9;
        readonly Dictionary<ulong, uint> requests = new();
        readonly RouteResetBarrier resetBarrier = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        uint revision, sequence;
        bool synchronized, ropesPresent = true;
        bool[] initialSupports, initialVisuals;
        bool initialGate;
        int resetFrame = -1;
        double nextSnapshot;
        public static EscapeCooperativeRopes Current { get; private set; }
        public Transform Button => button;
        public HumanCannonTrampoline Trampoline => trampoline;
        public bool ReturnOpen { get; private set; }
        public bool RopesPresent => ropesPresent;

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            initialSupports = new bool[ropeSupports.Length];
            for (int i = 0; i < ropeSupports.Length; i++) initialSupports[i] = ropeSupports[i] != null && ropeSupports[i].enabled;
            initialVisuals = new bool[ropeVisuals.Length];
            for (int i = 0; i < ropeVisuals.Length; i++) initialVisuals[i] = ropeVisuals[i] != null && ropeVisuals[i].enabled;
            initialGate = returnGate != null && returnGate.activeSelf;
            NetworkPlayer.Restarted += Restart;
            SetGate(false);
        }

        void Update()
        {
            Attach();
            var hazards = HumanCannonHazardCourse.Current;
            bool timed = synchronized && !resetBarrier.Waiting && hazards != null && hazards.IsSynchronized;
            double on = Math.Max(.1, visibleSeconds), period = on + Math.Max(.1, hiddenSeconds);
            SetRopes(!timed || hazards.Elapsed % period < on);
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
                || player.State != PlayerState.Normal || player.IsAirborne || session == null
                || session.GameState != GameSessionState.Playing) return false;
            var cannon = HumanCannonSystem.Current;
            if (cannon != null && (cannon.IsSeated(player.OwnerClientId) || cannon.IsFlying(player.OwnerClientId))) return false;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            return PlayerInteraction.InRange(player, button.position, buttonDistance * reach);
        }

        void SetRopes(bool present)
        {
            if (ropesPresent == present) return;
            if (!present)
            {
                var local = NetworkPlayer.Local;
                if (local != null && dropBounds != null && dropBounds.bounds.Contains(local.transform.position))
                    switcher?.ReleaseForAuthoredDrop();
            }
            ropesPresent = present;
            for (int i = 0; i < ropeSupports.Length; i++) if (ropeSupports[i] != null) ropeSupports[i].enabled = present && initialSupports[i];
            for (int i = 0; i < ropeVisuals.Length; i++) if (ropeVisuals[i] != null) ropeVisuals[i].enabled = present && initialVisuals[i];
        }

        void SetGate(bool open)
        {
            ReturnOpen = open;
            if (returnGate != null) returnGate.SetActive(!open);
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

        void SendState(ulong client, int token)
        {
            using var writer = new FastBufferWriter(StateSize, Allocator.Temp);
            writer.WriteValueSafe(revision); writer.WriteValueSafe(ReturnOpen); writer.WriteValueSafe(token);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId
                || reader.Length - reader.Position != StateSize) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out bool open); reader.ReadValueSafe(out int token);
            if (incoming < revision || !resetBarrier.Accept(token)) return;
            revision = incoming; synchronized = true; SetGate(open);
        }

        void Restart(NetworkPlayer player)
        {
            if (player == null || messaging == null) return;
            if (session.IsHost)
            {
                if (resetFrame == Time.frameCount) return;
                resetFrame = Time.frameCount;
                SetGate(false); SetRopes(true); revision++; Publish();
            }
            else if (player.IsLocal)
            {
                synchronized = false; SetGate(false); SetRopes(true);
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
            SetGate(false); SetRopes(true);
        }

        void OnDisable()
        {
            if (Current != this) return;
            NetworkPlayer.Restarted -= Restart; Detach();
            if (returnGate != null) returnGate.SetActive(initialGate);
            Current = null;
        }
    }
}
