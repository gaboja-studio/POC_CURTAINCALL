using System;

namespace CurtainCall.Player
{
    /// <summary>
    /// 데미지로 잃을 수 있는 신체 부위(사지). 여러 부위를 함께 담을 수 있다(비트 플래그).
    /// 온라인으로 숫자를 보내므로 값은 바꾸지 않고 끝에만 추가한다.
    /// </summary>
    [Flags]
    public enum BodyPart
    {
        None = 0,
        LeftArm = 1 << 0,
        RightArm = 1 << 1,
        LeftLeg = 1 << 2,
        RightLeg = 1 << 3,
    }
}
