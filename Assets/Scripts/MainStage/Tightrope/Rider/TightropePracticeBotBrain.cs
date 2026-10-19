using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>연습 동료의 다음 입력만 계산한다. 실제 이동·균형·목마·부상 판정은 기존 시스템이 처리한다.</summary>
    public sealed partial class TightropePracticeBotBrain
    {
        readonly NetworkPlayer player, assignedPartner;
        readonly PlayerMover mover;
        readonly PlayerBalance balance;
        ulong releaseBelow = TightropePracticeBot.Nobody, releaseAbove = TightropePracticeBot.Nobody;
        float nextJumpAt, nextInteractAt;

        public TightropeBotRole Role { get; private set; }
        public string Status { get; private set; } = "동료를 기다립니다.";

        public TightropePracticeBotBrain(NetworkPlayer player, NetworkPlayer partner = null)
        {
            this.player = player;
            assignedPartner = partner;
            mover = player != null ? player.GetComponent<PlayerMover>() : null;
            balance = player != null ? player.GetComponent<PlayerBalance>() : null;
        }

        public void SetRole(TightropeBotRole role)
        {
            if (Role == role) return;
            Role = role;
            var stack = PiggybackSystem.Current;
            var below = stack != null ? stack.GetBelow(player) : null;
            var above = stack != null ? stack.GetAbove(player) : null;
            releaseBelow = below != null ? below.OwnerClientId : TightropePracticeBot.Nobody;
            releaseAbove = above != null ? above.OwnerClientId : TightropePracticeBot.Nobody;
            nextInteractAt = Time.time + .2f;
        }

        public void Reset()
        {
            releaseBelow = releaseAbove = TightropePracticeBot.Nobody;
            nextJumpAt = nextInteractAt = Time.time + .6f;
            Status = "출발점에서 다시 준비 중";
        }

        NetworkPlayer Partner()
        {
            if (assignedPartner != null) return assignedPartner;
            foreach (var other in NetworkPlayer.All)
                if (other != null && other != player && other.IsSpawned && other.OwnerClientId == NetworkManager.ServerClientId)
                    return other;
            return null;
        }

        public PlayerInputFrame BuildInput()
        {
            var input = new PlayerInputFrame();
            var course = TightropeCourse.Current;
            var other = Partner();
            if (player == null || mover == null || course == null || !course.IsTraversalActive)
            { Status = "외줄 코스 대기"; return input; }
            if (player.State != PlayerState.Normal || (balance != null && balance.HasFallen))
            { Status = "봇 추락 · R로 다시 시작"; return input; }
            if (other == null || other.State != PlayerState.Normal)
            { Status = "동료를 기다립니다. 다시 시작은 R"; return input; }

            var stack = PiggybackSystem.Current;
            var below = stack != null ? stack.GetBelow(player) : null;
            var above = stack != null ? stack.GetAbove(player) : null;
            // 도약 방향은 출발할 때만 정한다. 비행 중에는 새 이동·옆줄 입력을 만들지 않는다.
            if (player.IsAirborne) { Status = "착지 대기"; return input; }
            if (balance != null && balance.IsActive)
                input.Posture = Correction(balance.Value);
            if (releaseBelow != TightropePracticeBot.Nobody || releaseAbove != TightropePracticeBot.Nobody)
            {
                // 전환 당시 연결만 해제한다. 상대가 곧바로 반대쪽에 올라탄 새 연결은 기다리지 않는다.
                if (below != null && below.OwnerClientId == releaseBelow)
                { input.InteractLongPressed = true; Status = "내려온 뒤 역할 교대"; return input; }
                if (above != null && above.OwnerClientId == releaseAbove)
                { Status = "동료 하차 대기"; return input; }
                releaseBelow = releaseAbove = TightropePracticeBot.Nobody;
            }

            if (below != null)
            {
                Status = "봇이 위 · 내가 W/S로 이동";
                HandleGate(course, below, ref input);
                return input;
            }

            if (!mover.FreeMovement && course.TryGetRopePoint(player.transform.position, out int lane, out float distance))
            {
                if (EvacuateBurningLane(course, lane, distance, ref input)) return input;
                if (HandleGate(course, null, ref input)) return input;
                if (above != null && Role == TightropeBotRole.Lower)
                {
                    input.Move = distance < course.Shape.FinishDistance + 1f ? 1f : 0f;
                    Status = "봇이 아래 · 관문 앞에서 멈춥니다";
                }
                else FollowRope(course, other, lane, distance, ref input);
                AvoidOrdinarySaws(course, lane, distance, ref input);
                return input;
            }

            Status = "플랫폼에서 동료 따라가는 중";
            Vector3 target = other.transform.position;
            float otherDistance = course.GetDistance(target);
            float selfDistance = course.GetDistance(player.transform.position);
            if (above != null && Role == TightropeBotRole.Lower)
            {
                if (selfDistance >= course.FinishDistance)
                { Status = "도착 플랫폼 · 동료 하차 대기"; return input; }
                int entryLane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
                entryLane = ClosestUsableLane(course, entryLane, .5f);
                if (entryLane == TightropeCourse.NoLane) { Status = "건널 수 있는 줄 대기"; return input; }
                float entrySideError = course.Layout.GetLaneSide(entryLane) - course.GetSide(player.transform.position);
                target = course.GetLanePosition(entryLane, Mathf.Abs(entrySideError) > .12f ? Mathf.Min(-.8f, selfDistance) : 1f);
                Status = "봇이 아래 · 줄 입구로 이동 중";
                return WalkPlatform(course, target);
            }
            if (selfDistance < 0f && otherDistance >= 0f)
            {
                int startLane = Mathf.Clamp(course.Layout.GetLaneSlotAt(course.GetSide(target)), 0, course.LaneCount - 1);
                if (!course.IsLaneUsable(startLane, .5f)) startLane = ClosestUsableLane(course, startLane, .5f);
                if (startLane == TightropeCourse.NoLane) { Status = "건널 수 있는 줄 대기"; return input; }
                float sideError = course.Layout.GetLaneSide(startLane) - course.GetSide(player.transform.position);
                // 출발 플랫폼에서 먼저 줄 중앙에 맞춘 뒤 앞으로 들어간다.
                target = course.GetLanePosition(startLane, Mathf.Abs(sideError) > .12f ? Mathf.Min(-.8f, selfDistance) : 1f);
            }
            else if (Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).magnitude < 1.1f)
            { TryMount(other, stack, ref input); return input; }
            input = WalkPlatform(course, target);
            return input;
        }

        public static float Correction(float value) => value > 3f ? -1f : value < -3f ? 1f : 0f;

        void FollowRope(TightropeCourse course, NetworkPlayer other, int lane, float distance, ref PlayerInputFrame input)
        {
            float partnerDistance = course.GetDistance(other.transform.position);
            int partnerLane = course.Layout.GetLaneSlotAt(course.GetSide(other.transform.position));
            float gap = partnerDistance - distance;
            // 옆줄에서 동료 몸 바로 위에 자동 합체하지 않도록 약간 뒤에서 옮긴다.
            if (partnerLane != lane && Mathf.Abs(gap) <= 5f && gap >= 1.6f)
            {
                if (TryLaneJump(course, System.Math.Sign(partnerLane - lane), ref input))
                { Status = "동료가 있는 줄로 이동"; return; }
            }
            if (partnerLane != lane && gap < 1.6f) input.Move = -1f;
            else if (gap > 1.1f) input.Move = 1f;
            else if (gap < -.9f) input.Move = -1f;
            Status = Role == TightropeBotRole.Lower ? "동료 대기 · F로 봇 위에 타세요" : "동료 따라가는 중";
            TryMount(other, PiggybackSystem.Current, ref input);
        }

        void TryMount(NetworkPlayer other, PiggybackSystem stack, ref PlayerInputFrame input)
        {
            if (other == null || Role != TightropeBotRole.Upper || stack == null || !TightropeCourse.Current.IsGameplayActive
                || stack.IsInStack(player) || other.IsAirborne || Time.time < nextInteractAt) return;
            float reach = GameSettings.Tightrope.Piggyback.LinkDistance;
            if (player.TryGetComponent(out PlayerInteraction interaction)) reach *= interaction.ReachMultiplier;
            if (Vector3.ProjectOnPlane(other.transform.position - player.transform.position, Vector3.up).magnitude > reach * .9f) return;
            input.Move = 0f;
            input.InteractTapped = true;
            nextInteractAt = Time.time + .8f;
            Status = "동료 위로 올라가는 중";
        }

        PlayerInputFrame WalkPlatform(TightropeCourse course, Vector3 target)
        {
            Vector3 forward = CameraViewDirector.Current != null && CameraViewDirector.Current.LocalPlayer == player
                && CameraViewDirector.Current.UsesMouseLook ? CameraViewDirector.Current.ViewForward : course.Forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 offset = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up);
            float along = Vector3.Dot(offset, forward), side = Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward));
            return new PlayerInputFrame
            {
                Move = Mathf.Abs(along) > .1f ? Mathf.Sign(along) : 0f,
                Posture = Mathf.Abs(side) > .1f ? Mathf.Sign(side) : 0f
            };
        }

        static int ClosestUsableLane(TightropeCourse course, int from, float distance)
        {
            for (int gap = 0; gap < course.LaneCount; gap++)
                foreach (int lane in new[] { from - gap, from + gap })
                    if (lane >= 0 && lane < course.LaneCount && course.IsLaneUsable(lane, distance)) return lane;
            return TightropeCourse.NoLane;
        }

        bool TryLaneJump(TightropeCourse course, int direction, ref PlayerInputFrame input)
        {
            if (Time.time < nextJumpAt || direction == 0 || !course.IsGameplayActive || !course.TryGetComponent(out RopeLaneJumpRules rules)
                || !rules.CanLaneJump(direction) || !rules.TryGetLandingDistance(direction, out _)) return false;
            input.Move = 0f;
            input.AuxDirection = direction;
            input.ActionPressed = true;
            nextJumpAt = Time.time + .8f;
            return true;
        }
    }
}
