using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropePracticeBotBrain
    {
        bool EvacuateBurningLane(TightropeCourse course, int lane, float distance, ref PlayerInputFrame input)
        {
            var fire = TightropeFire.Current;
            if (fire == null || !fire.IsPerformanceActive || fire.Settings == null) return false;
            int warningLane = fire.PendingSegment >= 0 && fire.PendingSegment < fire.Settings.segments.Length
                ? fire.Settings.segments[fire.PendingSegment].lane - 1 : TightropeCourse.NoLane;
            if (warningLane != lane && course.IsLaneUsable(lane, distance)) return false;
            foreach (int direction in new[] { -1, 1 })
                if (lane + direction != warningLane && course.IsLaneUsable(lane + direction, distance)
                    && TryLaneJump(course, direction, ref input))
                { Status = "불 예고 · 옆줄로 피하는 중"; return true; }
            input.Move = 0f;
            Status = "불 예고 · 옆줄 점프 준비";
            return true;
        }

        bool HandleGate(TightropeCourse course, NetworkPlayer below, ref PlayerInputFrame input)
        {
            var saws = TightropeSaws.Current;
            if (saws == null || !saws.isActiveAndEnabled || !saws.IsCooperativeGateActive || saws.IsCooperativeGatePassable) return false;
            if (course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position)) != saws.CooperativeGateLane) return false;
            float distance = course.GetDistance(player.transform.position);
            float gate = saws.CooperativeGateDistance;
            if (distance > gate + .2f)
            {
                float plateGap = saws.CooperativePlateDistance - distance;
                input.Move = Mathf.Abs(plateGap) > .2f ? Mathf.Sign(plateGap) : 0f;
                Status = input.Move != 0f ? "톱 뒤 발판으로 이동" : "후방 발판 · 동료가 통과할 때까지 대기";
                return true;
            }
            float launch = saws.CooperativeJumpLaunchDistance;
            if (below != null)
            {
                float baseDistance = course.GetDistance(below.transform.position);
                if (Role == TightropeBotRole.Upper && saws.IsCooperativeGateStopped
                    && Mathf.Abs(baseDistance - launch) <= .18f && !below.IsAirborne && Time.time >= nextJumpAt)
                {
                    input.Move = 1f;
                    input.ActionPressed = true;
                    nextJumpAt = Time.time + 1.5f;
                    Status = "관문 뒤 발판으로 앞점프";
                }
                else Status = "관문 앞 점프 위치까지 이동해 주세요";
                return true;
            }
            if (distance < launch - .07f) return false;
            input.Move = distance > launch + .1f ? -1f : 0f;
            Status = Role == TightropeBotRole.Lower
                ? "관문 앞 정지 · 위에서 W+Space로 발판을 밟으세요"
                : "관문 앞 정지 · 목마로 뒤 발판을 밟아 주세요";
            if (Role == TightropeBotRole.Upper) TryMount(Partner(), PiggybackSystem.Current, ref input);
            return true;
        }

        void AvoidOrdinarySaws(TightropeCourse course, int lane, float distance, ref PlayerInputFrame input)
        {
            var saws = TightropeSaws.Current;
            if (saws == null || !saws.isActiveAndEnabled || !course.IsGameplayActive || Time.time < nextJumpAt || input.ActionPressed) return;
            if (saws.IsVerticalActive && saws.VerticalLane == lane && !saws.IsPlatePressed
                && saws.VerticalDistance - distance is > 0f and < 3.5f)
            {
                foreach (int direction in new[] { -1, 1 })
                    if (course.IsLaneUsable(lane + direction, distance) && TryLaneJump(course, direction, ref input))
                    { Status = "정면 톱 · 옆줄로 회피"; return; }
                input.Move = -1f;
                return;
            }
            if (input.Move <= 0f) return;
            for (int i = 0; i < saws.HorizontalCount; i++)
            {
                float gap = saws.Settings.GetHorizontalDistance(i) - distance;
                if (gap < .7f || gap > .95f || !saws.TryGetHorizontalPosition(i, out _)) continue;
                input.ActionPressed = true;
                nextJumpAt = Time.time + .8f;
                Status = "가로 톱 구간 점프";
                return;
            }
        }
    }
}
