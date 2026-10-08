using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 외줄 코스의 플랫폼(시작·도착) 조작 규칙. 이동(W/S) → 코스 앞뒤, 자세 제어(A/D) → 코스 옆으로 8방향 일반 이동(이동 방향을 바라봄),
    /// 기본 액션(Space) → 제자리 점프. 좌우 보조 방향(Q/E)+Space·상호작용은 동작 없음.
    /// 줄 위에서는 <see cref="TightropeControlScheme"/>로 바뀐다(<see cref="CourseControlSwitcher"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Tightrope Platform", fileName = "PlatformControlScheme")]
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
