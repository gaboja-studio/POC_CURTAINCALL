using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>봉을 놓은 뒤의 비행 전용 WASD 보정. 일반 이동·줄 위 점프·옆줄 점프에는 연결하지 않는다.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed class TightropeCannonAirControl : MonoBehaviour
    {
        readonly CannonAirControl air = new();
        readonly HumanCannonInjurySettings defaults = new();
        PlayerMover mover;
        NetworkPlayer player;
        public static TightropeCannonAirControl Current { get; private set; }
        void OnEnable() => Current = this;
        void Update()
        {
            var course = TightropeCourse.Current;
            var local = course != null && course.IsTraversalActive ? NetworkPlayer.Local : null;
            if (local == player && mover != null) return;
            Release();
            if (local == null || !local.TryGetComponent(out mover)) return;
            player = local;
        }
        public void SetInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            if (player == null || context.Mover != mover) return;
            var pole = TightropeSwingPole.Current;
            // 놓는 프레임의 입력도 받되 실제 보정은 IsInFlight인 동안에만 소비한다.
            if (pole == null || (!pole.IsHeld(player) && !pole.IsInFlight(player))) { air.Clear(); return; }
            air.SetInput(new Vector2(input.Posture, input.Move), Time.frameCount);
        }
        public Vector3 Consume()
        {
            if (mover == null || player == null || TightropeSwingPole.Current == null
                || !TightropeSwingPole.Current.IsInFlight(player)) { air.Clear(); return Vector3.zero; }
            return air.ConsumeVelocity(Time.frameCount, InputGate.IsBlocked, mover.CourseForward, ControlSpeedFor(player));
        }
        public void ClearInput() => air.Clear();
        public float ControlSpeedFor(NetworkPlayer target)
        {
            var settings = HumanCannonSystem.Current != null ? HumanCannonSystem.Current.Injuries : defaults;
            int lostLegs = target != null && target.TryGetComponent(out PlayerCondition condition) ? condition.LostLegCount : 0;
            float speed = TightropeSwingPole.Current != null ? TightropeSwingPole.Current.AirControlSpeed : 0f;
            return speed * settings.AirMultiplier(lostLegs);
        }
        void Release()
        {
            mover = null; player = null; air.Clear();
        }
        void OnDisable() { Release(); if (Current == this) Current = null; }
    }
}
