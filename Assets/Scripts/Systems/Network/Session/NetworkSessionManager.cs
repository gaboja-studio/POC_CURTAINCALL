using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CurtainCall.Dev;
using CurtainCall.Settings;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 방 접속과 게임 진행 상태의 공개 진입점. 다른 기능은 이 클래스의 메서드·이벤트만 사용한다.
    /// NetworkManager 프리팹(Resources/Prefabs/Controllers/Network/NetworkManager)에 함께 붙는다.
    /// 정원·최소 시작 인원은 게임 기본 세팅(세션)에서 읽는다(호스트 판정이므로 호스트 값 기준).
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkSessionManager : MonoBehaviour
    {
        const string PrefabPath = "Prefabs/Controllers/Network/NetworkManager";
        const string StateSyncPrefabPath = "Prefabs/Controllers/Network/GameSession";

        public static NetworkSessionManager Instance { get; private set; }

        /// <summary>씬에 없으면 Resources의 NetworkManager 프리팹을 만들어 돌려준다.</summary>
        public static NetworkSessionManager EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var prefab = Resources.Load<NetworkSessionManager>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Session] Resources/{PrefabPath} 프리팹을 찾지 못했습니다.");
                return null;
            }

            Instantiate(prefab);
            return Instance;
        }

        public SessionConnectionState ConnectionState { get; private set; } = SessionConnectionState.Offline;

        /// <summary>모든 플레이어가 같게 보는 게임 상태. 접속이 끊기면 진행 중이었어도 Ended로 남는다.</summary>
        public GameSessionState GameState { get; private set; } = GameSessionState.Waiting;

        /// <summary>현재 접속 인원. 호스트·클라이언트 모두 같은 값을 본다.</summary>
        public int PlayerCount { get; private set; }

        /// <summary>세션 최대 인원(정원).</summary>
        public int RequiredPlayers => GameSettings.Base.Session.MaxPlayers;

        /// <summary>호스트 포함 최소 시작 인원. 기본 규칙·개발자 모드에 현재 콘텐츠의 완화 요청을 적용한다.</summary>
        public int MinPlayers
        {
            get
            {
                int minimum = DevMode.MinPlayers(GameSettings.Base.Session.MinPlayers);
                foreach (var request in _minimumStartPlayerRequests) minimum = Mathf.Min(minimum, request.Minimum);
                return minimum;
            }
        }

        /// <summary>
        /// 이 세션에서 콘텐츠가 활성인 동안 최소 시작 인원을 낮춘다. OnDisable에서 반환한 토큰을 Dispose한다.
        /// 여러 요청은 가장 낮은 값을 사용하며, 마지막 요청 해제 후 기본 규칙으로 돌아간다.
        /// 전역 세팅·개발자 모드·접속 정원·현재 게임 상태는 바꾸지 않는다. 실제 시작 권한은 여전히 호스트에게 있다.
        /// </summary>
        public IDisposable RequestMinimumStartPlayers(int minimum)
        {
            if (minimum < 1) throw new ArgumentOutOfRangeException(nameof(minimum), "최소 시작 인원은 1명 이상이어야 합니다.");
            var request = new MinimumStartPlayerRequest(this, minimum);
            _minimumStartPlayerRequests.Add(request);
            return request;
        }

        /// <summary>호스트가 지금 게임을 시작할 수 있는지(대기 중 + 최소 인원 이상).</summary>
        public bool CanStartGame => IsHost && GameState == GameSessionState.Waiting && PlayerCount >= MinPlayers;

        /// <summary>참가자에게 알려 줄 접속 키(세션은 방 코드, LAN은 "IP:포트"). 접속 전에는 null.</summary>
        public string JoinKey => _connector?.JoinKey;

        /// <summary>마지막으로 접속이 끝난 이유. 접속 중에는 null.</summary>
        public string LastDisconnectReason { get; private set; }

        /// <summary>현재 접속 방식. 세션 세부 정보가 필요하면 <see cref="ServicesSessionConnector"/>로 확인한다.</summary>
        public ISessionConnector ActiveConnector => _connector;

        public NetworkManager NetworkManager => _networkManager;

        public bool IsHost => ConnectionState == SessionConnectionState.Host;

        public event Action<SessionConnectionState> ConnectionStateChanged;

        /// <summary>게임 상태(대기/진행/종료)가 바뀔 때. 모든 플레이어에게 같은 순서로 온다.</summary>
        public event Action<GameSessionState> GameStateChanged;

        public event Action<int> PlayerCountChanged;

        /// <summary>내 접속이 끝났을 때(직접 나감·호스트 종료·연결 실패). 인자는 이유.</summary>
        public event Action<string> Disconnected;

        NetworkManager _networkManager;
        ISessionConnector _connector;
        GameObject _stateSyncPrefab;
        GameSessionStateSync _stateSync;
        string _pendingDisconnectReason;
        bool _leaveRequested;
        readonly List<MinimumStartPlayerRequest> _minimumStartPlayerRequests = new();

        sealed class MinimumStartPlayerRequest : IDisposable
        {
            NetworkSessionManager owner;
            public int Minimum { get; }

            public MinimumStartPlayerRequest(NetworkSessionManager owner, int minimum)
            {
                this.owner = owner;
                Minimum = minimum;
            }

            public void Dispose()
            {
                var previous = owner;
                owner = null;
                if (previous != null) previous._minimumStartPlayerRequests.Remove(this);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _networkManager = GetComponent<NetworkManager>();
            EnsureTransportAssigned();
            RegisterStateSyncPrefab();

            // The UI allows 30 seconds, but NGO's default handshake deadline is 10.
            // A cold scene/shader load must not abort the same join attempt earlier underneath it.
            _networkManager.NetworkConfig.ClientConnectionBufferTimeout = Mathf.Max(
                _networkManager.NetworkConfig.ClientConnectionBufferTimeout, JoinTimeoutMilliseconds / 1000);
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = ApproveConnection;
            _networkManager.OnConnectionEvent += HandleConnectionEvent;
            _networkManager.OnClientStopped += HandleStopped;
            _networkManager.OnServerStopped += HandleStopped;
        }

        void OnDestroy()
        {
            _minimumStartPlayerRequests.Clear();
            if (Instance != this)
                return;

            if (_networkManager != null)
            {
                _networkManager.ConnectionApprovalCallback = null;
                _networkManager.OnConnectionEvent -= HandleConnectionEvent;
                _networkManager.OnClientStopped -= HandleStopped;
                _networkManager.OnServerStopped -= HandleStopped;
            }

            Instance = null;
        }

        /// <summary>인스펙터에서 Transport 연결이 빠졌어도 같은 오브젝트의 UnityTransport를 쓴다.</summary>
        void EnsureTransportAssigned()
        {
            if (_networkManager.NetworkConfig.NetworkTransport != null)
                return;

            var transport = GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[Session] NetworkManager 오브젝트에 UnityTransport가 없습니다.");
                return;
            }

            _networkManager.NetworkConfig.NetworkTransport = transport;
        }

        /// <summary>게임 상태 동기화 프리팹이 네트워크 프리팹 목록에 없으면 실행 중에 등록한다.</summary>
        void RegisterStateSyncPrefab()
        {
            var sync = Resources.Load<GameSessionStateSync>(StateSyncPrefabPath);
            if (sync == null)
            {
                Debug.LogError($"[Session] Resources/{StateSyncPrefabPath} 프리팹을 찾지 못했습니다. 게임 상태가 공유되지 않습니다.");
                return;
            }

            _stateSyncPrefab = sync.gameObject;
            foreach (var list in _networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (list != null && list.Contains(_stateSyncPrefab))
                    return;
            }

            _networkManager.AddNetworkPrefab(_stateSyncPrefab);
        }

        // ── 접속 ──────────────────────────────────────────────

        public Task<bool> HostLanAsync(ushort port = LanSessionConnector.DefaultPort) =>
            HostAsync(new LanSessionConnector(_networkManager, port));

        /// <param name="address">"IP" 또는 "IP:포트"</param>
        public Task<bool> JoinLanAsync(string address, ushort defaultPort = LanSessionConnector.DefaultPort) =>
            JoinAsync(new LanSessionConnector(_networkManager, defaultPort), address);

        /// <summary>Multiplayer Services 세션(Relay)으로 방을 연다. 방 코드는 <see cref="JoinKey"/>.</summary>
        public Task<bool> HostSessionAsync() =>
            HostAsync(new ServicesSessionConnector(_networkManager, RequiredPlayers));

        /// <summary>방 고유 코드로 세션에 참가한다.</summary>
        public Task<bool> JoinSessionAsync(string code) =>
            JoinAsync(new ServicesSessionConnector(_networkManager, RequiredPlayers), code);

        public const string JoinFailureMessage = "방이 없거나 네트워크 연결이 불안정 합니다";
        public const int JoinTimeoutMilliseconds = 30000;
        bool _cancellingConnect;
        DateTime _connectStartedUtc;

        public async Task<bool> HostAsync(ISessionConnector connector)
        {
            if (!BeginConnect(connector)) return false;
            try
            {
                if (await connector.HostAsync() && ReferenceEquals(_connector, connector) && !_cancellingConnect)
                {
                    SetState(SessionConnectionState.Host);
                    SpawnStateSync();
                    ServerRefresh();
                    return true;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Session] 방 생성 실패: {e.Message}"); }
            await CancelConnectAsync(connector, "방을 열지 못했습니다.");
            return false;
        }

        public async Task<bool> JoinAsync(ISessionConnector connector, string joinKey)
        {
            if (!BeginConnect(connector)) return false;
            var completion = WaitForClientAsync(connector, joinKey);
            // 커넥터의 true는 연결 시작일 수 있다. NGO의 실제 Client 상태까지 같은 제한시간을 쓴다.
            await Task.WhenAny(completion, Task.Delay(JoinTimeoutMilliseconds));
            if (completion.IsCompleted)
            {
                if (await completion) return true;
                await CancelConnectAsync(connector, JoinFailureMessage, "JoinFailed");
            }
            else
                await CancelConnectAsync(connector, JoinFailureMessage, "Timeout");
            return false;
        }

        async Task<bool> WaitForClientAsync(ISessionConnector connector, string joinKey)
        {
            try
            {
                if (!await connector.JoinAsync(joinKey)) return false;
                while (this != null && ReferenceEquals(_connector, connector) && !_cancellingConnect
                    && ConnectionState == SessionConnectionState.Connecting)
                    await Task.Delay(10);
                return this != null && ReferenceEquals(_connector, connector) && !_cancellingConnect
                    && ConnectionState == SessionConnectionState.Client;
            }
            catch (Exception e)
            {
                // SDK가 취소 예외를 감싸기도 한다. 이미 기록한 취소를 참가 오류로 중복 출력하지 않는다.
                if (ReferenceEquals(_connector, connector) && !_cancellingConnect)
                    Debug.LogWarning($"[Session] 방 참가 실패 [JoinError] mode={ConnectionMode(connector)}: {e}");
                return false;
            }
        }

        public async Task LeaveAsync()
        {
            var connector = _connector;
            if (connector == null || _cancellingConnect) return;
            if (ConnectionState == SessionConnectionState.Connecting)
            {
                await CancelConnectAsync(connector, "접속을 취소했습니다.", "UserCancelled");
                return;
            }
            _leaveRequested = true;
            await connector.LeaveAsync();
        }

        static string ConnectionMode(ISessionConnector connector) =>
            connector is LanSessionConnector ? "LAN" : connector is ServicesSessionConnector ? "Relay" : connector.GetType().Name;

        async Task CancelConnectAsync(ISessionConnector connector, string reason, string cause = "HostFailed")
        {
            if (this == null || !ReferenceEquals(_connector, connector) || _cancellingConnect) return;
            _cancellingConnect = true;
            string detail = !string.IsNullOrEmpty(_pendingDisconnectReason)
                ? _pendingDisconnectReason : _networkManager.DisconnectReason;
            Debug.Log($"[Session] 접속 취소 [{cause}] mode={ConnectionMode(connector)} elapsedMs={(DateTime.UtcNow - _connectStartedUtc).TotalMilliseconds:F0} timeoutMs={JoinTimeoutMilliseconds} disconnectReason={detail}");
            try
            {
                // Services는 먼저 시도를 무효화한다. 늦은 HTTP 완료가 새 NGO 연결을 시작하지 못한다.
                await connector.LeaveAsync();
            }
            catch (Exception e) { Debug.LogWarning($"[Session] 접속 취소 정리: {e.Message}"); }
            finally
            {
                if (_networkManager != null)
                {
                    if (_networkManager.IsListening) _networkManager.Shutdown();
                    while (_networkManager != null && _networkManager.ShutdownInProgress) await Task.Delay(10);
                }
                if (this != null && ReferenceEquals(_connector, connector))
                {
                    _cancellingConnect = false;
                    FailConnect(reason);
                }
            }
        }

        bool BeginConnect(ISessionConnector connector)
        {
            if (connector == null || _cancellingConnect || _networkManager.ShutdownInProgress
                || _networkManager.IsListening || ConnectionState != SessionConnectionState.Offline)
                return false;

            _connector = connector;
            _connectStartedUtc = DateTime.UtcNow;
            _pendingDisconnectReason = null;
            _leaveRequested = false;
            LastDisconnectReason = null;
            SetPlayerCount(0);
            SetGameState(GameSessionState.Waiting);
            SetState(SessionConnectionState.Connecting);
            return true;
        }

        void FailConnect(string reason)
        {
            _connector = null;
            LastDisconnectReason = reason;
            SetState(SessionConnectionState.Offline);
            Disconnected?.Invoke(reason);
        }

        // ── 게임 상태 (호스트 판정) ───────────────────────────

        /// <summary>호스트 전용. 최소 인원이 모였으면 대기 → 진행으로 바꾸고 세션을 잠근다(이후 참가 거절).</summary>
        public bool StartGame()
        {
            if (!CanStartGame)
            {
                Debug.LogWarning($"[Session] 게임을 시작할 수 없습니다(호스트·대기 중·최소 {MinPlayers}명 필요).");
                return false;
            }

            SetGameState(GameSessionState.Playing);
            _ = LockSessionAsync();
            PushStateToClients();
            return true;
        }

        /// <summary>호스트 전용. 진행 중인 게임을 종료 상태로 바꾼다(예: 도착 성공).</summary>
        public bool EndGame()
        {
            if (!IsHost)
            {
                Debug.LogWarning("[Session] EndGame은 호스트만 호출할 수 있습니다.");
                return false;
            }

            if (GameState != GameSessionState.Playing)
                return false;

            SetGameState(GameSessionState.Ended);
            PushStateToClients();
            return true;
        }

        /// <summary>호스트 전용. 끝난 게임을 대기로 되돌린다(로비 복귀). 세션 잠금을 풀어 다시 참가를 받고, 런 진행은 런 전으로 돌아간다.</summary>
        public bool ReturnToLobby()
        {
            if (!IsHost || GameState != GameSessionState.Ended)
                return false;

            SetGameState(GameSessionState.Waiting);
            _ = LockSessionAsync(false);
            PushStateToClients();
            return true;
        }

        /// <summary>콘텐츠가 성공 결과를 확인한 뒤 호출하는 호스트 재도전 경계. 최초 시작 조건은 바꾸지 않는다.</summary>
        internal bool RestartEndedGame()
        {
            if (!IsHost || GameState != GameSessionState.Ended || PlayerCount < MinPlayers) return false;
            SetGameState(GameSessionState.Playing);
            PushStateToClients();
            return true;
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool isHostSelf = request.ClientNetworkId == NetworkManager.ServerClientId;
            int connected = _networkManager.ConnectedClientsIds.Count;

            if (!isHostSelf && GameState != GameSessionState.Waiting)
                Reject(response, "게임이 이미 진행 중입니다.");
            else if (!isHostSelf && connected >= RequiredPlayers)
                Reject(response, "방이 가득 찼습니다.");
            else
                response.Approved = true;

            response.CreatePlayerObject = response.Approved && _networkManager.NetworkConfig.PlayerPrefab != null;
        }

        static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;
            Debug.Log($"[Session] 접속 거절: {reason}");
        }

        void SpawnStateSync()
        {
            if (_stateSyncPrefab == null)
                return;

            var instance = Instantiate(_stateSyncPrefab);
            instance.GetComponent<NetworkObject>().Spawn();
        }

        /// <summary>호스트: 인원을 다시 세어 모두에게 알린다. 게임 시작은 호스트가 <see cref="StartGame"/>으로 한다.</summary>
        void ServerRefresh()
        {
            if (!IsHost)
                return;

            SetPlayerCount(_networkManager.ConnectedClientsIds.Count);
            PushStateToClients();
        }

        void PushStateToClients()
        {
            if (_stateSync != null)
                _stateSync.ServerSet(GameState, PlayerCount);
        }

        /// <summary>시작 후 세션을 잠가 방 코드로 더 들어오지 못하게 한다(접속 승인과 이중 방어). 로비 복귀 때는 푼다.</summary>
        async Task LockSessionAsync(bool locked = true)
        {
            if (!(_connector is ServicesSessionConnector services) || !(services.CurrentSession is IHostSession host))
                return;

            try
            {
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] 세션 {(locked ? "잠금" : "잠금 해제")} 실패(접속 승인으로 막습니다): {e.Message}");
            }
        }

        internal void AttachStateSync(GameSessionStateSync sync)
        {
            _stateSync = sync;
            sync.Changed += HandleStateSyncChanged;

            if (IsHost)
                PushStateToClients();
            else
                HandleStateSyncChanged();
        }

        internal void DetachStateSync(GameSessionStateSync sync)
        {
            sync.Changed -= HandleStateSyncChanged;
            if (_stateSync == sync)
                _stateSync = null;
        }

        void HandleStateSyncChanged()
        {
            if (IsHost || _stateSync == null)
                return;

            SetPlayerCount(_stateSync.PlayerCount);
            SetGameState(_stateSync.State);
        }

        // ── 연결 이벤트 ───────────────────────────────────────

        void HandleConnectionEvent(NetworkManager networkManager, ConnectionEventData data)
        {
            if (_cancellingConnect || ConnectionState == SessionConnectionState.Offline) return;
            bool isLocal = data.ClientId == networkManager.LocalClientId;

            switch (data.EventType)
            {
                case ConnectionEvent.ClientConnected:
                    if (networkManager.IsServer)
                        ServerRefresh();
                    else if (isLocal)
                        SetState(SessionConnectionState.Client);
                    break;

                case ConnectionEvent.ClientDisconnected:
                    if (networkManager.IsServer)
                        ServerRefresh();
                    else if (isLocal)
                        _pendingDisconnectReason = networkManager.DisconnectReason;
                    break;
            }
        }

        void HandleStopped(bool wasHost)
        {
            // 호스트는 서버·클라이언트 정지가 둘 다 오므로 한 번만 처리한다.
            if (_cancellingConnect || ConnectionState == SessionConnectionState.Offline)
                return;
            if (ConnectionState == SessionConnectionState.Connecting)
            {
                _ = CancelConnectAsync(_connector, JoinFailureMessage, "NetworkStopped");
                return;
            }

            var previous = ConnectionState;
            string reason;
            if (_leaveRequested)
                reason = wasHost ? "방을 닫았습니다." : "방에서 나갔습니다.";
            else if (!string.IsNullOrEmpty(_pendingDisconnectReason))
                reason = _pendingDisconnectReason;
            else if (previous == SessionConnectionState.Connecting)
                reason = "연결하지 못했습니다(주소·방화벽 확인).";
            else
                reason = "호스트와 연결이 끊겼습니다.";

            _connector = null;
            _stateSync = null;
            _leaveRequested = false;
            LastDisconnectReason = reason;

            // 진행 중에 끊기면 모두 종료 화면으로 간다(호스트 종료 = 게임 종료).
            if (GameState == GameSessionState.Playing)
                SetGameState(GameSessionState.Ended);

            SetState(SessionConnectionState.Offline);
            Debug.Log($"[Session] 접속 종료: {reason}");
            Disconnected?.Invoke(reason);
        }

        // ── 상태 변경 알림 ────────────────────────────────────

        void SetState(SessionConnectionState state)
        {
            if (ConnectionState == state)
                return;

            ConnectionState = state;
            Debug.Log($"[Session] 접속 상태: {state}");
            ConnectionStateChanged?.Invoke(state);
        }

        void SetGameState(GameSessionState state)
        {
            if (GameState == state)
                return;

            GameState = state;
            Debug.Log($"[Session] 게임 상태: {state}");
            GameStateChanged?.Invoke(state);
        }

        void SetPlayerCount(int count)
        {
            if (PlayerCount == count)
                return;

            PlayerCount = count;
            PlayerCountChanged?.Invoke(count);
        }
    }
}
