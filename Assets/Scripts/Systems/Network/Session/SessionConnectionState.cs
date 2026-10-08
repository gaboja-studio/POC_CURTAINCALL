namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 내 컴퓨터의 접속 상태. 게임 진행 상태(대기/진행/종료)와는 별개다.
    /// </summary>
    public enum SessionConnectionState
    {
        Offline,
        Connecting,
        Host,
        Client
    }
}
