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

        /// <summary>자세(균형) 제어. -1 왼쪽(A), +1 오른쪽(D), 0 입력 없음. 좌우 이동에는 쓰지 않는다.</summary>
        public float Posture;
    }
}
