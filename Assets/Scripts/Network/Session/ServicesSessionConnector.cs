using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// Unity Multiplayer Services 세션(Relay)으로 방을 열고, 방 고유 코드로 참가한다.
    /// 방 목록 검색(QuerySessionsAsync)은 나중에 별도 인터페이스로 추가한다.
    /// </summary>
    public sealed class ServicesSessionConnector : ISessionConnector
    {
        public const int DefaultMaxPlayers = 4;

        readonly NetworkManager _networkManager;
        readonly int _maxPlayers;
        readonly bool _isPrivate;

        /// <summary>현재 들어가 있는 세션. 플레이어 ID 등 세부 정보가 필요할 때 쓴다.</summary>
        public ISession CurrentSession { get; private set; }

        public string JoinKey => CurrentSession?.Code;

        /// <param name="isPrivate">true면 방 코드로만 들어올 수 있다(목록 검색에 안 보임).</param>
        public ServicesSessionConnector(NetworkManager networkManager, int maxPlayers = DefaultMaxPlayers, bool isPrivate = true)
        {
            _networkManager = networkManager;
            _maxPlayers = maxPlayers;
            _isPrivate = isPrivate;
        }

        public async Task<bool> HostAsync()
        {
            await EnsureSignedInAsync();

            var options = new SessionOptions
            {
                MaxPlayers = _maxPlayers,
                IsPrivate = _isPrivate
            }.WithRelayNetwork();

            CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[Session] 방 생성: 코드 {CurrentSession.Code}, 최대 {CurrentSession.MaxPlayers}명");
            return true;
        }

        public async Task<bool> JoinAsync(string joinKey)
        {
            string code = joinKey?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("방 코드를 입력하세요.");

            await EnsureSignedInAsync();

            CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            Debug.Log($"[Session] 방 참가: 코드 {CurrentSession.Code}");
            return true;
        }

        public async Task LeaveAsync()
        {
            var session = CurrentSession;
            CurrentSession = null;

            try
            {
                if (session != null)
                    await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] 세션 나가기 실패(네트워크는 종료합니다): {e.Message}");
            }

            if (_networkManager.IsListening)
                _networkManager.Shutdown();
        }

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // 같은 PC에서 여러 플레이어(MPPM)를 띄워도 서로 다른 익명 계정이 되도록 실행마다 프로필을 나눈다.
                var options = new InitializationOptions().SetProfile(CreateProfileName());
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string CreateProfileName() => "p" + Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
