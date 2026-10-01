using System;
using Unity.Netcode;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 게임 상태와 접속 인원을 모두에게 복제한다. 호스트가 접속 직후 스폰한다.
    /// 다른 기능은 이 컴포넌트가 아니라 <see cref="NetworkSessionManager"/>의 이벤트를 쓴다.
    /// </summary>
    public sealed class GameSessionStateSync : NetworkBehaviour
    {
        readonly NetworkVariable<GameSessionState> _state = new(GameSessionState.Waiting);
        readonly NetworkVariable<int> _playerCount = new();

        public GameSessionState State => _state.Value;
        public int PlayerCount => _playerCount.Value;

        internal event Action Changed;

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += HandleStateChanged;
            _playerCount.OnValueChanged += HandleCountChanged;
            NetworkSessionManager.Instance?.AttachStateSync(this);
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;
            _playerCount.OnValueChanged -= HandleCountChanged;
            NetworkSessionManager.Instance?.DetachStateSync(this);
        }

        internal void ServerSet(GameSessionState state, int playerCount)
        {
            if (!IsServer)
                return;

            _state.Value = state;
            _playerCount.Value = playerCount;
        }

        void HandleStateChanged(GameSessionState previous, GameSessionState current) => Changed?.Invoke();

        void HandleCountChanged(int previous, int current) => Changed?.Invoke();
    }
}
