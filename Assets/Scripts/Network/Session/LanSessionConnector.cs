using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 같은 네트워크 안에서 IP로 직접 연결한다. 세션 서비스가 준비되기 전 테스트용 경로.
    /// </summary>
    public sealed class LanSessionConnector : ISessionConnector
    {
        public const ushort DefaultPort = 7777;

        const string ListenAllAddress = "0.0.0.0";
        const string LoopbackAddress = "127.0.0.1";

        readonly NetworkManager _networkManager;
        readonly ushort _port;

        public string JoinKey { get; private set; }

        public LanSessionConnector(NetworkManager networkManager, ushort port = DefaultPort)
        {
            _networkManager = networkManager;
            _port = port;
        }

        public Task<bool> HostAsync()
        {
            var transport = GetTransport();
            if (transport == null)
                return Task.FromResult(false);

            transport.SetConnectionData(LoopbackAddress, _port, ListenAllAddress);
            bool started = _networkManager.StartHost();
            JoinKey = started ? $"{GetLocalIPv4()}:{_port}" : null;
            return Task.FromResult(started);
        }

        public Task<bool> JoinAsync(string joinKey)
        {
            if (!TryParseJoinKey(joinKey, _port, out string address, out ushort port))
            {
                Debug.LogWarning($"[Session] 잘못된 주소입니다: '{joinKey}' (예: 192.168.0.10:7777)");
                return Task.FromResult(false);
            }

            var transport = GetTransport();
            if (transport == null)
                return Task.FromResult(false);

            transport.SetConnectionData(address, port);
            bool started = _networkManager.StartClient();
            JoinKey = started ? $"{address}:{port}" : null;
            return Task.FromResult(started);
        }

        public Task LeaveAsync()
        {
            _networkManager.Shutdown();
            JoinKey = null;
            return Task.CompletedTask;
        }

        UnityTransport GetTransport()
        {
            var transport = _networkManager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null)
                Debug.LogError("[Session] NetworkManager에 UnityTransport가 설정되어 있지 않습니다.");
            return transport;
        }

        /// <summary>"IP" 또는 "IP:포트"를 읽는다. 포트가 없으면 기본 포트를 쓴다.</summary>
        public static bool TryParseJoinKey(string joinKey, ushort defaultPort, out string address, out ushort port)
        {
            address = null;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(joinKey))
                return false;

            string text = joinKey.Trim();
            int colon = text.LastIndexOf(':');
            if (colon >= 0)
            {
                if (!ushort.TryParse(text.Substring(colon + 1), out port) || port == 0)
                    return false;
                text = text.Substring(0, colon);
            }

            if (!IPAddress.TryParse(text, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                return false;

            address = ip.ToString();
            return true;
        }

        /// <summary>참가자에게 알려 줄 이 컴퓨터의 IPv4 주소. 찾지 못하면 루프백.</summary>
        static string GetLocalIPv4()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                            return unicast.Address.ToString();
                    }
                }
            }
            catch (NetworkInformationException e)
            {
                Debug.LogWarning($"[Session] 로컬 IP를 찾지 못했습니다: {e.Message}");
            }

            return LoopbackAddress;
        }
    }
}
