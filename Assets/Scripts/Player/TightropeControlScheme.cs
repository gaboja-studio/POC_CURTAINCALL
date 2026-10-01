using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 외줄타기 조작 규칙. 이동 → 코스 앞뒤, 자세 제어 → 균형 보정, 기본 액션 → 제자리 점프.
    /// 규칙 근거: Docs/References/tightrope-keymap-summary.md, Harness/Project/Decisions/tightrope-rules.md
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Tightrope", fileName = "TightropeControlScheme")]
    public class TightropeControlScheme : PlayerControlScheme
    {
        public override void Tick(PlayerControlContext context, in PlayerInputFrame input)
        {
            if (context.Mover != null)
            {
                context.Mover.SetMoveInput(input.Move);
                if (input.ActionPressed) context.Mover.RequestJump();
            }

            if (context.Balance != null)
                context.Balance.SetCorrectionInput(input.Posture);
        }
    }
}
