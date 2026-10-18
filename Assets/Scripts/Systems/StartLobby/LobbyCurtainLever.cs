using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using UnityEngine;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 로비 무대 옆 커튼 레버(방장 전용). 모두 출연 명단에 서명하고 최소 인원이 모이면 당겨서 게임을 시작한다.
    /// 런이 끝나 로비로 돌아온 뒤(게임 종료 상태)에는 당기면 같은 방으로 다시 준비한다(<see cref="NetworkSessionManager.ReturnToLobby"/>).
    /// 레버 손잡이는 진행 중(도입~런)에 당겨진 채로 보인다.
    /// </summary>
    public sealed class LobbyCurtainLever : LobbyInteractable
    {
        [SerializeField] LobbyReadyBoard board;
        [Tooltip("당기면 X축으로 기우는 손잡이.")]
        [SerializeField] Transform handle;
        [SerializeField] float pulledAngle = 50f;

        NetworkSessionManager session;
        float restAngle;
        float angle;

        void Start()
        {
            session = NetworkSessionManager.EnsureExists();
            if (handle != null) restAngle = handle.localEulerAngles.x;
        }

        void Update()
        {
            bool pulled = session != null && session.GameState == GameSessionState.Playing;
            angle = Mathf.MoveTowards(angle, pulled ? pulledAngle : 0f, 160f * Time.deltaTime);
            if (handle != null)
            {
                var e = handle.localEulerAngles;
                handle.localEulerAngles = new Vector3(restAngle + angle, e.y, e.z);
            }
        }

        public override string Prompt(out bool usable)
        {
            usable = false;
            if (session == null || session.ConnectionState == SessionConnectionState.Offline) return "커튼 레버";
            switch (session.GameState)
            {
                case GameSessionState.Playing:
                    return "공연 중";
                case GameSessionState.Ended:
                    if (!session.IsHost) return "방장이 레버로 다시 준비할 수 있어요";
                    usable = true;
                    return "[F] 커튼 레버 올리기 (다시 준비)";
            }
            if (!session.IsHost) return "방장만 커튼 레버를 당길 수 있어요";
            if (session.PlayerCount < session.MinPlayers) return $"단원이 {session.MinPlayers}명 이상 모여야 해요";
            if (board != null && !board.AllReady) return $"모두 출연 명단에 서명해야 해요 ({board.ReadyCount}/{session.PlayerCount})";
            usable = session.CanStartGame;
            return usable ? "[F] 커튼 레버 당기기 (공연 시작)" : "커튼 레버";
        }

        public override void Use()
        {
            if (session == null || !session.IsHost) return;
            if (session.GameState == GameSessionState.Ended)
            {
                if (!session.ReturnToLobby()) return;
                NetworkPlayer.ServerRestoreEveryone(); // 새 런은 온전한 몸으로
                return;
            }
            if (board != null && !board.AllReady) return;
            if (session.StartGame()) Debug.Log("[StartLobby] 커튼 레버 → 게임 시작");
        }
    }
}
