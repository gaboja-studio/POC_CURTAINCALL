using CurtainCall.Player;

namespace CurtainCall.HumanCannon
{
    /// <summary>평소에는 공용 플랫폼 이동, 대포 점유/비행 중에는 대포가 이동을 맡는다.</summary>
    public sealed class HumanCannonControlScheme : PlatformControlScheme
    {
        internal HumanCannonPilot Pilot { get; set; }

        public override void Enter(PlayerControlContext context)
        {
            base.Enter(context);
            // 공용 플레이어의 테스트용 시작값을 덮어쓴다. 대포에서는 외줄 균형을 사용하지 않는다.
            // 매 프레임 초기화하면 대포 착지 사망의 래그돌까지 복구되므로 전환할 때만 끈다.
            if (context.Balance != null && context.Balance.IsActive)
            {
                // 사망 후 콘텐츠 재연결에서는 ResetBalance/래그돌 복구 이벤트를 보내지 않는다.
                if (context.Balance.HasFallen) context.Balance.SetDisplayedState(false, 0f);
                else context.Balance.SetBalanceActive(false);
            }
        }

        public override void Tick(PlayerControlContext context, in PlayerInputFrame input)
        {
            if (Pilot == null) return;
            if (!Pilot.IsDriving) base.Tick(context, input); // 시작·포인트 플랫폼: 공용 달리기·앉기 포함
            else context.LogUndefinedLocomotion(input, "인간 대포 탑승·비행"); // 대포 전용 이동 중 달리기·앉기는 미정: 로그만
            Pilot.ReadInput(input);
        }
    }
}
