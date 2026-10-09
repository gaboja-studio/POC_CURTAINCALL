using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.LevelPreview
{
    /// <summary>
    /// 맵 배치 확인용 조작 규칙(2026-10-09 PM @MoHoDu). 이동(W/S)·자세 제어(A/D) → 바라보는 방향 기준 8방향 이동, 기본 액션(Space) → 제자리 점프.
    /// 바라보는 방향은 <see cref="LevelPreviewCamera"/>가 정한다. 맵 배치 씬(Assets/Scenes/Levels/)에서 공간을 둘러보는 용도이며 게임 조작이 아니다.
    /// <see cref="LevelPreviewCamera"/>가 시작할 때 플레이어에 끼운다.
    /// </summary>
    public sealed class LevelPreviewControlScheme : PlayerControlScheme
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
            var mover = context.Mover;
            if (mover == null) return;

            Vector3 forward = LevelPreviewCamera.ViewForward;
            if (forward == Vector3.zero)
            {
                mover.SetMoveInput(input.Posture, input.Move);
            }
            else
            {
                // PlayerMover는 코스 기준 축(옆·앞)으로 입력을 받으므로, 바라보는 방향 기준 입력을 그 축으로 바꿔 넘긴다.
                Vector3 wish = forward * input.Move + Vector3.Cross(Vector3.up, forward) * input.Posture;
                mover.SetMoveInput(Vector3.Dot(wish, mover.CourseRight), Vector3.Dot(wish, mover.CourseForward));
            }

            if (input.ActionPressed) mover.RequestJump();
        }
    }
}
