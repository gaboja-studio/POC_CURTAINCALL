using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropePracticeBotBrain
    {
        int gateBrainRound = -1, gateBrainIndex = -1, gateLaunchFloor;
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
            if (saws == null || !saws.isActiveAndEnabled || !saws.IsCooperativeGateActive) return false;
            if (course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position)) != saws.CooperativeGateLane) return false;
            int round = TightropeRun.Current != null ? TightropeRun.Current.Round : -1;
            if (round != gateBrainRound || gateBrainIndex != saws.CooperativeGateIndex)
            {
                gateBrainRound = round; gateBrainIndex = saws.CooperativeGateIndex; gateLaunchFloor = 0;
            }
            int admittedFloor = saws.CooperativeAdmittedFloor(player);
            if (admittedFloor > 0) gateLaunchFloor = admittedFloor;
            float distance = course.GetDistance(player.transform.position);
            float gate = saws.CooperativeGateDistance;
            if (distance > gate + .2f)
            {
                // 3·4층은 먼저 건너도 버튼을 밟지 않는다. 승인 당시 층을 착지 뒤에도 기억한다.
                if (gateLaunchFloor >= 3 || saws.IsCooperativeGatePassable)
                {
                    float safe = saws.CooperativeUpperLandingDistance;
                    input.Move = Mathf.Abs(safe - distance) > .2f ? Mathf.Sign(safe - distance) : 0f;
                    Status = input.Move != 0f ? "뒤 버튼을 피해 앞쪽 안전 지점으로 이동"
                        : saws.IsCooperativeGatePassable ? "1층 통과 대기 · 모두 지나오면 결승선으로"
                        : "원래 2층이 버튼을 밟을 때까지 앞쪽에서 대기";
                    return true;
                }
                if (gateLaunchFloor != 2)
                { Status = "뒤 버튼은 원래 2층 담당 · 먼저 건넌 동료는 대기"; return true; }
                float plateGap = saws.CooperativePlateDistance - distance;
                input.Move = Mathf.Abs(plateGap) > .2f ? Mathf.Sign(plateGap) : 0f;
                Status = input.Move != 0f ? "원래 2층 · 뒤 버튼으로 이동" : "2층 버튼 · 톱을 띄워 1층을 보냅니다";
                return true;
            }
            float launch = saws.CooperativeJumpLaunchDistance;
            var stack = PiggybackSystem.Current;
            var above = stack != null ? stack.GetAbove(player) : null;
            if (below != null)
            {
                if (above != null)
                { Status = "맨 위부터 4 → 3 → 2층 순서 · 위 동료가 먼저 넘어갑니다"; return true; }
                if (!saws.IsCooperativeStackApproved)
                { Status = $"출발선에서 {saws.CooperativeRequiredStackSize}명이 목마로 모일 때까지 대기"; return true; }
                if (Time.time >= nextJumpAt && saws.CanLaunchCooperativeGate(player))
                {
                    gateLaunchFloor = admittedFloor > 0 ? admittedFloor : stack.GetFloor(player);
                    input.Move = 1f;
                    input.ActionPressed = true;
                    nextJumpAt = Time.time + 1.5f;
                    Status = gateLaunchFloor >= 3 ? $"원래 {gateLaunchFloor}층 · 버튼을 넘어 앞쪽으로 도약"
                        : "원래 2층 · 마지막으로 뒤 버튼을 향해 도약";
                }
                else Status = "목마 출발선에서 맨 위 동료의 도약 순서를 기다립니다";
                return true;
            }
            if (saws.IsCooperativeGatePassable)
            {
                input.Move = 1f;
                Status = "톱이 뛰었어요 · 1층은 앞으로 통과";
                return true;
            }
            if (distance < launch - .07f) return false;
            input.Move = distance > launch + .1f ? -1f : 0f;
            Status = saws.IsCooperativeStackApproved ? "1층은 정지 · 위층이 순서대로 넘고 2층이 버튼을 밟습니다"
                : $"톱 앞 정지 · F로 {saws.CooperativeRequiredStackSize}명 목마를 만드세요";
            if (above == null && Role == TightropeBotRole.Upper) TryMount(GateMountPartner(course, saws), stack, ref input);
            return true;
        }

        NetworkPlayer GateMountPartner(TightropeCourse course, TightropeSaws saws)
        {
            var preferred = Partner();
            if (GatePartnerAvailable(course, saws, preferred)) return preferred;
            NetworkPlayer found = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in NetworkPlayer.All)
            {
                if (!GatePartnerAvailable(course, saws, candidate)) continue;
                float distance = Vector3.ProjectOnPlane(candidate.transform.position - player.transform.position, Vector3.up).sqrMagnitude;
                if (distance >= nearest) continue;
                found = candidate; nearest = distance;
            }
            return found;
        }

        bool GatePartnerAvailable(TightropeCourse course, TightropeSaws saws, NetworkPlayer candidate) => candidate != player
            && RelayParticipant(candidate) && !candidate.IsAirborne
            && (PiggybackSystem.Current == null || PiggybackSystem.Current.GetBelow(candidate) == null)
            && course.Layout.GetLaneSlotAt(course.GetSide(candidate.transform.position)) == saws.CooperativeGateLane
            && course.GetDistance(candidate.transform.position) <= saws.CooperativeGateDistance;

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
