namespace CurtainCall.Player
{
    /// <summary>
    /// 한 프레임 동안 플레이어가 내린 명령. 어떤 키를 눌렀는지가 아니라 무엇을 하려는지를 담는다.
    /// 외줄·목마·온라인 기능은 키 대신 이 값을 읽는다.
    /// </summary>
    public struct PlayerCommand
    {
        /// <summary>코스 진행 방향 기준 이동. +1 전진(W), -1 후진(S), 0 정지.</summary>
        public float Move;
    }
}
