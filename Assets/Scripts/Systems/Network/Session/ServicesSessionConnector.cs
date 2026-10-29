using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>Unity Multiplayer Services 세션(Relay). 취소된 요청은 NGO를 시작하거나 새 연결을 종료할 수 없다.</summary>
    public sealed class ServicesSessionConnector : ISessionConnector
    {
        public const int DefaultMaxPlayers = 4;
        // SDK HTTP에는 취소 토큰이 없다. 오래된 SDK 작업/세션 정리와 다음 작업을 겹치지 않는다.
        static readonly SemaphoreSlim Operations = new(1, 1);
        readonly NetworkManager network;
        readonly int maxPlayers;
        readonly bool isPrivate;
        readonly CancellationTokenSource cancellation = new();
        readonly SessionNetworkHandler handler;
        public ISession CurrentSession { get; private set; }
        public string JoinKey => CurrentSession?.Code;

        public ServicesSessionConnector(NetworkManager networkManager, int maxPlayers = DefaultMaxPlayers, bool isPrivate = true)
        {
            network = networkManager;
            this.maxPlayers = maxPlayers;
            this.isPrivate = isPrivate;
            handler = new SessionNetworkHandler(networkManager, cancellation.Token);
        }

        public Task<bool> HostAsync() => ConnectAsync(null, true);

        public Task<bool> JoinAsync(string joinKey)
        {
            string code = joinKey?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("방 코드를 입력하세요.");
            return ConnectAsync(code, false);
        }

        async Task<bool> ConnectAsync(string code, bool host)
        {
            await Operations.WaitAsync(cancellation.Token);
            try
            {
                await EnsureSignedInAsync();
                cancellation.Token.ThrowIfCancellationRequested();
                ISession session;
                if (host)
                {
                    var options = new SessionOptions { MaxPlayers = maxPlayers, IsPrivate = isPrivate }
                        .WithRelayNetwork().WithNetworkHandler(handler);
                    session = await MultiplayerService.Instance.CreateSessionAsync(options);
                }
                else
                {
                    var options = new JoinSessionOptions().WithNetworkHandler(handler);
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
                }
                if (cancellation.IsCancellationRequested)
                {
                    await LeaveSessionAsync(session);
                    return false;
                }
                CurrentSession = session;
                return true;
            }
            finally { Operations.Release(); }
        }

        public async Task LeaveAsync()
        {
            cancellation.Cancel();
            var session = CurrentSession;
            CurrentSession = null;
            // NGO만 먼저 확실하게 종료한다. 원격 나가기 응답을 기다리며 UI를 계속 잠그지 않는다.
            await handler.StopAsync();
            if (session != null) _ = CleanupSessionAsync(session);
        }

        static async Task CleanupSessionAsync(ISession session)
        {
            await Operations.WaitAsync();
            try { await LeaveSessionAsync(session); }
            finally { Operations.Release(); }
        }

        static async Task LeaveSessionAsync(ISession session)
        {
            try { await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning($"[Session] 원격 세션 정리 실패: {e.Message}"); }
        }

        // 세션별 소유권으로 Stop을 제한한다. 늦은 SDK 정리가 새 시도의 NGO를 끄지 못한다.
        sealed class SessionNetworkHandler : INetworkHandler
        {
            readonly NetworkManager network;
            readonly CancellationToken cancellation;
            bool ownsNetwork;
            public SessionNetworkHandler(NetworkManager network, CancellationToken cancellation)
            { this.network = network; this.cancellation = cancellation; }

            public async Task StartAsync(NetworkConfiguration configuration)
            {
                cancellation.ThrowIfCancellationRequested();
                if (network == null || network.IsListening || network.ShutdownInProgress)
                    throw new InvalidOperationException("네트워크가 다른 연결을 처리 중입니다.");
                if (configuration.Type != NetworkType.Relay)
                    throw new InvalidOperationException("Relay 연결만 지원합니다.");
                var transport = network.NetworkConfig.NetworkTransport as UnityTransport;
                if (transport == null) throw new InvalidOperationException("UnityTransport가 없습니다.");
                transport.SetRelayServerData(configuration.RelayServerData);
                bool client = configuration.Role == NetworkRole.Client;
                ownsNetwork = client ? network.StartClient() : network.StartHost();
                if (!ownsNetwork) throw new InvalidOperationException("네트워크를 시작하지 못했습니다.");
                while (client && !network.IsConnectedClient)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!ownsNetwork || !network.IsListening)
                        throw new InvalidOperationException("연결이 종료되었습니다.");
                    await Task.Delay(10);
                }
                cancellation.ThrowIfCancellationRequested();
            }

            public async Task StopAsync()
            {
                if (!ownsNetwork) return;
                ownsNetwork = false;
                if (network == null) return;
                network.Shutdown();
                while (network != null && network.ShutdownInProgress) await Task.Delay(10);
            }
        }

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions().SetProfile(CreateProfileName());
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string CreateProfileName() => "p" + Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
