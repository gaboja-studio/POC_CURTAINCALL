using System.Runtime.CompilerServices;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Control;
using CurtainCall.Tightrope;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Player // 에셋 클래스 식별자 유지(Resources/Input/TightropeControlScheme.asset)
{
    /// <summary>
    /// 외줄타기 조작 규칙. W/S로 앞뒤 이동, 휠 클릭으로 180도 전환, A/D로 균형 보정.
    /// Space는 제자리 점프, 좌·우 클릭은 해당 방향 옆줄 점프다.
    /// 상호작용 짧게 → 목마 연결 시도, 길게 → 목마 해제(실제 처리는 목마 기능). 달리기(LShift)·앉기(LCtrl) → 동작 미정, 누른 순간 로그만.
    /// 규칙 근거: Docs/References/tightrope-keymap-summary.md, Harness/Project/Decisions/tightrope-rules.md
    /// 줄 위에서 <see cref="CurtainCall.Tightrope.CourseControlSwitcher"/>가 끼운다(공용 기본 규칙이 아니다).
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Tightrope", fileName = "TightropeControlScheme")]
    public class TightropeControlScheme : PlayerControlScheme
    {
        readonly ConditionalWeakTable<PlayerControlContext, TightropeJumpBuffer> jumps = new();
        // 기존 목마/균형 표시 호출도 시점과 무관한 진행 방향을 사용한다.
        public static int LocalViewSign(PlayerMover mover) => 1;

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
            CancelCatch(context);
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
            // M2 추락 매달림: 매달린 사람은 조작 불가, 근처 동료는 F 짧게로 끌어올리기(2초 동안 다른 조작 없음, F 길게 취소).
            if (TightropeHangSystem.Current != null && TightropeHangSystem.Current.TryHandleInput(context, input))
            {
                ClearBuffer(context);
                context.Balance?.SetCorrectionInput(0f);
                return;
            }
            if (CurtainCall.HumanCannon.HumanCannonPoleSystem.Current != null
                && CurtainCall.HumanCannon.HumanCannonPoleSystem.Current.TryHandleInput(context, input))
            {
                ClearBuffer(context);
                context.Balance?.SetCorrectionInput(0f);
                return;
            }
            if (TightropeTrapeze.Current != null && TightropeTrapeze.Current.TryHandleInput(context, input))
            {
                ClearBuffer(context);
                context.Balance?.SetCorrectionInput(0f);
                return;
            }
            if (TightropeRelayGate.Current != null && TightropeRelayGate.Current.TryHandleInput(context, input))
            {
                ClearBuffer(context);
                context.Balance?.SetCorrectionInput(0f);
                return;
            }
            TightropeCannonAirControl.Current?.SetInput(context, input);
            if (TightropeSwingPole.Current != null && TightropeSwingPole.Current.TryHandleInput(context, input))
            {
                ClearBuffer(context);
                context.Balance?.SetCorrectionInput(0f);
                return;
            }
            const int viewSign = 1;
            context.HeldDirection = input.AuxDirection * viewSign;
            context.LogUndefinedLocomotion(input, "외줄 위"); // 줄 위 달리기·앉기는 미정(2026-10-19 PM): 로그만, 이동·균형·점프는 그대로
            ApplyMovementSettings(context);
            if (input.Move != 0f || input.ActionPressed || input.LaneChangePressed || input.TurnAroundPressed || input.InteractLongPressed) CancelCatch(context);

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
                    mover.SetMoveInput(input.Move * viewSign);
                    if (input.TurnAroundPressed)
                    {
                        buffer.Clear();
                        mover.RequestRopeTurnJump();
                    }
                    else if (buffer.Resolve(input.ActionPressed || input.LaneChangePressed, context.HeldDirection, !mover.IsAirborne,
                        Time.timeAsDouble, GameSettings.Tightrope.RopeMovement.JumpInputBuffer, out int direction))
                    {
                        if (direction != 0) mover.RequestLaneJump(direction);
                        else mover.RequestJump();
                    }
                }
            }

            if (context.Balance != null)
                context.Balance.SetCorrectionInput(input.Posture * viewSign);

            bool requestedDevice = false;
            if (context.Mover != null && context.Mover.TryGetComponent(out HumanCannonPilot pilot))
            {
                if (pilot.TryHandleCarryInput(input)) return;
                requestedDevice = input.InteractTapped && (pilot.TryRequestNearbyPole() || pilot.TryRequestNearbyCannon());
            }
            if (context.Interaction != null)
            {
                if (input.InteractTapped && !requestedDevice)
                    context.Interaction.RequestInteract();
                if (input.InteractLongPressed) context.Interaction.RequestRelease();
            }
        }

        static void CancelCatch(PlayerControlContext context)
        {
            if (NetworkPlayer.Local != null && context.Mover != null
                && context.Mover.gameObject == NetworkPlayer.Local.gameObject)
                TightropeCatchSystem.Current?.CancelLocal();
        }
    }
}
