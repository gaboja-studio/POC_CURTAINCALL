using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 로비의 출연 명단. F로 내 이름에 서명(준비)하거나 지운다. 호스트가 판정하고 모두에게 같은 명단을 보낸다(이름 메시지).
    /// 게임 상태가 바뀌면(시작·로비 복귀) 명단을 비운다. 명단 칸은 자리 번호(<see cref="NetworkPlayer.Slot"/>) 순서.
    /// </summary>
    public sealed class LobbyReadyBoard : LobbyInteractable
    {
        const string StateMessage = "CurtainCall.Lobby.ReadyState";
        const string ToggleMessage = "CurtainCall.Lobby.ReadyToggle";
        const string RequestMessage = "CurtainCall.Lobby.ReadyRequest";

        [Tooltip("자리 번호 0~3 순서의 명단 칸.")]
        public TextMeshPro[] rows = new TextMeshPro[0];
        public TextMeshPro summary;

        readonly HashSet<ulong> ready = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        int knownClients = -1;

        /// <summary>명단이 바뀌었다.</summary>
        public event Action Changed;

        public int ReadyCount => ready.Count;

        /// <summary>방에 있는 모두가 서명했는지.</summary>
        public bool AllReady => session != null && session.PlayerCount > 0 && ready.Count >= session.PlayerCount;

        public bool IsReady(ulong clientId) => ready.Contains(clientId);

        public bool LocalReady => session != null && session.NetworkManager != null && ready.Contains(session.NetworkManager.LocalClientId);

        void Start()
        {
            session = NetworkSessionManager.EnsureExists();
            if (session == null) return;
            session.ConnectionStateChanged += HandleConnection;
            session.GameStateChanged += HandleGameState;
            HandleConnection(session.ConnectionState);
            RefreshRows();
        }

        void OnDestroy()
        {
            Unregister();
            if (session == null) return;
            session.ConnectionStateChanged -= HandleConnection;
            session.GameStateChanged -= HandleGameState;
        }

        void Update()
        {
            // 호스트: 나간 사람의 서명을 지운다.
            if (session != null && session.IsHost && session.NetworkManager != null)
            {
                var ids = session.NetworkManager.ConnectedClientsIds;
                if (ids.Count != knownClients)
                {
                    knownClients = ids.Count;
                    if (ready.RemoveWhere(id => !Contains(ids, id)) > 0) Broadcast();
                }
            }
            RefreshRows();
        }

        public override string Prompt(out bool usable)
        {
            usable = session != null && session.GameState == GameSessionState.Waiting && messaging != null;
            if (!usable) return "출연 명단";
            return LocalReady ? "[F] 출연 명단에서 서명 지우기" : "[F] 출연 명단에 서명하기 (준비)";
        }

        public override void Use()
        {
            if (session == null || session.GameState != GameSessionState.Waiting || messaging == null) return;
            if (session.IsHost)
            {
                Toggle(session.NetworkManager.LocalClientId);
                return;
            }
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            messaging.SendNamedMessage(ToggleMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        // ── 호스트 판정 ──────────────────────────────────────

        void Toggle(ulong clientId)
        {
            if (session.GameState != GameSessionState.Waiting) return;
            if (!ready.Remove(clientId)) ready.Add(clientId);
            Broadcast();
        }

        void HandleGameState(GameSessionState state)
        {
            if (session == null || !session.IsHost) return;
            ready.Clear();
            Broadcast();
        }

        void Broadcast()
        {
            Changed?.Invoke();
            // Shutdown also changes game state, after the last recipient has left.
            var network = session != null ? session.NetworkManager : null;
            if (messaging == null || !session.IsHost || network == null || !network.IsListening
                || network.ShutdownInProgress || network.ConnectedClientsIds.Count == 0) return;
            using var writer = WriteState();
            messaging.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        FastBufferWriter WriteState()
        {
            var writer = new FastBufferWriter(4 + ready.Count * 8, Allocator.Temp);
            writer.WriteValueSafe(ready.Count);
            foreach (var id in ready) writer.WriteValueSafe(id);
            return writer;
        }

        // ── 메시지 ──────────────────────────────────────────

        void HandleConnection(SessionConnectionState connection)
        {
            Unregister();
            ready.Clear();
            knownClients = -1;
            if (connection != SessionConnectionState.Host && connection != SessionConnectionState.Client) return;
            messaging = session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;
            if (messaging == null) return;

            if (connection == SessionConnectionState.Host)
            {
                messaging.RegisterNamedMessageHandler(ToggleMessage, (sender, _) => Toggle(sender));
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            }
            else
            {
                messaging.RegisterNamedMessageHandler(StateMessage, HandleState);
                using var writer = new FastBufferWriter(1, Allocator.Temp);
                messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
            Changed?.Invoke();
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            using var writer = WriteState();
            messaging.SendNamedMessage(StateMessage, sender, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            ready.Clear();
            reader.ReadValueSafe(out int count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id);
                ready.Add(id);
            }
            Changed?.Invoke();
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(StateMessage);
            messaging.UnregisterNamedMessageHandler(ToggleMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging = null;
        }

        // ── 표시 ─────────────────────────────────────────────

        void RefreshRows()
        {
            ulong local = session != null && session.NetworkManager != null ? session.NetworkManager.LocalClientId : ulong.MaxValue;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                var player = Find(i);
                string text;
                if (player == null) text = $"<color=#7A6A5C>{i + 1}.  빈 자리</color>";
                else
                {
                    bool signed = ready.Contains(player.OwnerClientId);
                    string me = player.OwnerClientId == local ? " (나)" : "";
                    text = signed
                        ? $"{i + 1}.  단원 {i + 1}{me}   <color=#C8102E><b>서명 완료</b></color>"
                        : $"{i + 1}.  단원 {i + 1}{me}   <color=#7A6A5C>서명 전</color>";
                }
                if (rows[i].text != text) rows[i].text = text;
            }
            if (summary != null)
            {
                int total = session != null ? session.PlayerCount : 0;
                string text = total == 0 ? "방에 접속하면 서명할 수 있어요" : $"서명 {ready.Count} / {total}";
                if (summary.text != text) summary.text = text;
            }
        }

        static NetworkPlayer Find(int slot)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.Slot == slot) return player;
            return null;
        }

        static bool Contains(IReadOnlyList<ulong> ids, ulong id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;
            return false;
        }
    }
}
