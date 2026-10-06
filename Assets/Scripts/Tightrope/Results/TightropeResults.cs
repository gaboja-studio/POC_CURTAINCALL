using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.Results
{
    /// <summary>호스트 명부와 확정 결과의 단일 공유 진입점. Run의 시작·종료 경계에서 호출한다.</summary>
    public sealed class TightropeResults : MonoBehaviour
    {
        const string SnapshotMessage = "CurtainCall.Tightrope.Results";
        const string RequestMessage = "CurtainCall.Tightrope.ResultsRequest";
        const int MaxPayloadBytes = 65536;
        readonly Dictionary<string, ParticipantData> roster = new();
        readonly Dictionary<ulong, string> connectionKeys = new();
        readonly Dictionary<ulong, NetworkPlayer> connections = new();
        readonly Dictionary<string, TightropeResult> history = new();
        readonly Queue<string> historyOrder = new();
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        ResultData latestData;
        int connectionGeneration;
        int retiredRound;
        float nextRequest;
        bool attemptOpen;

        public string RoomId { get; private set; }
        public string SessionId { get; private set; }
        public string AttemptId { get; private set; }
        public int Round { get; private set; }
        public TightropeResult Latest { get; private set; }
        public bool IsAttemptOpen => attemptOpen;
        public IReadOnlyList<TightropeParticipantResult> Participants { get; private set; }
            = Array.Empty<TightropeParticipantResult>();
        public event Action AttemptStarted;
        public event Action<TightropeResult> ResultConfirmed;
        public event Action Cleared;
        public event Action RosterChanged;

        public TightropeResult Find(string attemptId) =>
            attemptId != null && history.TryGetValue(attemptId, out var result) ? result : null;

        void OnDisable()
        {
            Unregister();
            if (session != null) session.ConnectionStateChanged -= HandleConnection;
            session = null;
            Clear();
        }

        void Update()
        {
            Bind();
            if (session != null && session.ConnectionState == SessionConnectionState.Client
                && Time.unscaledTime >= nextRequest)
            {
                nextRequest = Time.unscaledTime + 5f;
                RequestSnapshot();
            }
        }

        void Bind()
        {
            if (session != null || NetworkSessionManager.Instance == null) return;
            session = NetworkSessionManager.Instance;
            session.ConnectionStateChanged += HandleConnection;
            HandleConnection(session.ConnectionState);
        }

        void HandleConnection(SessionConnectionState state)
        {
            Unregister();
            if (state == SessionConnectionState.Offline || state == SessionConnectionState.Connecting)
            {
                Clear();
                return;
            }
            messaging = session.NetworkManager.CustomMessagingManager;
            if (messaging == null) return;
            if (state == SessionConnectionState.Host)
            {
                EnsureHostRoom();
                messaging.RegisterNamedMessageHandler(RequestMessage, HandleRequest);
            }
            else if (state == SessionConnectionState.Client)
            {
                messaging.RegisterNamedMessageHandler(SnapshotMessage, HandleSnapshot);
                RequestSnapshot();
            }
        }

        void EnsureHostRoom()
        {
            if (!string.IsNullOrEmpty(RoomId)) return;
            RoomId = Guid.NewGuid().ToString("N");
            SessionId = Guid.NewGuid().ToString("N");
        }

        public void BeginAttempt(int round)
        {
            Bind();
            if (session == null || !session.IsHost || round <= Round) return;
            EnsureHostRoom();
            Round = round;
            AttemptId = Guid.NewGuid().ToString("N");
            roster.Clear();
            attemptOpen = true;
            ObserveParticipants();
            RefreshRoster();
            AttemptStarted?.Invoke();
            Broadcast();
        }

        public void ObserveParticipants()
        {
            if (session == null || !session.IsHost || !attemptOpen) return;
            bool changed = false;
            foreach (var entry in roster.Values)
            {
                bool present = false;
                foreach (var player in NetworkPlayer.All)
                    if (player.OwnerClientId == entry.clientId
                        && connections.TryGetValue(entry.clientId, out var known) && known == player
                        && connectionKeys[entry.clientId] == entry.participantKey)
                    { present = true; break; }
                if (!present && entry.connected) { entry.connected = false; changed = true; }
            }
            foreach (var player in NetworkPlayer.All)
            {
                ulong id = player.OwnerClientId;
                if (!connections.TryGetValue(id, out var known) || known != player)
                {
                    connections[id] = player;
                    connectionKeys[id] = RoomId + ":" + id + ":" + ++connectionGeneration;
                }
                string key = connectionKeys[id];
                if (!roster.TryGetValue(key, out var entry))
                {
                    if (roster.Count >= 64) continue;
                    entry = new ParticipantData
                    {
                        participantKey = key, clientId = id,
                        startingLostParts = (int)player.LostParts, connected = true,
                        outcome = (byte)ParticipantOutcome.Unfinished
                    };
                    roster[key] = entry;
                    changed = true;
                }
                if (entry.slot != player.Slot || entry.lostParts != (int)player.LostParts
                    || entry.state != (byte)player.State) changed = true;
                entry.slot = player.Slot;
                entry.lostParts = (int)player.LostParts;
                entry.state = (byte)player.State;
            }
            if (!changed) return;
            RefreshRoster();
            RosterChanged?.Invoke();
            Broadcast();
        }

        public void Confirm(bool succeeded, TightropeEndReason reason, float elapsed, float remaining, float performanceElapsed)
        {
            if (session == null || !session.IsHost || !attemptOpen || Find(AttemptId) != null
                || reason > TightropeEndReason.TimeExpired || (int)reason < 0
                || succeeded != (reason == TightropeEndReason.Completed)
                || !ValidTime(elapsed) || !ValidTime(remaining) || !ValidTime(performanceElapsed)) return;
            ObserveParticipants();
            var people = CopyRoster();
            foreach (var person in people)
            {
                person.outcome = (byte)(!person.connected ? ParticipantOutcome.Disconnected
                    : (PlayerState)person.state == PlayerState.Fallen ? ParticipantOutcome.Fallen
                    : (PlayerState)person.state == PlayerState.Arrived ? ParticipantOutcome.DirectFinish
                    : succeeded ? ParticipantOutcome.CompanionSuccess : ParticipantOutcome.Unfinished);
            }
            latestData = new ResultData
            {
                roomId = RoomId, sessionId = SessionId, attemptId = AttemptId, round = Round,
                succeeded = succeeded, reason = (byte)reason, elapsed = Mathf.Max(0f, elapsed),
                remaining = Mathf.Max(0f, remaining), performanceElapsed = Mathf.Max(0f, performanceElapsed),
                finishedUtc = DateTime.UtcNow.ToString("O"), participants = people
            };
            attemptOpen = false;
            AcceptResult(latestData);
            Broadcast();
        }

        ParticipantData[] CopyRoster()
        {
            var people = new List<ParticipantData>();
            foreach (var entry in roster.Values) people.Add(entry.Copy());
            people.Sort((a, b) => string.CompareOrdinal(a.participantKey, b.participantKey));
            return people.ToArray();
        }

        void RefreshRoster()
        {
            var people = CopyRoster();
            var views = new TightropeParticipantResult[people.Length];
            for (int i = 0; i < views.Length; i++) views[i] = new TightropeParticipantResult(people[i]);
            Participants = Array.AsReadOnly(views);
        }

        void AcceptResult(ResultData data)
        {
            if (data.round <= retiredRound || history.ContainsKey(data.attemptId)) return;
            var result = new TightropeResult(data);
            history.Add(data.attemptId, result);
            historyOrder.Enqueue(data.attemptId);
            while (historyOrder.Count > 10)
            {
                string id = historyOrder.Dequeue();
                retiredRound = Math.Max(retiredRound, history[id].Round);
                history.Remove(id);
            }
            if (Latest == null || result.Round > Latest.Round)
            {
                Latest = result;
                latestData = data;
            }
            ResultConfirmed?.Invoke(result);
        }

        void Broadcast()
        {
            if (messaging == null) return;
            if (!TryWriteSnapshot(out var writer)) return;
            using var snapshot = writer;
            messaging.SendNamedMessageToAll(SnapshotMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        bool TryWriteSnapshot(out FastBufferWriter writer)
        {
            writer = default;
            string json = JsonUtility.ToJson(new ResultEnvelope
            {
                roomId = RoomId, sessionId = SessionId, attemptId = AttemptId,
                round = Round, roster = CopyRoster(), latest = latestData
            });
            int bytes = FastBufferWriter.GetWriteSize(json);
            if (bytes > MaxPayloadBytes)
            {
                Debug.LogWarning("[Results] 결과 메시지가 허용 용량을 초과했습니다.");
                return false;
            }
            writer = new FastBufferWriter(bytes, Allocator.Temp);
            writer.WriteValueSafe(json);
            return true;
        }

        void HandleRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !session.NetworkManager.ConnectedClients.ContainsKey(sender)) return;
            if (messaging == null || !TryWriteSnapshot(out var writer)) return;
            using var snapshot = writer;
            messaging.SendNamedMessage(SnapshotMessage, sender, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        public void RequestSnapshot()
        {
            Bind();
            if (messaging == null || session.ConnectionState != SessionConnectionState.Client) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length > MaxPayloadBytes) return;
            try
            {
                reader.ReadValueSafe(out string json);
                var data = JsonUtility.FromJson<ResultEnvelope>(json);
                if (!ValidSnapshot(data)) return;
                if (!string.IsNullOrEmpty(RoomId) && (RoomId != data.roomId || SessionId != data.sessionId)) return;
                if (data.round < Round || (Round > 0 && data.round == Round && AttemptId != data.attemptId)) return;
                bool started = data.round > Round;
                RoomId = data.roomId;
                SessionId = data.sessionId;
                Round = data.round;
                AttemptId = data.attemptId;
                roster.Clear();
                foreach (var entry in data.roster) roster.Add(entry.participantKey, entry);
                RefreshRoster();
                attemptOpen = data.round > 0 && (data.latest == null || data.latest.round != data.round);
                if (started) AttemptStarted?.Invoke();
                RosterChanged?.Invoke();
                if (data.latest != null) AcceptResult(data.latest);
            }
            catch (Exception e) when (e is ArgumentException || e is OverflowException || e is InvalidOperationException
                || e is IndexOutOfRangeException)
            {
                Debug.LogWarning("[Results] 결과 메시지를 읽지 못했습니다.");
            }
        }

        static bool ValidSnapshot(ResultEnvelope data)
        {
            if (data == null || !IsId(data.roomId) || !IsId(data.sessionId) || data.round < 0
                || (data.round > 0 ? !IsId(data.attemptId) : !string.IsNullOrEmpty(data.attemptId))
                || !ValidParticipants(data.roster)) return false;
            var result = data.latest;
            return result == null || (IsId(result.attemptId) && result.roomId == data.roomId
                && result.sessionId == data.sessionId && result.round > 0 && result.round <= data.round
                && (result.round != data.round || result.attemptId == data.attemptId)
                && result.reason <= (byte)TightropeEndReason.TimeExpired
                && result.succeeded == (result.reason == (byte)TightropeEndReason.Completed)
                && ValidTime(result.elapsed) && ValidTime(result.remaining) && ValidTime(result.performanceElapsed)
                && DateTime.TryParse(result.finishedUtc, out _) && ValidParticipants(result.participants));
        }

        static bool ValidParticipants(ParticipantData[] people)
        {
            if (people == null || people.Length > 64) return false;
            var keys = new HashSet<string>();
            foreach (var person in people)
                if (person == null || string.IsNullOrEmpty(person.participantKey) || person.participantKey.Length > 100
                    || !keys.Add(person.participantKey) || person.slot < -1 || person.slot > 63
                    || (person.startingLostParts & ~15) != 0 || (person.lostParts & ~15) != 0
                    || person.state > (byte)PlayerState.Arrived || person.outcome > (byte)ParticipantOutcome.Unfinished)
                    return false;
            return true;
        }

        static bool ValidTime(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        static bool IsId(string value) => Guid.TryParseExact(value, "N", out _);

        void Clear()
        {
            RoomId = SessionId = AttemptId = null;
            Round = connectionGeneration = retiredRound = 0;
            nextRequest = 0f;
            roster.Clear();
            connections.Clear();
            connectionKeys.Clear();
            history.Clear();
            historyOrder.Clear();
            latestData = null;
            Latest = null;
            attemptOpen = false;
            Participants = Array.Empty<TightropeParticipantResult>();
            Cleared?.Invoke();
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(SnapshotMessage);
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging = null;
        }
    }
}
