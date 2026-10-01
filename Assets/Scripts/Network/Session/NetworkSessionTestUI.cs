using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 테스트 씬 전용 임시 화면. 진짜 로비 UI가 생기면 지운다.
    /// </summary>
    public sealed class NetworkSessionTestUI : MonoBehaviour
    {
        [SerializeField] string _address = "127.0.0.1:7777";
        [SerializeField] Rect _panelRect = new Rect(10, 10, 340, 320);

        string _code = "";
        bool _useLan;

        NetworkSessionManager _session;

        void Start()
        {
            _session = NetworkSessionManager.EnsureExists();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(_panelRect, GUI.skin.box);

            if (_session == null)
            {
                GUILayout.Label("NetworkManager 프리팹을 찾지 못했습니다.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"접속: {_session.ConnectionState}");

            switch (_session.ConnectionState)
            {
                case SessionConnectionState.Offline:
                    DrawOffline();
                    break;
                case SessionConnectionState.Connecting:
                    GUILayout.Label("연결 중...");
                    break;
                default:
                    DrawConnected();
                    break;
            }

            GUILayout.EndArea();
        }

        void DrawOffline()
        {
            if (_session.GameState == GameSessionState.Ended)
                GUILayout.Label("== 게임 종료 ==");

            _useLan = GUILayout.Toolbar(_useLan ? 1 : 0, new[] { "방 코드(세션)", "LAN(직접 IP)" }) == 1;

            if (_useLan)
                DrawLan();
            else
                DrawSession();

            if (!string.IsNullOrEmpty(_session.LastDisconnectReason))
                GUILayout.Label($"마지막 종료: {_session.LastDisconnectReason}");
        }

        void DrawSession()
        {
            if (GUILayout.Button("방 만들기"))
                _ = _session.HostSessionAsync();

            GUILayout.Label("방 코드");
            _code = GUILayout.TextField(_code);
            if (GUILayout.Button("참가"))
                _ = _session.JoinSessionAsync(_code);
        }

        void DrawLan()
        {
            if (GUILayout.Button("방 만들기 (LAN Host)"))
                _ = _session.HostLanAsync();

            GUILayout.Label("주소 (IP:포트)");
            _address = GUILayout.TextField(_address);
            if (GUILayout.Button("참가 (LAN Client)"))
                _ = _session.JoinLanAsync(_address);
        }

        void DrawConnected()
        {
            bool isSession = _session.ActiveConnector is ServicesSessionConnector;
            GUILayout.Label($"{(isSession ? "방 코드" : "접속 주소")}: {_session.JoinKey}");
            if (isSession && GUILayout.Button("방 코드 복사"))
                GUIUtility.systemCopyBuffer = _session.JoinKey;

            GUILayout.Label($"게임 상태: {GameStateLabel(_session.GameState)}");
            GUILayout.Label($"접속 인원: {_session.PlayerCount} / {_session.RequiredPlayers}");

            if (_session.IsHost && _session.GameState == GameSessionState.Playing &&
                GUILayout.Button("게임 종료 (호스트 판정 테스트)"))
                _session.EndGame();

            if (GUILayout.Button(_session.IsHost ? "방 닫기" : "나가기"))
                _ = _session.LeaveAsync();
        }

        static string GameStateLabel(GameSessionState state)
        {
            switch (state)
            {
                case GameSessionState.Waiting: return "대기";
                case GameSessionState.Playing: return "진행";
                default: return "종료";
            }
        }
    }
}
