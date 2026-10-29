using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class CourseControlSwitcher
    {
        [Tooltip("이 씬의 외줄 끝에서만 원본 인간대포 그네로 F 인계를 허용한다.")]
        [SerializeField] bool allowCannonPoleExit;
        [Tooltip("줄 양끝에서 원본 그네 잡기를 허용할 진행 거리(m). 실제 손 도달 범위는 원본 봉이 검사한다.")]
        [SerializeField, Min(.1f)] float cannonPoleExitDistance = 2f;

        /// <summary>F 입력과 안내가 함께 사용하는, 현재 외줄 끝의 원본 그네 후보.</summary>
        public int FindCannonPoleExit(NetworkPlayer target)
        {
            if (!allowCannonPoleExit || ReviewSuspended || course == null || !course.UsesSceneGeometry
                || !course.IsTraversalActive || onRope != true || !StillOwnsControls
                || target == null || target != player || !target.IsLocal || target.State != PlayerState.Normal
                || mover == null || !mover.isActiveAndEnabled || !mover.ControlEnabled || !mover.Simulated
                || mover.PassThrough || balance != null && balance.HasFallen
                || HumanCannonSystem.Current == null || CannonPilot == null || CannonPilot.IsDriving
                || !course.IsWithinCannonPoleExit(target.transform.position, mover.BodyRadius, cannonPoleExitDistance)
                || HasPriorRopeInteraction(target)) return -1;
            if (target.TryGetComponent(out PlayerCondition condition) && condition.AllLimbsLost) return -1;
            var poles = HumanCannonPoleSystem.Current;
            return poles != null ? poles.FindGrabbable(target) : -1;
        }

        /// <summary>승인 전에는 외줄 이동·균형을 유지한다. 승인된 Pilot.IsDriving은 기존 표면 전환에서 인계한다.</summary>
        public bool TryRequestCannonPoleExit(PlayerMover requester)
        {
            if (requester == null || requester != mover) return false;
            int pole = FindCannonPoleExit(player);
            if (pole < 0) return false;
            // 원본 호스트 판정이 거리·점유·생존·운반 상태를 다시 검사한다.
            HumanCannonPoleSystem.Current.RequestPlatformGrab(pole);
            return true;
        }

        static bool HasPriorRopeInteraction(NetworkPlayer target)
        {
            var stack = PiggybackSystem.Current;
            if (stack != null && (stack.IsInStack(target) || stack.FindMountTarget(target) != null)) return true;
            var catcher = TightropeCatchSystem.Current;
            if (catcher != null && catcher.IsReady(target)) return true;
            var hangs = TightropeHangSystem.Current;
            if (hangs != null)
            {
                if (hangs.IsHanging(target) || hangs.NearestRescueTarget(target) != null) return true;
                foreach (var hang in hangs.Hangs) if (hang.Rescuer == target.OwnerClientId) return true;
            }
            var trapeze = TightropeTrapeze.Current;
            if (trapeze != null && trapeze.IsActive)
            {
                if (trapeze.IsCrossing(target)) return true;
                for (int i = 0; i < trapeze.PoleCount; i++)
                    if (trapeze.CanGrabPole(i, target) || trapeze.CanAttachToPole(i, target)) return true;
            }
            var relay = TightropeRelayGate.Current;
            if (relay != null && relay.IsActive)
            {
                if (relay.IsHeld(target)) return true;
                for (int i = 0; i < relay.PoleCount; i++) if (relay.CanUsePole(i, target)) return true;
            }
            var swing = TightropeSwingPole.Current?.ForPlayer(target);
            return swing != null && (swing.IsCrossing(target) || swing.CanGrabPole(target));
        }
    }
}
