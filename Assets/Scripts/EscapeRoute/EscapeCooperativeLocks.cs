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
    /// <summary>버튼은 독립적으로 작동하며 봉 점유는 연결 장치를 가동한다. 이동은 원본 시스템이 맡는다.</summary>
    [DefaultExecutionOrder(-75)]
    public sealed class EscapeCooperativeLocks : MonoBehaviour
    {
        [Serializable]
        public sealed class Checkpoint
        {
            public Transform shoulderControl;
            public HumanCannonPole helperPole;
            public HumanCannonPole returnHelperPole;
            public BoxCollider arrival;
            public GameObject[] shutters = Array.Empty<GameObject>();
            public Renderer indicator;
            public Renderer[] linkedIndicators = Array.Empty<Renderer>();
            public HumanCannonStation lockedStation;
            public EscapeCooperativeSpan span;
            [Min(1)] public int minimumHelpers = 1;
            public bool latchWhenCrewReady;
            public string prompt = "전원 버튼 누르기";
            public string instruction = "가까이에서 F를 눌러 연결 장치를 켭니다";
            public string sourceName = "연결 장치";
            public string effectName = "통로";
        }

        [SerializeField] Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
        [Tooltip("버튼 중심의 구형 상호작용 반경(m). 모든 방향에 같은 거리를 사용한다."), Min(.1f), SerializeField]
        float controlDistance = EscapeSwitchInteraction.DefaultRadius;
        const string RequestName = "CurtainCall.Escape.Locks.Request";
        const string StateName = "CurtainCall.Escape.Locks.State";
        const int RequestSize = 17, StateSize = 20;
        readonly RouteResetBarrier resetBarrier = new();
        readonly Dictionary<ulong, uint> requests = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        uint armed, latched, held, revision, sequence;
        bool synchronized;
        int resetFrame = -1;
        double nextSnapshot, nextSync;
        MaterialPropertyBlock tint;
        public static EscapeCooperativeLocks Current { get; private set; }
        public int Count => checkpoints.Length;
        public bool IsReady => Ready;
        public Transform Control(int index) => Valid(index) ? checkpoints[index].shoulderControl : null;
        public Vector3 ControlPoint(int index) => EscapeSwitchInteraction.Center(Control(index));
        public string Prompt(int index) => Valid(index) ? checkpoints[index].prompt : "";
        public string Instruction(int index) => Valid(index) ? checkpoints[index].instruction : "";
        public string Status(int index) => !Valid(index) ? "" : checkpoints[index].effectName
            + (IsLatched(index) ? " · 가동 고정" : IsOpen(index) ? " · 가동 중" : " · 정지");
        public string Feedback(int index) => !Valid(index) ? "" : (IsLatched(index)
            && checkpoints[index].shoulderControl == null && !checkpoints[index].latchWhenCrewReady ? "맞은편 도착" : checkpoints[index].sourceName)
            + (IsOpen(index) ? " → " : " 해제 → ") + Status(index);
        public string StationInstruction(HumanCannonStation station)
        {
            for (int i = 0; i < checkpoints.Length; i++)
                if (checkpoints[i] != null && checkpoints[i].lockedStation == station) return Status(i) + "\n" + checkpoints[i].instruction;
            return "F로 탑승 · Space로 발사합니다";
        }
        public string HelperInstruction(HumanCannonPole pole)
        {
            for (int i = 0; i < checkpoints.Length; i++)
                if (checkpoints[i] != null && (checkpoints[i].helperPole == pole || checkpoints[i].returnHelperPole == pole)) return Status(i) + "\n" + checkpoints[i].instruction;
            return "동료가 건널 때까지 잡고 있습니다";
        }
        public bool IsHelperPole(HumanCannonPole pole) => pole != null && Array.Exists(checkpoints, c => c != null
            && (c.helperPole == pole || c.returnHelperPole == pole));
        bool Valid(int index) => index >= 0 && index < checkpoints.Length && index < 32 && checkpoints[index] != null;
        bool Ready => isActiveAndEnabled && synchronized && !resetBarrier.Waiting
            && session != null && session.GameState == GameSessionState.Playing;
        internal static bool OpenAt(uint complete, uint supported, int index) => index >= 0 && index < 32
            && ((complete | supported) & (1u << index)) != 0;
        public bool IsLatched(int index) => Valid(index) && (latched & (1u << index)) != 0;
        public bool IsArmed(int index) => Valid(index) && (armed & (1u << index)) != 0;
        public bool IsOpen(int index) => Ready && Valid(index) && OpenAt(latched, held, index);

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            NetworkPlayer.Restarted += Restart;
            Present();
        }

        void Update()
        {
            Attach();
            if (!Ready)
            {
                Present();
                if (messaging != null && session != null && !session.IsHost && Time.unscaledTimeAsDouble >= nextSync)
                { nextSync = Time.unscaledTimeAsDouble + .5; SendRequest(0, -1, 0, resetBarrier.Waiting ? resetBarrier.Token : 0); }
                return;
            }
            if (session.IsHost && resetFrame != Time.frameCount) HostStep();
            var local = NetworkPlayer.Local;
            if (!InputGate.IsBlocked && local != null && local.TryGetComponent(out PlayerInputReader input)
                && input.isActiveAndEnabled && input.Current.InteractTapped)
                for (int i = 0; i < checkpoints.Length; i++)
                    if (CanUseControl(local, i))
                    {
                        if (session.IsHost) Arm(local.OwnerClientId, i, revision, ++sequence);
                        else SendRequest(1, i, ++sequence, 0);
                        input.ClearPendingInput();
                        break;
                    }
            Present();
        }

        public bool CanUseControl(NetworkPlayer player, int index)
        {
            if (!Ready || !Valid(index) || checkpoints[index].shoulderControl == null
                || (armed & (1u << index)) != 0 || !Alive(player)) return false;
            return EscapeSwitchInteraction.InRange(player, checkpoints[index].shoulderControl, controlDistance);
        }

        static bool Alive(NetworkPlayer player) => player != null && player.IsSpawned && player.State == PlayerState.Normal;
        int HelperIndex(int index, bool returning = false)
        {
            var poles = HumanCannonPoleSystem.Current;
            if (!Valid(index) || poles == null) return -1;
            var helper = returning ? checkpoints[index].returnHelperPole : checkpoints[index].helperPole;
            if (helper == null) return -1;
            for (int i = 0; i < poles.PoleCount; i++)
                if (poles.Pole(i) == helper) return i;
            return -1;
        }

        int HelperCount(int poleIndex)
        {
            if (poleIndex < 0) return 0;
            int count = 0;
            foreach (var player in NetworkPlayer.All)
                if (Alive(player) && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) == poleIndex) count++;
            return count;
        }

        bool AtArrival(NetworkPlayer player, int index)
        {
            var zone = checkpoints[index].arrival;
            if (!Alive(player) || player.IsAirborne || zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy
                || !player.TryGetComponent(out PlayerMover mover)
                || HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0
                || HumanCannonSystem.Current != null && (HumanCannonSystem.Current.IsFlying(player.OwnerClientId)
                    || HumanCannonSystem.Current.IsSeated(player.OwnerClientId))) return false;
            Vector3 point = zone.transform.InverseTransformPoint(player.transform.position) - zone.center;
            if (Mathf.Abs(point.x) > zone.size.x * .5f || Mathf.Abs(point.y) > zone.size.y * .5f
                || Mathf.Abs(point.z) > zone.size.z * .5f) return false;
            if (TightropeCourse.Current != null && TightropeCourse.Current.TryGetSupportingRopeAt(mover, player.transform.position, out _)) return true;
            return Physics.Raycast(player.transform.position + Vector3.up * .12f, Vector3.down, .3f, ~0, QueryTriggerInteraction.Ignore);
        }

        void HostStep()
        {
            uint beforeHeld = held, beforeLatched = latched;
            held = 0;
            for (int i = 0; i < checkpoints.Length && i < 32; i++)
            {
                if (!Valid(i)) continue;
                uint bit = 1u << i;
                int poleIndex = HelperIndex(i);
                int returning = HelperIndex(i, true);
                int helpers = HelperCount(poleIndex) + (returning != poleIndex ? HelperCount(returning) : 0);
                bool crewReady = helpers >= Mathf.Max(1, checkpoints[i].minimumHelpers);
                if (crewReady) held |= bit;
                if (crewReady && checkpoints[i].latchWhenCrewReady) latched |= bit;
                if ((latched & bit) != 0 || checkpoints[i].helperPole == null && (armed & bit) == 0
                    || checkpoints[i].helperPole != null && !crewReady) continue;
                foreach (var player in NetworkPlayer.All)
                    if (AtArrival(player, i) && (poleIndex < 0 || HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) != poleIndex))
                    { latched |= bit; break; }
            }
            if (beforeHeld != held || beforeLatched != latched) { revision++; Publish(); }
            else if (Time.unscaledTimeAsDouble >= nextSnapshot)
            { nextSnapshot = Time.unscaledTimeAsDouble + .5; Publish(); }
        }

        void Present()
        {
            tint ??= new MaterialPropertyBlock();
            for (int i = 0; i < checkpoints.Length && i < 32; i++)
            {
                if (!Valid(i)) continue;
                bool open = IsOpen(i);
                if (checkpoints[i].lockedStation != null) checkpoints[i].lockedStation.InteractionAvailable = open;
                if (checkpoints[i].span != null) checkpoints[i].span.SetDeployed(open);
                foreach (var shutter in checkpoints[i].shutters)
                    if (shutter != null && shutter.activeSelf == open) shutter.SetActive(!open);
                Color color = IsLatched(i) ? Color.green : open || (armed & (1u << i)) != 0 ? new Color(1f, .65f, .1f) : Color.red;
                tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
                tint.SetColor("_EmissionColor", color * 2f);
                if (checkpoints[i].indicator != null) checkpoints[i].indicator.SetPropertyBlock(tint);
                foreach (var indicator in checkpoints[i].linkedIndicators)
                    if (indicator != null) indicator.SetPropertyBlock(tint);
            }
        }

        void Arm(ulong actor, int index, uint expected, uint request)
        {
            if (session == null || !session.IsHost || expected != revision || request == 0
                || requests.TryGetValue(actor, out uint last) && request <= last) return;
            requests[actor] = request;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.OwnerClientId == actor && CanUseControl(player, index))
                {
                    armed |= 1u << index;
                    latched |= 1u << index;
                    revision++; Publish(); break;
                }
        }

        void Attach()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                && (next.ConnectionState == SessionConnectionState.Host || next.ConnectionState == SessionConnectionState.Client)
                ? next.NetworkManager.CustomMessagingManager : null;
            if (manager == messaging) return;
            Detach(); session = next; messaging = manager;
            if (manager == null) return;
            if (session.IsHost) { synchronized = true; manager.RegisterNamedMessageHandler(RequestName, ReadRequest); }
            else { manager.RegisterNamedMessageHandler(StateName, ReadState); SendRequest(0, -1, 0, 0); }
        }
        void SendRequest(byte action, int index, uint request, int token)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
            writer.WriteValueSafe(action); writer.WriteValueSafe(index); writer.WriteValueSafe(revision);
            writer.WriteValueSafe(request); writer.WriteValueSafe(token);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out int index); reader.ReadValueSafe(out uint expected);
            reader.ReadValueSafe(out uint request); reader.ReadValueSafe(out int token);
            if (action == 0 && token >= 0) SendState(sender, token);
            else if (action == 1 && token == 0) { Arm(sender, index, expected, request); SendState(sender, 0); }
        }
        void Publish()
        {
            Present();
            if (messaging == null || session == null || !session.IsHost) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, 0);
        }
        void SendState(ulong client, int token)
        {
            using var writer = new FastBufferWriter(StateSize, Allocator.Temp);
            writer.WriteValueSafe(revision); writer.WriteValueSafe(armed); writer.WriteValueSafe(latched);
            writer.WriteValueSafe(held); writer.WriteValueSafe(token);
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || reader.Length - reader.Position != StateSize) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out uint a); reader.ReadValueSafe(out uint l);
            reader.ReadValueSafe(out uint h); reader.ReadValueSafe(out int token);
            ApplyState(incoming, a, l, h, token);
        }
        bool ApplyState(uint incoming, uint a, uint l, uint h, int token)
        {
            uint valid = checkpoints.Length >= 32 ? uint.MaxValue : (1u << checkpoints.Length) - 1u;
            if (incoming < revision || ((a | l | h) & ~valid) != 0 || !resetBarrier.Accept(token)) return false;
            revision = incoming; armed = a; latched = l; held = h; synchronized = true; Present(); return true;
        }
        void Restart(NetworkPlayer player)
        {
            if (player == null || messaging == null) return;
            if (session.IsHost)
            {
                if (resetFrame == Time.frameCount) return;
                resetFrame = Time.frameCount; armed = latched = held = 0; requests.Clear(); revision++; Publish();
            }
            else if (player.IsLocal)
            { synchronized = false; armed = latched = held = 0; Present(); SendRequest(0, -1, 0, resetBarrier.Begin()); }
        }
        void Detach()
        {
            if (messaging != null)
            { messaging.UnregisterNamedMessageHandler(RequestName); messaging.UnregisterNamedMessageHandler(StateName); }
            messaging = null; session = null; synchronized = false; armed = latched = held = revision = sequence = 0;
            requests.Clear(); resetBarrier.Clear(); Present();
        }
        void OnDisable()
        {
            if (Current != this) return;
            NetworkPlayer.Restarted -= Restart; Detach(); Current = null;
            foreach (var checkpoint in checkpoints)
                if (checkpoint != null && checkpoint.lockedStation != null) checkpoint.lockedStation.InteractionAvailable = true;
        }
    }
}
