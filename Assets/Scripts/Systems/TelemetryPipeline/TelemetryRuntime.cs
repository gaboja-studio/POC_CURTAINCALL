using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CurtainCall.Campaign;
using CurtainCall.Network.Session;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Reward;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Persistent, opt-in remote transport. No gameplay state, prefab or input changes.</summary>
    public sealed class TelemetryRuntime : MonoBehaviour
    {
        public static TelemetryRuntime Instance { get; private set; }
        DurableOutbox outbox;
        readonly Stopwatch elapsed = new();
        readonly FlowTimingTracker flowTiming = new();
        string playerId;
        long sequence;
        float sceneEnteredAt;
        float nextSeal;
        string sceneName;
        string endpoint;
        string token;
        bool quitting;
        NetworkSessionManager session;
        CampaignRun campaign;
        bool wasRunning;
        string localRun;
        int generation;
        string ledgerSession;
        int lastLedgerNumber;
        int? lastPlayerNumber;
        string lastNetworkClientId;
        NetworkLogAdapter networkLogs;
        PerformanceLogAdapter performanceLogs;
        WaitingRoomLogAdapter waitingLogs;
        InputLogAdapter inputLogs;
        public string LastError { get; private set; }
        public bool RemoteConfigured => !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(token);
        public string CurrentRunId => localRun;
        public string CurrentAttemptId { get; private set; }
        public string CurrentRoomId { get; private set; }
        public string CurrentPlayerId => playerId;
        public string FallbackPlayerId => outbox?.SenderId;
        public string CurrentBreakId { get; private set; }
        public event Action<LogEvent> Recorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; SceneManager.sceneLoaded -= EntrySceneLoaded; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void WatchEntryScenes() { SceneManager.sceneLoaded -= EntrySceneLoaded; SceneManager.sceneLoaded += EntrySceneLoaded; }
        static void EntrySceneLoaded(Scene scene, LoadSceneMode mode) => Bootstrap();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            string name = SceneManager.GetActiveScene().name;
            if (name != "Start" && name != "Lobby" && name != "Tightrope" && name != "HummanCannon"
                && name != "HumanCannon" && name != "WaitingRoomBase") return;
            var go = new GameObject(nameof(TelemetryRuntime));
            DontDestroyOnLoad(go);
            go.AddComponent<TelemetryRuntime>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            try
            {
                string root = Path.Combine(Application.persistentDataPath, "TelemetryPipeline");
                outbox = new DurableOutbox(root, Guid.NewGuid().ToString("D"));
                playerId = outbox.SenderId; // Explicit fallback identity; never pretend it is a service login ID.
                ConfigureFromEnvironment();
                elapsed.Start();
                networkLogs = new NetworkLogAdapter(this);
                performanceLogs = new PerformanceLogAdapter(this);
                waitingLogs = new WaitingRoomLogAdapter(this);
                inputLogs = new InputLogAdapter(this);
                SceneManager.activeSceneChanged += SceneChanged;
                sceneName = SceneManager.GetActiveScene().name;
                Emit("GAME_STARTED");
                EnterScene(sceneName);
                StartCoroutine(UploadLoop());
            }
            catch (Exception error) { StorageFailure(error); }
        }

        void ConfigureFromEnvironment()
        {
            string proposed = Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_URL");
            string proposedToken = Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_TOKEN");
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            // User setup can happen after Unity Hub starts; read its user-scoped values without printing them.
            proposed ??= Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_URL", EnvironmentVariableTarget.User);
            proposedToken ??= Environment.GetEnvironmentVariable("CURTAINCALL_TELEMETRY_TOKEN", EnvironmentVariableTarget.User);
#endif
            if (string.IsNullOrEmpty(proposed) || string.IsNullOrEmpty(proposedToken)) return;
            if (!Uri.TryCreate(proposed, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                || uri.Host != "script.google.com" || !uri.AbsolutePath.StartsWith("/macros/s/", StringComparison.Ordinal)
                || !uri.AbsolutePath.EndsWith("/exec", StringComparison.Ordinal) || !string.IsNullOrEmpty(uri.Query)
                || proposedToken.Length < 32) { LastError = "Invalid telemetry endpoint configuration."; return; }
            endpoint = proposed;
            token = proposedToken;
        }

        public bool Emit(string eventType, Dictionary<string, object> payload = null, string authority = "LOCAL")
            => EmitFor(null, eventType, payload, authority);

        public bool EmitFor(NetworkPlayer subject, string eventType, Dictionary<string, object> payload = null, string authority = "LOCAL", CampaignSnapshot? stepContext = null)
        {
            if (outbox == null) return false;
            try
            {
                var player = subject != null ? subject : NetworkPlayer.Local;
                var state = stepContext ?? (campaign != null ? campaign.State : default);
                if (subject == null && player != null && player.Slot is >= 0 and <= 3)
                { lastPlayerNumber = player.Slot; lastNetworkClientId = player.OwnerClientId.ToString(); }
                var entry = new LogEvent
                {
                    occurredAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    sequence = ++sequence, monotonicMs = elapsed.Elapsed.TotalMilliseconds,
                    environment = Application.isEditor || Debug.isDebugBuild ? "TEST" : "LIVE",
                    gameVersion = Application.version,
                    playerId = subject != null ? PlayerCode(subject.OwnerClientId) : playerId,
                    playerNumber = player != null && player.Slot >= 0 && player.Slot <= 3 ? player.Slot : eventType == "PLAYER_EXITED" ? lastPlayerNumber : null,
                    networkClientId = session != null && session.NetworkManager != null && session.NetworkManager.IsListening
                        ? (subject != null ? subject.OwnerClientId : session.NetworkManager.LocalClientId).ToString() : eventType == "PLAYER_EXITED" ? lastNetworkClientId : null,
                    roomId = CurrentRoomId, runId = localRun, attemptId = CurrentAttemptId,
                    performanceId = state.Day > 0 ? $"D{state.Day}P{state.PerformanceIndex + 1}" : null,
                    sceneType = SceneType(sceneName), eventType = eventType, authority = authority,
                    breakId = stepContext.HasValue ? DetailLogIds.Break(state) : CurrentBreakId,
                    payload = payload ?? new Dictionary<string, object>()
                };
                if (entry.sceneType == "MAINSTAGE" && entry.attemptId != null && player != null)
                    entry.payload = DetailLogIds.RoutePayload(eventType, entry.payload, player.transform.position);
                entry.payload = flowTiming.Enrich(entry);
                if (!EventContract.IsValid(entry)) { LastError = "Telemetry event rejected by v1 contract."; return false; }
                outbox.Append(entry);
                flowTiming.Observe(entry);
                LastError = null;
                if (Recorded != null)
                    foreach (Action<LogEvent> observer in Recorded.GetInvocationList())
                        try { observer(LogJson.Parse<LogEvent>(LogJson.Serialize(entry))); } catch { /* An observer cannot invalidate persisted data. */ }
                return true;
            }
            catch (Exception error) { StorageFailure(error); return false; }
        }

        public string PlayerCode(ulong clientId) => networkLogs?.PlayerCode(clientId) ?? playerId;
        public void UsePlayerIdentity(string identity) => playerId = string.IsNullOrEmpty(identity) ? FallbackPlayerId : identity;
        public void SetAttempt(string attemptId) => CurrentAttemptId = attemptId;
        public void SessionLeft()
        {
            performanceLogs?.Leave("DISCONNECTED"); inputLogs?.CloseMicrophone(); waitingLogs?.SceneLeaving();
            localRun = null; CurrentAttemptId = null; CurrentRoomId = null; CurrentBreakId = null; wasRunning = false;
            lastPlayerNumber = null; lastNetworkClientId = null;
            flowTiming.Reset();
        }

        // Domain adapters must supply host-synchronized identifiers before emitting detailed events.
        public void SetContext(string roomId, string runId, string attemptId, string authenticatedPlayerId = null)
        {
            if (roomId != null && !System.Text.RegularExpressions.Regex.IsMatch(roomId, "^[0-9a-f]{32}$"))
                throw new ArgumentException(nameof(roomId));
            if (runId != null && !Guid.TryParseExact(runId, "D", out _)) throw new ArgumentException(nameof(runId));
            if (attemptId != null && !Guid.TryParseExact(attemptId, "D", out _)) throw new ArgumentException(nameof(attemptId));
            if (authenticatedPlayerId != null && (authenticatedPlayerId.Length == 0 || authenticatedPlayerId.Length > 128))
                throw new ArgumentException(nameof(authenticatedPlayerId));
            CurrentRoomId = roomId;
            localRun = runId;
            CurrentAttemptId = attemptId;
            if (authenticatedPlayerId != null) playerId = authenticatedPlayerId;
        }

        void Update()
        {
            if (outbox == null) return;
            if (NetworkSessionManager.Instance != session)
            {
                session = NetworkSessionManager.Instance;
            }
            networkLogs.Tick(session);
            if (CampaignRun.Instance != campaign)
            {
                if (campaign != null) campaign.RunEnded -= RunEnded;
                campaign = CampaignRun.Instance;
                if (campaign != null) campaign.RunEnded += RunEnded;
            }
            var state = campaign != null ? campaign.State : default;
            var shared = PerformanceRewards.Instance != null ? PerformanceRewards.Instance.State : null;
            if (state.IsRunning && shared != null && shared.Generation == state.Generation
                && (!wasRunning || state.Generation != generation))
            {
                // Existing reward snapshots already carry a host-generated ID shared with every client.
                localRun = SharedRunId(shared.SessionId, state.Generation);
                generation = state.Generation;
                Emit("RUN_STARTED", new() { ["playerCount"] = Math.Max(1, session != null ? session.PlayerCount : 1), ["identityScope"] = "SHARED_REWARD_SESSION" });
                wasRunning = true;
            }
            if (!state.IsRunning) wasRunning = false;
            ObserveLedger(shared);
            CurrentBreakId = DetailLogIds.Break(state);
            performanceLogs.Tick();
            waitingLogs.Tick();
            inputLogs.Tick();
            if (Time.unscaledTime >= nextSeal) { nextSeal = Time.unscaledTime + 15f; Seal("CHECKPOINT"); }
        }

        public static string SharedRunId(string sharedSessionId, int runGeneration)
        {
            string hex = DurableOutbox.Sha256(sharedSessionId + ":generation:" + runGeneration).Substring(0, 32);
            return Guid.ParseExact(hex, "N").ToString("D");
        }

        void ObserveLedger(RewardSnapshot shared)
        {
            if (shared == null || session == null || !session.IsHost || localRun == null) return;
            if (ledgerSession != shared.SessionId) { ledgerSession = shared.SessionId; lastLedgerNumber = 0; }
            if (shared.LedgerStart > lastLedgerNumber)
            {
                Emit("TELEMETRY_GAP", new() { ["reason"] = "LEDGER_PAGE_NOT_OBSERVED", ["droppedEvents"] = shared.LedgerStart - lastLedgerNumber });
                lastLedgerNumber = shared.LedgerStart;
            }
            foreach (var entry in shared.Ledger)
            {
                if (entry.Number <= lastLedgerNumber) continue;
                if (entry.Generation != shared.Generation) { lastLedgerNumber = entry.Number; continue; }
                long before = entry.Balance - entry.Amount;
                if (before > 9007199254740991L || entry.Balance > 9007199254740991L) { LastError = "Telemetry currency exceeds exact JSON integer range."; return; }
                if (!Emit("FUNDS_CHANGED", new()
                {
                    ["transactionId"] = shared.SessionId + ":" + entry.Number,
                    ["gameDay"] = entry.Day, ["balanceBefore"] = before, ["balanceAfter"] = entry.Balance,
                    ["category"] = entry.Kind == FundEntryKind.PerformanceIncome ? "REWARD"
                        : entry.Kind == FundEntryKind.GenerationReset ? "RESET" : "EXPENSE"
                }, "HOST")) return;
                lastLedgerNumber = entry.Number;
            }
        }

        void RunEnded(RunEndReason reason)
        {
            performanceLogs?.Leave("RUN_ENDED"); waitingLogs?.SceneLeaving(); inputLogs?.CloseMicrophone();
            Emit("RUN_ENDED", new() { ["reason"] = reason.ToString(), ["identityScope"] = "SHARED_REWARD_SESSION" }); Seal("RUN_ENDED");
            CurrentAttemptId = null;
        }
        void SceneChanged(Scene previous, Scene next)
        {
            performanceLogs?.SceneLeaving();
            waitingLogs?.SceneLeaving();
            inputLogs?.CloseMicrophone();
            Emit("SCENE_EXITED", new() { ["sceneName"] = sceneName ?? previous.name, ["durationMs"] = (Time.unscaledTime - sceneEnteredAt) * 1000d });
            EnterScene(next.name); Seal("SCENE_CHANGED");
        }
        void EnterScene(string name)
        {
            sceneName = name; sceneEnteredAt = Time.unscaledTime;
            Emit("SCENE_ENTERED", new() { ["sceneName"] = name });
            if (name == "Start") Emit("TITLE_ENTERED");
        }
        public void Flush() => Seal("CHECKPOINT");
        void Seal(string reason) { try { outbox?.Seal(reason); } catch (Exception error) { StorageFailure(error); } }
        void StorageFailure(Exception error)
        {
            LastError = error is UnauthorizedAccessException ? "Telemetry storage permission denied." : "Telemetry storage unavailable; pending data retained.";
            Debug.LogWarning("[Telemetry] " + LastError); // Do not print paths, exception payload or credentials.
        }

        IEnumerator UploadLoop()
        {
            float backoff = 2;
            while (!quitting)
            {
                if (!RemoteConfigured) { yield return new WaitForSecondsRealtime(10); continue; }
                string path = null, raw = null;
                try { path = outbox.NextPath(); if (path != null) raw = File.ReadAllText(path); }
                catch (Exception error) { StorageFailure(error); }
                if (raw == null) { yield return new WaitForSecondsRealtime(2); continue; }
                var requestBody = new { token = token, batchJson = raw };
                using var request = new UnityWebRequest(endpoint, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(LogJson.Serialize(requestBody)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;
                yield return request.SendWebRequest();
                bool accepted = false;
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JObject.Parse(request.downloadHandler.text);
                        if (response.Value<bool?>("ok") == true)
                            accepted = outbox.Acknowledge(path, response.Value<string>("batchId"), response.Value<string>("sha256"), response.Value<int>("acceptedEvents"));
                    }
                }
                catch { /* Retain original on malformed, mismatched or interrupted acknowledgement. */ }
                if (accepted) { backoff = 2; LastError = null; }
                else { LastError = "Telemetry upload not acknowledged; retry pending."; backoff = Math.Min(backoff * 2, 300); }
                yield return new WaitForSecondsRealtime(accepted ? 1 : backoff + UnityEngine.Random.Range(0, 2f));
            }
        }
        void OnApplicationPause(bool paused) { if (paused) Seal("CHECKPOINT"); }
        void OnApplicationQuit()
        {
            quitting = true;
            performanceLogs?.Leave("APPLICATION_QUIT");
            waitingLogs?.SceneLeaving();
            inputLogs?.CloseMicrophone();
            Emit("GAME_EXITED", new() { ["reason"] = "APPLICATION_QUIT" }); Seal("GAME_EXITED");
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.activeSceneChanged -= SceneChanged;
            networkLogs?.Dispose(); performanceLogs?.Dispose(); waitingLogs?.Dispose(); inputLogs?.Dispose();
            if (campaign != null) campaign.RunEnded -= RunEnded;
            Seal("CHECKPOINT"); Instance = null;
        }
        public static string SceneType(string name) => name switch
        {
            "Start" => "TITLE", "Lobby" => "LOBBY", "WaitingRoomBase" => "WAITINGROOM",
            "Tightrope" or "HummanCannon" or "HumanCannon" => "MAINSTAGE", _ => "UNKNOWN"
        };
    }
}
