using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.StartLobby;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Telemetry-only room handshake. Never records connection keys or service credentials.</summary>
    sealed class NetworkLogAdapter : IDisposable
    {
        const string Request = "CurtainCall.Log.RoomRequest.v1", Snapshot = "CurtainCall.Log.RoomState.v1";
        const string Relay = "CurtainCall.Log.Batch.v1", Receipt = "CurtainCall.Log.Receipt.v1";
        readonly TelemetryRuntime log;
        readonly Dictionary<ulong, string> identities = new();
        NetworkSessionManager session;
        CustomMessagingManager messages;
        LobbyReadyBoard board;
        string room, connectionIdentity;
        bool entered, signed;
        float nextRequest, nextFind;
        float nextRelay;
        string relayPath, relayHash, relayId;
        int relayCount;
        public NetworkLogAdapter(TelemetryRuntime runtime) => log = runtime;
        public string PlayerCode(ulong clientId) => identities.TryGetValue(clientId, out string id) ? id
            : session?.NetworkManager != null && session.NetworkManager.LocalClientId == clientId ? log.CurrentPlayerId
            : (room ?? "unresolved") + ":client:" + clientId;

        public void Tick(NetworkSessionManager value)
        {
            string identity = log.FallbackPlayerId;
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                identity = AuthenticationService.Instance.PlayerId;
            // Voice can sign in later during LAN play. Keep a room's participant identity stable.
            if (connectionIdentity != null) identity = connectionIdentity;
            log.UsePlayerIdentity(identity);
            if (!ReferenceEquals(session, value))
            {
                Unbind(); session = value;
                if (session != null) { session.ConnectionStateChanged += Connection; session.Disconnected += Disconnected; Connection(session.ConnectionState); }
            }
            if (messages != null && Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + 3;
                if (session.IsHost) identities[session.NetworkManager.LocalClientId] = log.CurrentPlayerId;
                else Send(Request, log.CurrentPlayerId, NetworkManager.ServerClientId);
            }
            if (room != null && session != null)
            {
                log.SetContext(room, log.CurrentRunId, log.CurrentAttemptId);
                var player = NetworkPlayer.Local;
                if (!entered && player != null && player.IsSpawned && player.Slot is >= 0 and <= 3)
                {
                    entered = log.Emit("ROOM_ENTERED", new() { ["roomType"] = RoomType, ["isHost"] = session.IsHost,
                        ["identityScope"] = identity == log.FallbackPlayerId ? "INSTALLATION" : "SERVICE_LOGIN" });
                    if (entered) log.Flush();
                }
            }
            if (Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 0.5f;
                var found = UnityEngine.Object.FindAnyObjectByType<LobbyReadyBoard>();
                if (board != found) { if (board != null) board.Changed -= Signature; board = found; signed = board != null && board.LocalReady; if (board != null) board.Changed += Signature; }
            }
            if (room != null && messages != null && session != null && !session.IsHost && Time.unscaledTime >= nextRelay)
            {
                nextRelay = Time.unscaledTime + 3;
                try
                {
                    string path = log.PendingPath;
                    if (path != null)
                    {
                        string raw = File.ReadAllText(path); var batch = LogJson.Parse<LogBatch>(raw);
                        if (TransportPolicy.RelayBatch(batch))
                        {
                            relayPath = path; relayHash = DurableOutbox.Sha256(raw); relayId = batch.batchId; relayCount = batch.events.Count;
                            Send(Relay, raw, NetworkManager.ServerClientId, true);
                        }
                    }
                }
                catch { /* Preserve the original until a host durability receipt arrives. */ }
            }
        }
        string RoomType => session?.ActiveConnector is ServicesSessionConnector ? "CODE" : "LAN";
        void Connection(SessionConnectionState state)
        {
            Unregister();
            if (state is not (SessionConnectionState.Host or SessionConnectionState.Client))
            { if (state == SessionConnectionState.Connecting) { room = null; entered = false; identities.Clear(); } return; }
            messages = session.NetworkManager.CustomMessagingManager;
            if (messages == null) return;
            nextRequest = 0;
            connectionIdentity = log.CurrentPlayerId;
            if (session.IsHost)
            {
                room ??= Guid.NewGuid().ToString("N");
                identities[session.NetworkManager.LocalClientId] = log.CurrentPlayerId;
                log.SetContext(room, log.CurrentRunId, log.CurrentAttemptId);
                messages.RegisterNamedMessageHandler(Request, ReadRequest);
                messages.RegisterNamedMessageHandler(Relay, ReadRelay);
                log.Emit("ROOM_CREATED", new() { ["roomType"] = RoomType });
            }
            else { messages.RegisterNamedMessageHandler(Snapshot, ReadSnapshot); messages.RegisterNamedMessageHandler(Receipt, ReadReceipt); }
        }
        void ReadRelay(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length > 256 * 1024
                || !session.NetworkManager.ConnectedClientsIds.Contains(sender)) return;
            try
            {
                reader.ReadValueSafe(out string raw);
                if (!log.AcceptRelay(raw)) return;
                var batch = LogJson.Parse<LogBatch>(raw);
                Send(Receipt, batch.batchId + ":" + DurableOutbox.Sha256(raw) + ":" + batch.events.Count, sender);
            }
            catch { /* No receipt on invalid data or host disk failure. */ }
        }
        void ReadReceipt(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length > 512 || relayPath == null) return;
            try
            {
                reader.ReadValueSafe(out string value);
                if (value != relayId + ":" + relayHash + ":" + relayCount) return;
                if (log.AcknowledgeRelay(relayPath, relayId, relayHash, relayCount)) { relayPath = null; nextRelay = 0; }
            }
            catch { }
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length > 1024) return;
            try
            {
                reader.ReadValueSafe(out string identity);
                foreach (ulong client in identities.Keys.ToArray())
                    if (!session.NetworkManager.ConnectedClientsIds.Contains(client)) identities.Remove(client);
                if (string.IsNullOrEmpty(identity) || identity.Length > 128 || identities.Count >= 4 && !identities.ContainsKey(sender)
                    || !session.NetworkManager.ConnectedClientsIds.Contains(sender)) return;
                identities[sender] = identity;
                var state = new RoomState { roomId = room };
                foreach (var pair in identities) state.people.Add(new Person { client = pair.Key, id = pair.Value });
                string json = JsonUtility.ToJson(state);
                foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                    if (client != NetworkManager.ServerClientId) Send(Snapshot, json, client);
            }
            catch (Exception) { /* Reject malformed telemetry message without affecting play. */ }
        }
        void ReadSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length > 4096) return;
            try
            {
                reader.ReadValueSafe(out string json); var state = JsonUtility.FromJson<RoomState>(json);
                if (state == null || !Guid.TryParseExact(state.roomId, "N", out _) || state.people == null || state.people.Count > 4) return;
                var checkedIds = new Dictionary<ulong, string>();
                foreach (var person in state.people)
                    if (person == null || string.IsNullOrEmpty(person.id) || person.id.Length > 128 || !checkedIds.TryAdd(person.client, person.id)) return;
                if (room != null && room != state.roomId) return;
                room = state.roomId; identities.Clear(); foreach (var pair in checkedIds) identities.Add(pair.Key, pair.Value);
            }
            catch (Exception) { }
        }
        void Send(string name, string value, ulong target, bool fragmented = false)
        {
            var network = session?.NetworkManager;
            if (messages == null || network == null || !network.IsListening || network.ShutdownInProgress) return;
            using var writer = new FastBufferWriter(fragmented ? 256 * 1024 : 4096, Allocator.Temp);
            writer.WriteValueSafe(value); messages.SendNamedMessage(name, target, writer,
                fragmented ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.ReliableSequenced);
        }
        void Signature()
        {
            if (board == null || session == null || session.GameState != GameSessionState.Waiting || !entered) return;
            bool current = board.LocalReady; if (signed == current) return;
            if (log.Emit("SIGNATURE_CHANGED", new() { ["signed"] = current })) { signed = current; log.Flush(); }
        }
        void Disconnected(string reason)
        {
            if (entered) { log.Emit("PLAYER_EXITED", new() { ["reason"] = DetailLogIds.Disconnect(reason) }); log.Flush(); }
            log.SessionLeft(); room = null; entered = false; connectionIdentity = null; identities.Clear(); Unregister();
        }
        void Unregister()
        {
            if (messages == null) return;
            messages.UnregisterNamedMessageHandler(Request); messages.UnregisterNamedMessageHandler(Snapshot);
            messages.UnregisterNamedMessageHandler(Relay); messages.UnregisterNamedMessageHandler(Receipt);
            messages = null; relayPath = null;
        }
        void Unbind()
        {
            Unregister(); if (session != null) { session.ConnectionStateChanged -= Connection; session.Disconnected -= Disconnected; }
        }
        public void Dispose() { Unbind(); if (board != null) board.Changed -= Signature; }
        [Serializable] sealed class Person { public ulong client; public string id; }
        [Serializable] sealed class RoomState { public string roomId; public List<Person> people = new(); }
    }
}
