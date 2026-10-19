using System.Runtime.CompilerServices;
using CurtainCall.Cameras;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 기본 이동 규칙(2026-10-14 PM, 외줄 플랫폼 이동에서 공용으로 옮김). 이동(W/S) → 앞뒤, 자세 제어(A/D) → 옆으로 8방향 일반 이동(이동 방향을 바라봄),
    /// 기본 액션(Space) → 제자리 점프. 달리기(LShift)·앉기(LCtrl) → 누르는 동안 달리기·앉기(조합 없이 홀드, 앉기 우선, 2026-10-19 PM).
    /// 좌우 보조 방향(Q/E)·상호작용은 동작 없음.
    /// 앞뒤·옆은 코스 기준이고, 자기 로컬 플레이어가 마우스 시선 시점(1인칭·3인칭 비고정)일 때만 카메라 수평 시선 기준이다(Task-045).
    /// 3인칭 고정·관전·다른 플레이어는 코스 기준 그대로.
    /// 이동 키를 누른 채 시점이 바뀌면 키를 모두 뗄 때까지 직전 기준을 유지한다(2026-10-15 사용자 결정).
    /// 조작 규칙을 끼우지 않은 공간은 <see cref="ControlSettings"/>의 기본 이동 규칙을 쓰고, 비어 있으면 이 규칙을 쓴다.
    /// 외줄 코스는 플랫폼에서 이 규칙, 줄 위에서 외줄 규칙으로 바꾼다(공연장 <c>CourseControlSwitcher</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "CurtainCall/Control Scheme/Platform (기본 이동)", fileName = "PlatformControlScheme")]
    public class PlatformControlScheme : PlayerControlScheme
    {
        // 규칙 에셋은 여러 플레이어가 함께 쓰므로 플레이어별 이동 기준은 컨텍스트마다 따로 둔다
        readonly ConditionalWeakTable<PlayerControlContext, MoveBasis> bases = new();

        public override void Enter(PlayerControlContext context)
        {
            context.HeldDirection = 0;
            bases.Remove(context);
            if (context.Mover != null) context.Mover.FreeMovement = true;
        }

        public override void Exit(PlayerControlContext context)
        {
            bases.Remove(context);
            if (context.Mover != null) context.Mover.FreeMovement = false;
        }

        public override void Tick(PlayerControlContext context, in PlayerInputFrame input)
        {
            if (context.Mover == null) return;
            bool moving = input.Move != 0f || input.Posture != 0f;
            Vector3 view = bases.GetOrCreateValue(context).Resolve(LocalViewForward(context.Player), moving);
            Vector2 move = ToCourseInput(view, context.Mover.CourseForward, input.Posture, input.Move);
            context.Mover.SetMoveInput(move.x, move.y);
            ApplyLocomotion(context, input);
        }

        /// <summary>
        /// 플랫폼 공통 동작: 달리기(LShift)·앉기(LCtrl) 홀드와 기본 액션(Space) 제자리 점프. 다른 일반 이동 규칙(배치 미리보기 등)도 같은 규칙을 쓰려고 이 함수를 부른다.
        /// 앉기가 달리기보다 우선하며, 앉아 있으면 점프 요청은 이동 부품이 무시한다(2026-10-19 PM).
        /// </summary>
        public static void ApplyLocomotion(PlayerControlContext context, in PlayerInputFrame input)
        {
            context.TrackLocomotionPresses(input, out _, out _);
            if (context.Mover == null) return;
            context.Mover.SetLocomotionInput(input.SprintHeld, input.CrouchHeld);
            if (input.ActionPressed) context.Mover.RequestJump();
        }

        /// <summary>
        /// 시선 기준 입력(옆 +1 오른쪽, 앞 +1 전진)을 <see cref="PlayerMover.SetMoveInput(float, float)"/>의 코스 기준 입력(x 옆, y 앞)으로 바꾼다.
        /// 시선이나 코스 방향이 없으면 입력을 그대로 돌려준다. 대각선이 빨라지지 않게 회전 전에 길이를 1로 제한한다.
        /// </summary>
        public static Vector2 ToCourseInput(Vector3 viewForward, Vector3 courseForward, float side, float forward)
        {
            viewForward.y = 0f;
            courseForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.0001f || courseForward.sqrMagnitude < 0.0001f) return new Vector2(side, forward);
            viewForward.Normalize();
            courseForward.Normalize();
            Vector3 wish = Vector3.ClampMagnitude(viewForward * forward + Vector3.Cross(Vector3.up, viewForward) * side, 1f);
            return new Vector2(Vector3.Dot(wish, Vector3.Cross(Vector3.up, courseForward)), Vector3.Dot(wish, courseForward));
        }

        /// <summary>
        /// 한 플레이어의 이동 기준. 이동 키를 누르기 시작한 기준(마우스 시선 / 코스)을 키를 모두 뗄 때까지 유지한다.
        /// 같은 기준 안에서는 지금 시선을 그대로 따르고(마우스 회전, 1인칭↔3인칭 비고정 포함), 누른 채 기준이 바뀌면 바뀌기 직전 기준으로 계속 간다.
        /// </summary>
        public sealed class MoveBasis
        {
            bool held;
            bool heldViewBased;
            Vector3 last;

            /// <summary><paramref name="view"/>: 지금 마우스 시선(아니면 zero). 반환: 이번 프레임 이동 기준 시선(zero면 코스 기준).</summary>
            public Vector3 Resolve(Vector3 view, bool moving)
            {
                bool viewBased = view != Vector3.zero;
                if (!moving)
                {
                    held = false;
                    return view;
                }
                if (!held)
                {
                    held = true;
                    heldViewBased = viewBased;
                }
                if (viewBased == heldViewBased) last = view;
                return last;
            }
        }

        /// <summary>이 플레이어가 공용 카메라의 로컬 플레이어이고 마우스 시선 시점일 때의 수평 시선. 아니면 Vector3.zero.</summary>
        static Vector3 LocalViewForward(GameObject player)
        {
            var view = CameraViewDirector.Current;
            if (view == null || !view.UsesMouseLook || view.LocalPlayer == null || view.LocalPlayer.gameObject != player) return Vector3.zero;
            return view.ViewForward;
        }
    }
}
