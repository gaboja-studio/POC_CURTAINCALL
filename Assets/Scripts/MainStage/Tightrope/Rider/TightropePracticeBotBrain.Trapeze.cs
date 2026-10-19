using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Fire;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropePracticeBotBrain
    {
        int trapezeFrom = -1;
        ulong trapezePartner = TightropePracticeBot.Nobody;

        bool HandleTrapeze(TightropeCourse course, NetworkPlayer other, ref PlayerInputFrame input)
        {
            var trapeze = TightropeTrapeze.Current;
            if (trapeze == null || !trapeze.IsActive || !course.HasTrapezeGap) return false;
            var partner = TrapezePartner(other);
            int held = trapeze.HeldPole(player);
            if (held >= 0)
            {
                trapezeFrom = held;
                bool lower = trapeze.IsLower(player);
                if (lower)
                {
                    trapezePartner = trapeze.HolderAt(held);
                    input.Move = 1f;
                    TryTrapezeLaunch(trapeze, held, true, ref input);
                    Status = input.ActionPressed ? "아래에서 다음 봉으로 도약합니다" : "아래에서 반동을 기다립니다 · 앞쪽에서 Space";
                    return true;
                }
                bool occupiedBelow = trapeze.PassengerAt(held) != TightropePracticeBot.Nobody;
                if (occupiedBelow) trapezePartner = trapeze.PassengerAt(held);
                else if (WaitForTrapezePartner(course, trapeze, partner, held))
                {
                    // 먼저 간 동료가 몸을 내어 줄 때에는 봉을 중앙으로 진정시켜 접근 지점을 유지한다.
                    input.Move = Mathf.Clamp(-trapeze.SwingAngle(held) * .035f - trapeze.SwingSpeed(held) * .012f, -1f, 1f);
                    Status = held == 0 ? "첫 봉을 잡았어요 · F로 제 몸에 매달려 주세요" : "다음 봉에서 기다립니다 · 제 몸에 F로 매달려 주세요";
                    return true;
                }
                float angle = trapeze.SwingAngle(held), speed = trapeze.SwingSpeed(held);
                input.Move = angle > 65f ? -1f : angle < -65f ? 1f : speed < -.2f ? -1f : 1f;
                if (!occupiedBelow) TryTrapezeLaunch(trapeze, held, false, ref input);
                Status = occupiedBelow ? "위에서 반동을 만듭니다 · 아래 동료가 먼저 Space"
                    : input.ActionPressed ? "동료가 기다리는 다음 봉으로 도약합니다" : "반동을 만들어 다음 잡기 타이밍을 기다립니다";
                return true;
            }
            if (trapeze.IsInFlight(player))
            {
                int target = Mathf.Clamp(trapezeFrom + 1, 0, trapeze.PoleCount);
                Vector3 point = target < trapeze.PoleCount
                    ? trapeze.PoleFeet(target, trapeze.HolderAt(target) != TightropePracticeBot.Nobody)
                    : course.GetLanePosition(trapeze.LaneIndex, course.TrapezeGapTo + 1.2f);
                float forward = course.GetDistance(point) - course.GetDistance(player.transform.position);
                float side = course.GetSide(point) - course.GetSide(player.transform.position);
                // 봉에서 놓은 전용 비행 중에만 공중 입력을 만든다.
                input.Move = forward > .2f ? 1f : forward < -.2f ? -1f : 0f;
                input.Posture = Mathf.Clamp(side * 3f, -1f, 1f);
                if (target < trapeze.PoleCount) TryTrapezeCatch(trapeze, target, ref input);
                Status = target < trapeze.PoleCount ? "공중에서 다음 봉·동료에게 접근 · 가까워지면 F" : "마지막 봉에서 줄 위로 착지 중";
                return true;
            }

            float distance = course.GetDistance(player.transform.position);
            if (distance < trapeze.EntryDistance - 6f || distance > course.TrapezeGapTo + FireCourseSpans.GateMargin)
            { trapezeFrom = -1; trapezePartner = TightropePracticeBot.Nobody; return false; }
            if (player.IsAirborne || mover.IsAirborne)
            {
                // 첫 목마 분리 점프·일반 점프·옆줄 점프는 기존 고정 궤적을 그대로 따른다.
                if (distance < course.TrapezeGapFrom + 1f) TryTrapezeCatch(trapeze, 0, ref input);
                Status = "높은 첫 봉에 닿으면 F · 점프 궤적 유지";
                return true;
            }
            trapezeFrom = -1;
            input.Posture = Correction(balance != null ? balance.Value : 0f);
            if (distance >= course.TrapezeGapTo)
            {
                if (partner != null && trapeze.IsCrossing(partner))
                {
                    float target = course.TrapezeGapTo + 1.6f;
                    input.Move = Mathf.Abs(target - distance) > .2f ? Mathf.Sign(target - distance) : 0f;
                    var catches = TightropeCatchSystem.Current;
                    if (input.Move == 0f && catches != null && !catches.IsReady(player) && Time.time >= nextInteractAt)
                    { input.InteractTapped = true; nextInteractAt = Time.time + .8f; }
                    Status = "줄에 도착했어요 · 뒤따라오는 동료 받기 대기";
                    return true;
                }
                return HandleProtectedExit(course, partner, course.TrapezeGapTo, course.TrapezeGapTo + 3f, ref input);
            }

            var stack = PiggybackSystem.Current;
            var below = stack != null ? stack.GetBelow(player) : null;
            var above = stack != null ? stack.GetAbove(player) : null;
            if (below != null)
            {
                trapezePartner = below.OwnerClientId;
                if (above != null) { Status = "공중그네는 2명씩 · 맨 위 동료가 내려올 때까지 대기"; return true; }
                if (releaseBelow == below.OwnerClientId)
                { input.InteractLongPressed = true; Status = "역할 변경 · 목마에서 내려옵니다"; return true; }
                float launch = TrapezeLaunchDistance(trapeze, player);
                int baseLane = course.Layout.GetLaneSlotAt(course.GetSide(below.transform.position));
                if (baseLane != trapeze.LaneIndex)
                {
                    var bottom = below;
                    while (stack.GetBelow(bottom) != null) bottom = stack.GetBelow(bottom);
                    bool safe = !bottom.IsAirborne && bottom.TryGetComponent(out PlayerMover bottomMover)
                        && course.TryGetSupportingRope(bottomMover, out int supportingLane)
                        && course.IsLaneUsable(supportingLane, course.GetDistance(bottom.transform.position)
                            + bottomMover.BodyRadius + mover.BodyRadius + GameSettings.Base.Character.PlayerGap);
                    input.InteractLongPressed = safe;
                    Status = safe ? "먼저 목마에서 내려와 공중그네 줄로 각각 이동합니다"
                        : "안전하게 내릴 수 있도록 아래 동료가 줄 위로 돌아오기를 기다립니다";
                    return true;
                }
                if (trapeze.HolderAt(0) == TightropePracticeBot.Nobody && baseLane == trapeze.LaneIndex
                    && Mathf.Abs(course.GetDistance(below.transform.position) - launch) <= .25f
                    && !below.IsAirborne && Time.time >= nextJumpAt)
                {
                    input.Move = 1f; input.ActionPressed = true; nextJumpAt = Time.time + 1.2f;
                    Status = "목마 위에서 높은 첫 봉으로 앞점프";
                }
                else Status = "첫 봉 앞 출발 위치에서 W+Space로 올라갈 준비";
                return true;
            }
            if (above != null)
            {
                trapezePartner = above.OwnerClientId;
                if (releaseAbove == above.OwnerClientId)
                { Status = "역할 변경 · 동료가 내려올 때까지 대기"; return true; }
            }
            releaseBelow = releaseAbove = TightropePracticeBot.Nobody;

            int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            if (lane != trapeze.LaneIndex)
            {
                if (above != null)
                {
                    // 위쪽 사람의 해제 입력을 대신 보내지 않는다. 하차할 앞 공간만 확보한다.
                    float gap = mover.BodyRadius + (above.TryGetComponent(out PlayerMover upperMover) ? upperMover.BodyRadius : mover.BodyRadius)
                        + GameSettings.Base.Character.PlayerGap;
                    if (!course.IsLaneUsable(lane, distance + gap)) input.Move = -1f;
                    Status = "위 동료가 F 길게로 내려오면 공중그네 줄로 각각 옮깁니다";
                }
                else { TryLaneJump(course, System.Math.Sign(trapeze.LaneIndex - lane), ref input); Status = "공중그네 출발 줄로 이동 중"; }
                return true;
            }

            ulong firstHolder = trapeze.HolderAt(0);
            if (firstHolder != TightropePracticeBot.Nobody)
            {
                bool ownPair = partner == null || firstHolder == partner.OwnerClientId;
                if (!ownPair || above != null || trapeze.PassengerAt(0) != TightropePracticeBot.Nobody)
                { Status = "앞 팀이 첫 봉을 비울 때까지 기다립니다"; return true; }
                float target = course.GetDistance(trapeze.PoleFeet(0, true));
                target = Mathf.Min(target, course.TrapezeGapFrom - .4f);
                input.Move = Mathf.Abs(target - distance) > .2f ? Mathf.Sign(target - distance) : 0f;
                if (trapeze.CanAttachToPole(0, player))
                { trapezePartner = firstHolder; TryTrapezeCatch(trapeze, 0, ref input); }
                Status = "첫 봉의 동료 몸에 F로 매달립니다";
                return true;
            }

            if (above == null && (partner == null || trapeze.IsCrossing(partner)
                || course.GetDistance(partner.transform.position) >= course.TrapezeGapFrom))
            {
                // 앞 팀과 묶여 입구에 남은 봇은 아직 출발하지 않은 동료와 다음 목마를 준비한다.
                partner = null;
                foreach (var candidate in NetworkPlayer.All)
                {
                    if (candidate == player || !RelayParticipant(candidate) || trapeze.IsCrossing(candidate)
                        || course.GetDistance(candidate.transform.position) >= course.TrapezeGapFrom
                        || course.GetDistance(candidate.transform.position) < trapeze.EntryDistance - 6f
                        || (stack != null && stack.IsInStack(candidate))) continue;
                    if (partner == null || candidate.OwnerClientId < partner.OwnerClientId) partner = candidate;
                }
            }
            float start = TrapezeLaunchDistance(trapeze, above != null ? above : Role == TightropeBotRole.Upper ? player : partner);
            if (above != null || Role != TightropeBotRole.Upper)
            {
                input.Move = Mathf.Abs(start - distance) > .2f ? Mathf.Sign(start - distance) : 0f;
                Status = above != null ? "첫 봉 앞 정지 · 위에서 W+Space로 뛰고 F로 잡아 주세요" : "첫 봉 앞 목마 준비 · F로 제 위에 올라타 주세요";
                return true;
            }
            if (partner == null || !RelayParticipant(partner) || trapeze.IsCrossing(partner))
            { Status = "첫 봉에 함께 올라갈 동료를 기다립니다"; return true; }
            if (stack != null && stack.GetAbove(partner) != null)
            { Status = "다른 동료의 목마 준비가 끝나기를 기다립니다"; return true; }
            float partnerDistance = course.GetDistance(partner.transform.position);
            input.Move = Mathf.Abs(partnerDistance - distance) > .65f ? Mathf.Sign(partnerDistance - distance) : 0f;
            TryMount(partner, stack, ref input);
            Status = input.InteractTapped ? "동료 위로 올라가 높은 첫 봉을 준비합니다" : "목마 아래 동료가 첫 봉 앞으로 이동하기를 기다립니다";
            return true;
        }

        void TryTrapezeLaunch(TightropeTrapeze trapeze, int pole, bool lower, ref PlayerInputFrame input)
        {
            int next = pole + 1;
            if (next < trapeze.PoleCount && trapeze.HolderAt(next) != TightropePracticeBot.Nobody
                && trapeze.PassengerAt(next) != TightropePracticeBot.Nobody) return;
            if (Time.time < nextJumpAt || !trapeze.CanLaunchTowardNext(pole, lower)) return;
            input.ActionPressed = true;
            nextJumpAt = Time.time + .6f;
        }

        void TryTrapezeCatch(TightropeTrapeze trapeze, int pole, ref PlayerInputFrame input)
        {
            if (Time.time < nextInteractAt || (!trapeze.CanGrabPole(pole, player) && !trapeze.CanAttachToPole(pole, player))) return;
            input.InteractTapped = true;
            nextInteractAt = Time.time + .12f;
        }

        NetworkPlayer TrapezePartner(NetworkPlayer fallback)
        {
            foreach (var candidate in NetworkPlayer.All)
                if (RelayParticipant(candidate) && candidate.OwnerClientId == trapezePartner) return candidate;
            return fallback != null && RelayParticipant(fallback) ? fallback : null;
        }

        static bool WaitForTrapezePartner(TightropeCourse course, TightropeTrapeze trapeze, NetworkPlayer partner, int held)
        {
            if (partner == null || !RelayParticipant(partner)) return false;
            if (held == 0 && course.GetDistance(partner.transform.position) < course.TrapezeGapFrom) return true;
            int partnerPole = trapeze.HeldPole(partner);
            return partnerPole >= 0 && partnerPole < held || trapeze.IsInFlight(partner);
        }

        static float TrapezeLaunchDistance(TightropeTrapeze trapeze, NetworkPlayer top)
        {
            var topMover = top != null ? top.GetComponent<PlayerMover>() : null;
            if (topMover == null) return trapeze.EntryDistance - 1f;
            float gravity = Mathf.Max(.01f, topMover.JumpGravity);
            float speed = GameSettings.Tightrope.Piggyback.ForwardJumpSpeed * Mathf.Sqrt(gravity / GameSettings.Base.Character.Gravity);
            return trapeze.EntryDistance - Mathf.Clamp(speed * topMover.JumpSpeed / gravity, .5f, 1.5f);
        }
    }
}
