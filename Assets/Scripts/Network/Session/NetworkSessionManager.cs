using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 방 접속의 공개 진입점. 다른 기능은 이 클래스의 메서드·이벤트만 사용한다.
    /// NetworkManager 프리팹(Resources/Prefabs/Controllers/Network/NetworkManager)에 함께 붙는다.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkSessionManager : MonoBehaviour
    {
        const string PrefabPath = "Prefabs/Controllers/Network/NetworkManager";

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

        /// <summary>참가자에게 알려 줄 접속 키(LAN은 "IP:포트"). 접속 전에는 null.</summary>
        public string JoinKey => _connector?.JoinKey;

        /// <summary>마지막으로 접속이 끝난 이유. 접속 중에는 null.</summary>
        public string LastDisconnectReason { get; private set; }

        /// <summary>현재 접속 인원. 호스트에서만 정확하다(클라이언트 공유는 게임 상태 동기화에서 한다).</summary>
        public int ConnectedPlayerCount => _networkManager != null && _networkManager.IsServer
            ? _networkManager.ConnectedClientsIds.Count
            : 0;

        public NetworkManager NetworkManager => _networkManager;

        public event Action<SessionConnectionState> ConnectionStateChanged;

        /// <summary>호스트에서 접속 인원이 바뀔 때.</summary>
        public event Action<int> PlayerCountChanged;

        /// <summary>내 접속이 끝났을 때(직접 나감·호스트 종료·연결 실패). 인자는 이유.</summary>
        public event Action<string> Disconnected;

        NetworkManager _networkManager;
        ISessionConnector _connector;
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

        public Task<bool> HostLanAsync(ushort port = LanSessionConnector.DefaultPort) =>
            HostAsync(new LanSessionConnector(_networkManager, port));

        /// <param name="address">"IP" 또는 "IP:포트"</param>
        public Task<bool> JoinLanAsync(string address, ushort defaultPort = LanSessionConnector.DefaultPort) =>
            JoinAsync(new LanSessionConnector(_networkManager, defaultPort), address);

        public async Task<bool> HostAsync(ISessionConnector connector)
        {
            if (!BeginConnect(connector))
                return false;

            bool started = await connector.HostAsync();
            if (!started)
            {
                FailConnect("방을 열지 못했습니다.");
                return false;
            }

            SetState(SessionConnectionState.Host);
            PlayerCountChanged?.Invoke(ConnectedPlayerCount);
            return true;
        }

        public async Task<bool> JoinAsync(ISessionConnector connector, string joinKey)
        {
            if (!BeginConnect(connector))
                return false;

            bool started = await connector.JoinAsync(joinKey);
            if (!started)
            {
                FailConnect("방에 들어가지 못했습니다.");
                return false;
            }

            // 실제 접속 완료는 HandleConnectionEvent에서 Client로 바뀐다.
            return true;
        }

        public async Task LeaveAsync()
        {
            if (_connector == null)
                return;

            _leaveRequested = true;
            await _connector.LeaveAsync();
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

        void HandleConnectionEvent(NetworkManager networkManager, ConnectionEventData data)
        {
            bool isLocal = data.ClientId == networkManager.LocalClientId;

            switch (data.EventType)
            {
                case ConnectionEvent.ClientConnected:
                    if (networkManager.IsServer)
                        PlayerCountChanged?.Invoke(ConnectedPlayerCount);
                    else if (isLocal)
                        SetState(SessionConnectionState.Client);
                    break;

                case ConnectionEvent.ClientDisconnected:
                    if (networkManager.IsServer)
                        PlayerCountChanged?.Invoke(ConnectedPlayerCount);
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
            _leaveRequested = false;
            LastDisconnectReason = reason;
            SetState(SessionConnectionState.Offline);
            Debug.Log($"[Session] 접속 종료: {reason}");
            Disconnected?.Invoke(reason);
        }

        void SetState(SessionConnectionState state)
        {
            if (ConnectionState == state)
                return;

            ConnectionState = state;
            Debug.Log($"[Session] 접속 상태: {state}");
            ConnectionStateChanged?.Invoke(state);
        }
    }
}
