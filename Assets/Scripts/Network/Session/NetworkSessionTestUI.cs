using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 테스트 씬 전용 임시 화면. 진짜 로비 UI가 생기면 지운다.
    /// </summary>
    public sealed class NetworkSessionTestUI : MonoBehaviour
    {
        [SerializeField] string _address = "127.0.0.1:7777";
        [SerializeField] Rect _area = new Rect(10, 10, 320, 220);

        NetworkSessionManager _session;

        void Start()
        {
            _session = NetworkSessionManager.EnsureExists();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(_area, GUI.skin.box);

            if (_session == null)
            {
                GUILayout.Label("NetworkManager 프리팹을 찾지 못했습니다.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"상태: {_session.ConnectionState}");

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
            if (GUILayout.Button("방 만들기 (LAN Host)"))
                _ = _session.HostLanAsync();

            GUILayout.Label("주소 (IP:포트)");
            _address = GUILayout.TextField(_address);
            if (GUILayout.Button("참가 (LAN Client)"))
                _ = _session.JoinLanAsync(_address);

            if (!string.IsNullOrEmpty(_session.LastDisconnectReason))
                GUILayout.Label($"마지막 종료: {_session.LastDisconnectReason}");
        }

        void DrawConnected()
        {
            GUILayout.Label($"접속 주소: {_session.JoinKey}");
            if (_session.ConnectionState == SessionConnectionState.Host)
                GUILayout.Label($"접속 인원: {_session.ConnectedPlayerCount}");

            if (GUILayout.Button(_session.ConnectionState == SessionConnectionState.Host ? "방 닫기" : "나가기"))
                _ = _session.LeaveAsync();
        }
    }
}
