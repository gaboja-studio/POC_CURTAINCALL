using System.Runtime.CompilerServices;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Control;
using UnityEngine;

namespace CurtainCall.Player // 에셋 클래스 식별자 유지(Resources/Input/TightropeControlScheme.asset)
{
    /// <summary>
    /// 외줄타기 조작 규칙. 이동 → 코스 앞뒤, 자세 제어 → 균형 보정, 기본 액션 → 제자리 점프,
    /// 좌우 보조 방향(Q/E) → 누르는 동안만 옆줄 이동 방향 지정(단독으로는 동작 없음),
    /// 방향을 누른 채 기본 액션 → 옆줄 점프(제자리 점프 대신). 방향은 뛴 순간 고정되고 착지까지 바뀌지 않는다.
    /// 상호작용 짧게 → 목마 연결 시도, 길게 → 목마 해제(실제 처리는 목마 기능).
    /// 규칙 근거: Docs/References/tightrope-keymap-summary.md, Harness/Project/Decisions/tightrope-rules.md
    /// 줄 위에서 <see cref="CurtainCall.Tightrope.CourseControlSwitcher"/>가 끼운다(공용 기본 규칙이 아니다).
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Tightrope", fileName = "TightropeControlScheme")]
    public class TightropeControlScheme : PlayerControlScheme
    {
        readonly ConditionalWeakTable<PlayerControlContext, TightropeJumpBuffer> jumps = new();

        public override void Enter(PlayerControlContext context)
        {
            ClearBuffer(context);
            ApplyMovementSettings(context);
        }

        static void ApplyMovementSettings(PlayerControlContext context)
        {
            if (context.Mover == null) return;
            context.Mover.CourseSpeedOverride = GameSettings.Tightrope.RopeMovement.MoveSpeed;
            context.Mover.CourseJumpHeightOverride = GameSettings.Tightrope.RopeMovement.JumpHeight;
            context.Mover.CourseJumpGravityMultiplier = GameSettings.Tightrope.RopeMovement.JumpGravityMultiplier;
        }

        void ClearBuffer(PlayerControlContext context)
        {
            if (!jumps.TryGetValue(context, out var buffer)) return;
            if (context.Mover != null) context.Mover.Teleported -= buffer.Clear;
            jumps.Remove(context);
        }

        public override void Exit(PlayerControlContext context)
        {
            ClearBuffer(context);
            context.HeldDirection = 0;
            if (context.Mover != null) context.Mover.ClearPendingInput();
            if (context.Mover != null)
            {
                context.Mover.CourseSpeedOverride = 0f;
                context.Mover.CourseJumpHeightOverride = 0f;
                context.Mover.CourseJumpGravityMultiplier = 1f;
            }
            if (context.Balance != null) context.Balance.SetCorrectionInput(0f);
        }

        public override void Tick(PlayerControlContext context, in PlayerInputFrame input)
        {
            if (InputGate.IsBlocked)
            {
                Exit(context);
                return;
            }
            context.HeldDirection = input.AuxDirection;
            ApplyMovementSettings(context);

            if (context.Mover != null)
            {
                var mover = context.Mover;
                if (!mover.isActiveAndEnabled || !mover.ControlEnabled
                    || (context.Condition != null && context.Condition.AllLimbsLost))
                {
                    Exit(context);
                    return;
                }
                if (!mover.Simulated)
                {
                    // 목마 위층은 위치만 Follow가 맡는다. 균형 보정과 F 하차는 계속 받아야 한다.
                    ClearBuffer(context);
                    mover.ClearPendingInput();
                }
                else
                {
                    if (!jumps.TryGetValue(context, out var buffer))
                    {
                        buffer = new TightropeJumpBuffer();
                        jumps.Add(context, buffer);
                        mover.Teleported += buffer.Clear;
                    }
                    mover.SetMoveInput(input.Move);
                    if (buffer.Resolve(input.ActionPressed, input.AuxDirection, !mover.IsAirborne,
                        Time.timeAsDouble, GameSettings.Tightrope.RopeMovement.JumpInputBuffer, out int direction))
                    {
                        if (direction != 0) mover.RequestLaneJump(direction);
                        else mover.RequestJump();
                    }
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
