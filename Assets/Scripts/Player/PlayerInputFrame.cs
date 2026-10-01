namespace CurtainCall.Player
{
    /// <summary>
    /// 한 프레임의 입력을 키맵핑 문서의 "공통 역할" 이름으로 담는다. 어떤 키인지도, 무슨 동작을 할지도 담지 않는다.
    /// 키 → 역할 연결은 입력 에셋(Resources/Input/PlayerControls), 역할 → 동작 해석은 조작 규칙(<see cref="PlayerControlScheme"/>)이 맡는다.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>이동 축. +1 앞(기본 W), -1 뒤(기본 S), 0 없음.</summary>
        public float Move;

        /// <summary>자세 제어 축. -1 왼쪽(기본 A), +1 오른쪽(기본 D), 0 없음.</summary>
        public float Posture;

        /// <summary>기본 액션(기본 Space)을 이번 프레임에 눌렀는지.</summary>
        public bool ActionPressed;
    }
}
