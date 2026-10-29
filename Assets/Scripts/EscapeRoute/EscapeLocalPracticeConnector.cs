using System;
using System.Threading.Tasks;
using CurtainCall.Network.Session;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace CurtainCall.EscapeRoute
{
    /// <summary>원본 NetworkPlayer를 위한 로컬 실행. 방 코드·외부 참가·온라인 서비스를 사용하지 않는다.</summary>
    public sealed class EscapeLocalPracticeConnector : ISessionConnector
    {
        readonly NetworkManager manager;
        bool attached;
        Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> previousApproval;
        public string JoinKey => null;

        public EscapeLocalPracticeConnector(NetworkManager manager) => this.manager = manager;

        public Task<bool> HostAsync()
        {
            if (manager == null || manager.IsListening
                || manager.NetworkConfig.NetworkTransport is not UnityTransport transport)
                return Task.FromResult(false);
            // 포트 0은 OS의 빈 포트. 명령행 LAN 설정도 이 개발씬에 영향을 주지 않는다.
            transport.SetConnectionData(true, "127.0.0.1", 0, "127.0.0.1");
            // NGO는 승인 콜백 하나만 허용하므로 원본 승인을 감싸고 종료 때 복원한다.
            previousApproval = manager.ConnectionApprovalCallback;
            manager.ConnectionApprovalCallback = ApproveLocal;
            attached = true;
            return Task.FromResult(manager.StartHost());
        }

        void ApproveLocal(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            { previousApproval?.Invoke(request, response); return; }
            response.Approved = response.CreatePlayerObject = false;
            response.Reason = "Local development practice only";
            response.Pending = false;
        }

        public Task<bool> JoinAsync(string joinKey) => Task.FromResult(false);

        public Task LeaveAsync()
        {
            if (manager != null)
            {
                if (attached && manager.ConnectionApprovalCallback == ApproveLocal)
                    manager.ConnectionApprovalCallback = previousApproval;
                if (manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
            }
            attached = false;
            previousApproval = null;
            return Task.CompletedTask;
        }
    }
}
