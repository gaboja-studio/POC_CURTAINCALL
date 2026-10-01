using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 외줄타기 조작 규칙. 이동 → 코스 앞뒤, 자세 제어 → 균형 보정, 기본 액션 → 제자리 점프,
    /// 좌우 보조 방향(Q/E) → 누르는 동안만 옆줄 이동 방향 지정(단독으로는 동작 없음),
    /// 방향을 누른 채 기본 액션 → 옆줄 점프(제자리 점프 대신). 방향은 뛴 순간 고정되고 착지까지 바뀌지 않는다.
    /// 상호작용 짧게 → 목마 연결 시도, 길게 → 목마 해제(실제 처리는 목마 기능).
    /// 규칙 근거: Docs/References/tightrope-keymap-summary.md, Harness/Project/Decisions/tightrope-rules.md
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Tightrope", fileName = "TightropeControlScheme")]
    public class TightropeControlScheme : PlayerControlScheme
    {
        public override void Exit(PlayerControlContext context) => context.HeldDirection = 0;

        public override void Tick(PlayerControlContext context, in PlayerInputFrame input)
        {
            context.HeldDirection = input.AuxDirection;

            if (context.Mover != null)
            {
                context.Mover.SetMoveInput(input.Move);
                if (input.ActionPressed)
                {
                    if (input.AuxDirection != 0) context.Mover.RequestLaneJump(input.AuxDirection);
                    else context.Mover.RequestJump();
                }
            }

            if (context.Balance != null)
                context.Balance.SetCorrectionInput(input.Posture);

            if (context.Interaction != null)
            {
                if (input.InteractTapped) context.Interaction.RequestInteract();
                if (input.InteractLongPressed) context.Interaction.RequestRelease();
            }
        }
    }
}
