using CurtainCall.Network.Session;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 시작 씬 → 로비 씬 → (게임 시작) 런 → 로비 씬의 큰 흐름(Task-052). 씬이 바뀌어도 남는다.
    /// 로비 씬 이름은 장소 전환 세팅의 시작 씬(<see cref="BaseGameSettings.SpaceTransitionSettings.HubScene"/>)을 그대로 쓴다.
    /// 접속이 끊기면(방 나가기·호스트 종료) 어디에 있든 시작 씬으로 돌아가고, 이유는 <see cref="LastMessage"/>로 남긴다.
    /// </summary>
    public sealed class StartLobbyFlow : MonoBehaviour
    {
        public const string StartScene = "Start";

        public static StartLobbyFlow Instance { get; private set; }

        /// <summary>없으면 만들어 돌려준다(씬이 바뀌어도 유지). 저장한 화면 설정도 이때 적용한다.</summary>
        public static StartLobbyFlow EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(StartLobbyFlow));
            DontDestroyOnLoad(go);
            return go.AddComponent<StartLobbyFlow>();
        }

        public static string LobbyScene => GameSettings.Base.SpaceTransition.HubScene;

        /// <summary>시작 씬에 보여 줄 마지막 알림(방에서 나온 이유 등). 읽으면 지운다.</summary>
        public string LastMessage { get; private set; }

        NetworkSessionManager session;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ClientSettings.ApplySaved();
        }

        void Start()
        {
            session = NetworkSessionManager.EnsureExists();
            if (session != null) session.Disconnected += HandleDisconnected;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (session != null) session.Disconnected -= HandleDisconnected;
            Instance = null;
        }

        public string TakeMessage()
        {
            string message = LastMessage;
            LastMessage = null;
            return message;
        }

        /// <summary>호스트가 방을 연 직후: 모두 함께 로비로 간다(뒤에 들어오는 사람도 NGO가 같은 씬으로 맞춘다).</summary>
        public bool OpenLobbyAsHost()
        {
            var network = session != null ? session.NetworkManager : null;
            if (network == null || !network.IsServer || network.SceneManager == null) return false;
            var status = network.SceneManager.LoadScene(LobbyScene, LoadSceneMode.Single);
            if (status == Unity.Netcode.SceneEventProgressStatus.Started) return true;
            Debug.LogError($"[StartLobby] 로비 씬({LobbyScene})을 열지 못했습니다: {status}. 빌드 씬 목록을 확인하세요.");
            return false;
        }

        /// <summary>방에서 나와 시작 씬으로 돌아간다.</summary>
        public void LeaveToStart()
        {
            if (session != null && session.ConnectionState != SessionConnectionState.Offline) _ = session.LeaveAsync();
            else LoadStart();
        }

        void HandleDisconnected(string reason)
        {
            LastMessage = reason;
            LoadStart();
        }

        static void LoadStart()
        {
            if (SceneManager.GetActiveScene().name != StartScene) SceneManager.LoadScene(StartScene, LoadSceneMode.Single);
        }
    }
}
