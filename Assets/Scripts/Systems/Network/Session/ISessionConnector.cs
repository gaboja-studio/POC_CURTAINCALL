using System.Threading.Tasks;

namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 방을 열고 들어가는 방식 하나. LAN(직접 IP)과 Multiplayer Services(방 코드)가 각각 구현한다.
    /// 방 목록 검색(로비)이 필요해지면 이 인터페이스를 바꾸지 않고 별도 인터페이스로 추가한다.
    /// </summary>
    public interface ISessionConnector
    {
        /// <summary>참가자에게 알려 줄 접속 키. LAN은 "IP:포트", 세션은 방 코드. 접속 전에는 null.</summary>
        string JoinKey { get; }

        Task<bool> HostAsync();

        Task<bool> JoinAsync(string joinKey);

        Task LeaveAsync();
    }
}
