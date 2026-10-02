namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 호스트가 확정해 모두에게 공유하는 플레이어 상태. 이후 기능이 상태를 늘린다(예: 대기, 목마 탑승).
    /// 값을 바꿀 때는 기존 숫자를 유지하고 끝에 추가한다(온라인에서 숫자로 보낸다).
    /// </summary>
    public enum PlayerState : byte
    {
        /// <summary>보통(조작 가능).</summary>
        Normal = 0,
        /// <summary>추락(조작 잠김, 래그돌). 모두 재시작까지 유지.</summary>
        Fallen = 1,
    }
}
