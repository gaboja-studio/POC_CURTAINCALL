using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Tightrope;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>이 검토 씬에서만 1키 비행을 허용한다. 장치 점유 해제는 호스트, 위치 공유는 기존 소유자 NetworkTransform이 맡는다.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class EscapeInspectionSession : MonoBehaviour
    {
        const string RequestMessage = "CurtainCall.Escape.Inspection.Request";
        const string StateMessage = "CurtainCall.Escape.Inspection.State";
        readonly HashSet<ulong> inspecting = new();
        readonly Dictionary<ulong, uint> revisions = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        Func<NetworkPlayer, PlayerState, bool> previousRule;
        uint sequence;
        public bool LocalActive { get; private set; }
        public bool Pending { get; private set; }
        public event Action<bool> LocalChanged;

        void OnEnable() => NetworkPlayer.Restarted += Restarted;
        void Update()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                ? next.NetworkManager.CustomMessagingManager : null;
            if (messaging == manager) return;
            Disconnect(); session = next; messaging = manager;
            if (messaging == null) return;
            messaging.RegisterNamedMessageHandler(RequestMessage, ReadRequest);
            messaging.RegisterNamedMessageHandler(StateMessage, ReadState);
            if (session.IsHost)
            {
                previousRule = NetworkPlayer.ServerCanChangeState;
                NetworkPlayer.ServerCanChangeState = AllowState;
            }
        }

        bool AllowState(NetworkPlayer player, PlayerState next) =>
            (player == null || !inspecting.Contains(player.OwnerClientId) || next != PlayerState.Fallen && next != PlayerState.Arrived)
            && (previousRule == null || previousRule(player, next));

        public bool Request(bool active)
        {
            var local = NetworkPlayer.Local;
            if (messaging == null || local == null || !local.IsSpawned || Pending) return false;
            if (active && local.State != PlayerState.Normal) return false;
            Pending = true; uint revision = ++sequence;
            if (session.IsHost) ServerApply(local.OwnerClientId, active, revision);
            else
            {
                using var writer = new FastBufferWriter(5, Allocator.Temp);
                writer.WriteValueSafe(active); writer.WriteValueSafe(revision);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
            return true;
        }

        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (!session.IsHost || !reader.TryBeginRead(5)) return;
            reader.ReadValueSafe(out bool active); reader.ReadValueSafe(out uint revision);
            ServerApply(sender, active, revision);
        }

        void ServerApply(ulong actor, bool active, uint revision)
        {
            if (revisions.TryGetValue(actor, out uint previous) && revision <= previous) return;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All) if (candidate.OwnerClientId == actor) { player = candidate; break; }
            active &= player != null && player.IsSpawned && player.State == PlayerState.Normal;
            revisions[actor] = revision;
            if (active)
            {
                inspecting.Add(actor);
                HumanCannonSystem.Current?.ServerReleaseForInspection(actor);
                TightropeHangSystem.Current?.ServerReleaseForInspection(actor);
                PiggybackSystem.Current?.ServerReleaseForInspection(actor);
            }
            else inspecting.Remove(actor);
            if (actor == session.NetworkManager.LocalClientId) ApplyLocal(active, revision);
            else
            {
                using var writer = new FastBufferWriter(5, Allocator.Temp);
                writer.WriteValueSafe(active); writer.WriteValueSafe(revision);
                messaging.SendNamedMessage(StateMessage, actor, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        void ReadState(ulong sender, FastBufferReader reader)
        {
            if (session.IsHost || sender != NetworkManager.ServerClientId || !reader.TryBeginRead(5)) return;
            reader.ReadValueSafe(out bool active); reader.ReadValueSafe(out uint revision);
            ApplyLocal(active, revision);
        }

        void ApplyLocal(bool active, uint revision)
        {
            if (revision != sequence) return;
            Pending = false;
            if (LocalActive == active) return;
            LocalActive = active; LocalChanged?.Invoke(active);
        }

        void Restarted(NetworkPlayer player)
        {
            if (player == null) return;
            inspecting.Remove(player.OwnerClientId);
            if (player.IsLocal && LocalActive) { LocalActive = false; Pending = false; LocalChanged?.Invoke(false); }
        }

        void Disconnect()
        {
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(RequestMessage);
                messaging.UnregisterNamedMessageHandler(StateMessage);
            }
            if (NetworkPlayer.ServerCanChangeState == AllowState) NetworkPlayer.ServerCanChangeState = previousRule;
            previousRule = null; messaging = null;
            inspecting.Clear(); revisions.Clear(); Pending = false;
            if (LocalActive) { LocalActive = false; LocalChanged?.Invoke(false); }
        }
        void OnDisable() { NetworkPlayer.Restarted -= Restarted; Disconnect(); }
    }
}
