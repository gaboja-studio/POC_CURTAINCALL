using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Network.Session;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Reward;
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
        readonly CancellationTokenSource uploadLifetime = new();
        string playerId;
        long sequence;
        float sceneEnteredAt;
        float nextSeal;
        string sceneName;
        string endpoint;
        string token;
        bool quitting;
        bool hasHosted;
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
        public string LastUploadError { get; private set; }
        public string LastUploadAt { get; private set; }
        public int PendingBatchCount => outbox?.PendingBatchCount ?? 0;
        internal Task<DurableOutbox.RelayPreparation> PrepareRelayAsync(CancellationToken cancellation)
            => outbox != null ? outbox.PrepareRelayAsync(cancellation) : Task.FromResult(new DurableOutbox.RelayPreparation());
        internal bool AcceptRelay(string raw) => outbox != null && outbox.AcceptRelay(raw);
        internal Task<bool> AcknowledgeRelayAsync(string path, string id, string hash, int count, CancellationToken cancellation)
            => outbox != null ? outbox.AcknowledgeRelayAsync(path, id, hash, count, cancellation) : Task.FromResult(false);
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
            if (Instance != null || !PlaytestBuildTags.Current().AllowsLogs) return;
            string name = SceneManager.GetActiveScene().name;
            if (name != "Start" && name != "Lobby" && name != "Tightrope" && name != "HummanCannon"
                && name != "HumanCannon" && name != "WaitingRoomBase") return;
            var go = new GameObject(nameof(TelemetryRuntime));
            DontDestroyOnLoad(go);
            go.AddComponent<TelemetryRuntime>();
        }

        void Awake()
        {
            if (!PlaytestBuildTags.Current().AllowsLogs) { Destroy(gameObject); return; }
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            try
            {
                string root = StorageDirectory(Application.persistentDataPath, Application.dataPath);
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
                ReportProgress(RemoteConfigured ? "수집 시작 · 자동 전송 설정 완료" : "수집 시작 · 전송 미설정, PC에만 저장");
            }
            catch (Exception error) { StorageFailure(error); }
        }

        void ConfigureFromEnvironment()
        {
            // Distributed PC builds carry the builder's settings; players need no environment setup.
            var configuration = !Application.isEditor ? TelemetryConfiguration.FromFile(Application.streamingAssetsPath) : null;
            configuration ??= TelemetryConfiguration.FromEnvironment();
            if (!configuration.IsValid) { LastError = "Invalid telemetry endpoint configuration."; return; }
            endpoint = configuration.endpoint;
            token = configuration.token;
        }

        // MPPM shares persistentDataPath with its parent Editor. Keep stable,
        // separate virtual-project queues and leave the parent's existing queue intact.
        public static string StorageDirectory(string persistentPath, string dataPath)
        {
            string root = Path.Combine(persistentPath, "TelemetryPipeline");
            string normalized = dataPath.Replace('\\', '/').TrimEnd('/');
            if (normalized.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase) < 0) return root;
            return Path.Combine(root, "clients", DurableOutbox.Sha256(normalized.ToLowerInvariant()).Substring(0, 24));
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
                    playerNumber = player != null && player.Slot >= 0 && player.Slot <= 3 ? player.Slot : CurrentRoomId != null ? lastPlayerNumber : null,
                    networkClientId = session != null && session.NetworkManager != null && session.NetworkManager.IsListening
                        ? (subject != null ? subject.OwnerClientId : session.NetworkManager.LocalClientId).ToString() : CurrentRoomId != null ? lastNetworkClientId : null,
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
        public string ParticipantCode(ulong clientId) => (CurrentRoomId ?? "unresolved") + ":client:" + clientId;
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
            if (session != null && session.IsHost) hasHosted = true;
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
            if (Time.unscaledTime >= nextSeal)
            { nextSeal = Time.unscaledTime + 15f; Seal("CHECKPOINT"); }
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
        void Seal(string reason)
        {
            try
            {
                outbox?.Seal(reason);
                if (PendingBatchCount > 0) ReportProgress($"로컬 저장 · 전송 대기 {PendingBatchCount}묶음 · {reason}");
            }
            catch (Exception error) { StorageFailure(error); }
        }
        static void ReportProgress(string message)
        {
            if (Application.isEditor || Debug.isDebugBuild) Debug.Log("[Telemetry] " + message);
        }
        void StorageFailure(Exception error)
        {
            LastError = error is UnauthorizedAccessException ? "Telemetry storage permission denied." : "Telemetry storage unavailable; pending data retained.";
            Debug.LogWarning("[Telemetry] " + LastError); // Do not print paths, exception payload or credentials.
        }

        bool CanUpload() => RemoteConfigured && TransportPolicy.CanUpload(
            session != null && session.IsHost,
            session != null && session.ConnectionState == SessionConnectionState.Client, hasHosted);

        IEnumerator UploadLoop()
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(uploadLifetime.Token);
            float backoff = 2;
            try
            {
                while (!quitting)
                {
                    if (!CanUpload()) { yield return new WaitForSecondsRealtime(2); continue; }
                    var queue = outbox;
                    var preparation = queue.PrepareUploadAsync(token, cancellation.Token);
                    while (!preparation.IsCompleted) yield return null;
                    if (quitting) yield break;
                    // 완료된 Task만 읽는다. I/O를 기다리며 메인스레드를 막지 않는다.
                    var upload = preparation.GetAwaiter().GetResult();
                    if (upload.Error != null) StorageFailure(upload.Error);
                    if (upload.Body == null || queue != outbox || !CanUpload())
                    { yield return new WaitForSecondsRealtime(2); continue; }

                    using var request = new UnityWebRequest(endpoint, "POST");
                    request.uploadHandler = new UploadHandlerRaw(upload.Body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 30;
                    yield return request.SendWebRequest();
                    if (quitting) yield break;
                    bool accepted = false;
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string response = request.downloadHandler.text;
                        var acknowledgement = queue.AcknowledgeUploadAsync(upload.Path, response, cancellation.Token);
                        while (!acknowledgement.IsCompleted) yield return null;
                        if (quitting) yield break;
                        accepted = acknowledgement.GetAwaiter().GetResult();
                    }
                    if (accepted)
                    {
                        backoff = 2; LastError = null; LastUploadError = null;
                        LastUploadAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                        ReportProgress("Drive 저장 확인 · 다음 기록 전송 계속");
                        yield return null;
                    }
                    else
                    {
                        if (LastUploadError == null) ReportProgress("Drive 전송 미확인 · 기록 보관 후 자동 재시도");
                        LastUploadError = "Telemetry upload not acknowledged; retry pending.";
                        LastError = LastUploadError; backoff = Math.Min(backoff * 2, 300);
                        yield return new WaitForSecondsRealtime(backoff + UnityEngine.Random.Range(0, 2f));
                    }
                }
            }
            finally { cancellation.Cancel(); }
        }
        void OnApplicationPause(bool paused) { if (paused) Seal("CHECKPOINT"); }
        void OnApplicationQuit()
        {
            quitting = true;
            uploadLifetime.Cancel();
            performanceLogs?.Leave("APPLICATION_QUIT");
            waitingLogs?.SceneLeaving();
            inputLogs?.CloseMicrophone();
            Emit("GAME_EXITED", new() { ["reason"] = "APPLICATION_QUIT" }); Seal("GAME_EXITED");
        }
        void OnDestroy()
        {
            uploadLifetime.Cancel();
            if (Instance != this) return;
            quitting = true;
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
