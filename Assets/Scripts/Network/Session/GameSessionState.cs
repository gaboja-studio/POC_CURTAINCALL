namespace CurtainCall.Network.Session
{
    /// <summary>
    /// 모든 플레이어가 같게 보는 게임 진행 상태. 호스트만 바꾼다.
    /// </summary>
    public enum GameSessionState : byte
    {
        Waiting,
        Playing,
        Ended
    }
}
