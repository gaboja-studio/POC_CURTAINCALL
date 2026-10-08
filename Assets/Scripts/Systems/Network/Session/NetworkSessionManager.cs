using System;
using System.Threading.Tasks;
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

        /// <summary>게임을 시작할 수 있는 최소 인원(호스트 포함).</summary>
        public int MinPlayers => GameSettings.Base.Session.MinPlayers;

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

            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = ApproveConnection;
            _networkManager.OnConnectionEvent += HandleConnectionEvent;
            _networkManager.OnClientStopped += HandleStopped;
            _networkManager.OnServerStopped += HandleStopped;
        }

        void OnDestroy()
        {
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

        public async Task<bool> HostAsync(ISessionConnector connector)
        {
            if (!BeginConnect(connector))
                return false;

            if (!await RunConnectAsync(connector.HostAsync, "방을 열지 못했습니다."))
                return false;

            SetState(SessionConnectionState.Host);
            SpawnStateSync();
            ServerRefresh();
            return true;
        }

        public async Task<bool> JoinAsync(ISessionConnector connector, string joinKey)
        {
            if (!BeginConnect(connector))
                return false;

            // 실제 접속 완료는 HandleConnectionEvent에서 Client로 바뀐다.
            return await RunConnectAsync(() => connector.JoinAsync(joinKey), "방에 들어가지 못했습니다.");
        }

        public async Task LeaveAsync()
        {
            if (_connector == null)
                return;

            _leaveRequested = true;
            await _connector.LeaveAsync();
        }

        async Task<bool> RunConnectAsync(Func<Task<bool>> connect, string failReason)
        {
            string reason = failReason;
            try
            {
                if (await connect())
                    return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] {failReason} {e}");
                reason = $"{failReason} ({e.Message})";
            }

            // 먼저 Offline으로 바꿔 Shutdown이 부르는 HandleStopped가 종료를 두 번 알리지 않게 한다.
            FailConnect(reason);
            if (_networkManager.IsListening)
                _networkManager.Shutdown();
            return false;
        }

        bool BeginConnect(ISessionConnector connector)
        {
            if (ConnectionState != SessionConnectionState.Offline)
            {
                Debug.LogWarning($"[Session] 이미 {ConnectionState} 상태입니다. 먼저 나가야 합니다.");
                return false;
            }

            _connector = connector;
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

        /// <summary>시작 후 세션을 잠가 방 코드로 더 들어오지 못하게 한다(접속 승인과 이중 방어).</summary>
        async Task LockSessionAsync()
        {
            if (!(_connector is ServicesSessionConnector services) || !(services.CurrentSession is IHostSession host))
                return;

            try
            {
                host.IsLocked = true;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] 세션 잠금 실패(접속 승인으로 막습니다): {e.Message}");
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
            if (ConnectionState == SessionConnectionState.Offline)
                return;

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
