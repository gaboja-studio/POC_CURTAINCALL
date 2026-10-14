using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 기본 이동 규칙(2026-10-14 PM, 외줄 플랫폼 이동에서 공용으로 옮김). 이동(W/S) → 앞뒤, 자세 제어(A/D) → 옆으로 8방향 일반 이동(이동 방향을 바라봄),
    /// 기본 액션(Space) → 제자리 점프. 좌우 보조 방향(Q/E)·상호작용은 동작 없음.
    /// 조작 규칙을 끼우지 않은 공간은 <see cref="ControlSettings"/>의 기본 이동 규칙을 쓰고, 비어 있으면 이 규칙을 쓴다.
    /// 외줄 코스는 플랫폼에서 이 규칙, 줄 위에서 외줄 규칙으로 바꾼다(공연장 <c>CourseControlSwitcher</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Platform (기본 이동)", fileName = "PlatformControlScheme")]
    public class PlatformControlScheme : PlayerControlScheme
    {
        public override void Enter(PlayerControlContext context)
        {
            context.HeldDirection = 0;
            if (context.Mover != null) context.Mover.FreeMovement = true;
        }

        public override void Exit(PlayerControlContext context)
        {
            if (context.Mover != null) context.Mover.FreeMovement = false;
        }

        public override void Tick(PlayerControlContext context, in PlayerInputFrame input)
        {
            if (context.Mover == null) return;
            context.Mover.SetMoveInput(input.Posture, input.Move);
            if (input.ActionPressed) context.Mover.RequestJump();
        }
    }
}
