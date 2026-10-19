using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Fire;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropePracticeBotBrain
    {
        // 매달림·이동·옆줄 점프를 실제 입력으로만 요청한다. 관문 상태나 위치를 직접 바꾸지 않는다.
        bool HandleRelayGate(TightropeCourse course, NetworkPlayer other, ref PlayerInputFrame input)
        {
            var relay = TightropeRelayGate.Current;
            if (relay == null || !relay.IsActive || !course.IsGameplayActive) return false;
            float distance = course.GetDistance(player.transform.position);
            if (relay.IsHeld(player))
            {
                int held = relay.HeldPole(player);
                bool far = held >= 0 && relay.IsFarPole(held);
                bool release = far ? RelayEveryoneAcross(course, relay) : relay.ReturnOpen;
                if (release && Time.time >= nextInteractAt)
                {
                    input.InteractTapped = true;
                    nextInteractAt = Time.time + .8f;
                }
                Status = release ? "교대 완료 · 봉 놓기" : far
                    ? "도착 봉 유지 · 출발 쪽 동료가 건널 때까지 기다립니다"
                    : "출발 봉 유지 · 먼저 건너가 반대편 봉을 잡아 주세요";
                return true;
            }

            if (distance < relay.EntryDistance - 5f || distance > relay.ExitDistance + 5f) return false;
            if (player.IsAirborne || mover.IsAirborne)
            { Status = "교대 관문 · 옆줄 착지 대기"; return true; }
            input.Posture = Correction(balance != null ? balance.Value : 0f);
            var stack = PiggybackSystem.Current;
            if (stack != null && stack.IsInStack(player))
            {
                bool canRelease = stack.GetBelow(player) != null && stack.GetAbove(player) == null;
                if (canRelease && Time.time >= nextInteractAt)
                { input.InteractLongPressed = true; nextInteractAt = Time.time + .8f; }
                Status = canRelease ? "교대 봉을 잡기 위해 목마에서 내려옵니다" : "교대 관문 · 위에 탄 동료가 F를 길게 눌러 내려와 주세요";
                return true;
            }

            bool farSide = distance >= relay.GapTo + .4f;
            if (farSide)
            {
                if (RelayEveryoneAcross(course, relay))
                    return HandleProtectedExit(course, other, relay.GapTo, relay.ExitDistance, ref input);
                int farPole = RelayAssignment(course, relay, true);
                if (farPole >= 0) MoveToRelayPole(course, relay, farPole, ref input);
                else Status = "도착 쪽 대기 · 남은 동료가 건너고 있습니다";
                return true;
            }

            if (!relay.ReturnOpen && RelayParticipantCount() < relay.RequiredHolders * 2)
            {
                Status = $"교대 관문 · 살아 있는 동료 {relay.RequiredHolders * 2}명이 필요합니다";
                return true;
            }
            if (distance < relay.GapFrom)
            {
                int sourcePole = relay.ReturnOpen ? -1 : RelayAssignment(course, relay, false);
                if (sourcePole >= 0)
                { MoveToRelayPole(course, relay, sourcePole, ref input); return true; }
                if (!MoveToRelayLane(course, relay.TransitLane, ref input)) return true;
                if (!relay.OutboundOpen && !relay.ReturnOpen)
                {
                    float stand = relay.EntryDistance - distance;
                    input.Move = Mathf.Abs(stand) > .2f ? Mathf.Sign(stand) : 0f;
                    Status = "통로 앞 대기 · 출발 봉을 잡으면 건너갑니다";
                    return true;
                }
            }

            int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            if (course.IsLaneUsable(lane, Mathf.Min(distance + .5f, relay.GapTo)))
            {
                input.Move = 1f;
                Status = relay.ReturnOpen ? "동료가 연결한 줄로 합류 중" : "연결된 통로 건너기 · 반대편 봉에서 교대합니다";
                AvoidOrdinarySaws(course, lane, distance, ref input);
            }
            else Status = "교대 관문 · 동료의 줄 연결을 기다립니다";
            return true;
        }

        void MoveToRelayPole(TightropeCourse course, TightropeRelayGate relay, int pole, ref PlayerInputFrame input)
        {
            float error = course.GetDistance(relay.PoleFeet(pole)) - course.GetDistance(player.transform.position);
            int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            // 건너자마자 옆으로 뛰면 동료를 피해 뒤로 보정된 착지점이 아직 끊긴 틈에 걸릴 수 있다.
            // 현재 연결 줄로 봉의 안전한 앞뒤 위치까지 이동한 다음 옆줄을 바꾼다.
            if (lane != relay.PoleLane(pole) && Mathf.Abs(error) > .2f)
            {
                input.Move = Mathf.Sign(error);
                Status = relay.IsFarPole(pole) ? "도착 봉 높이까지 먼저 전진합니다" : "출발 봉 위치로 이동 중";
                return;
            }
            if (!MoveToRelayLane(course, relay.PoleLane(pole), ref input)) return;
            input.Move = Mathf.Abs(error) > .16f ? Mathf.Sign(error) : 0f;
            if (relay.CanUsePole(pole, player) && Time.time >= nextInteractAt)
            {
                input.Move = 0f;
                input.InteractTapped = true;
                nextInteractAt = Time.time + .8f;
            }
            Status = relay.IsFarPole(pole) ? "도착 봉 잡기 · 남은 동료에게 돌아올 줄을 연결합니다"
                : "출발 봉 잡기 · 동료가 먼저 건널 줄을 연결합니다";
        }

        bool MoveToRelayLane(TightropeCourse course, int targetLane, ref PlayerInputFrame input)
        {
            int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            if (lane == targetLane) return true;
            TryLaneJump(course, System.Math.Sign(targetLane - lane), ref input);
            Status = "교대 관문 · 연결할 줄로 이동 중";
            return false;
        }

        // 관문 주변에는 탄 줄도 잠시 남는다. 끝을 넘어갈 줄을 미리 골라 보호 범위 안에서 옮긴다.
        bool HandleProtectedExit(TightropeCourse course, NetworkPlayer other, float gapEnd, float alignmentDistance,
            ref PlayerInputFrame input)
        {
            float distance = course.GetDistance(player.transform.position);
            float protectedEnd = gapEnd + FireCourseSpans.GateMargin;
            if (distance >= protectedEnd) return false;
            float probe = Mathf.Min(course.FinishDistance - .1f, protectedEnd + .5f);
            int warningLane = TightropeCourse.NoLane;
            var fire = TightropeFire.Current;
            if (fire != null && fire.IsPerformanceActive && fire.Settings != null
                && fire.PendingSegment >= 0 && fire.PendingSegment < fire.Settings.segments.Length)
            {
                var warning = fire.Settings.segments[fire.PendingSegment];
                if (probe >= warning.from && probe < warning.to) warningLane = warning.lane - 1;
            }
            bool hasDanger = false;
            for (int i = 0; i < course.LaneCount; i++)
                if (i == warningLane || !course.IsLaneUsable(i, probe)) { hasDanger = true; break; }
            if (!hasDanger) return false;

            int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            int target = TightropeCourse.NoLane, closest = int.MaxValue;
            for (int i = 0; i < course.LaneCount; i++)
            {
                int difference = Mathf.Abs(i - lane);
                if (i == warningLane || !course.IsLaneUsable(i, probe) || difference >= closest) continue;
                target = i; closest = difference;
            }
            if (target == TightropeCourse.NoLane)
            { Status = "관문 끝 · 앞으로 이어지는 안전한 줄을 기다립니다"; return true; }
            if (target != lane)
            {
                if (distance < alignmentDistance - .2f) input.Move = 1f;
                else TryLaneJump(course, System.Math.Sign(target - lane), ref input);
                Status = "관문 끝 소각 구간을 피해 살아 있는 줄로 이동 중";
                return true;
            }
            input.Move = other == null || course.GetDistance(other.transform.position) > distance + 1.1f ? 1f : 0f;
            Status = input.Move != 0f ? "살아 있는 줄로 관문을 빠져나갑니다" : "안전한 줄에서 동료를 기다립니다";
            return true;
        }

        int RelayAssignment(TightropeCourse course, TightropeRelayGate relay, bool far)
        {
            int held = 0;
            for (int i = 0; i < relay.PoleCount; i++)
                if (relay.IsFarPole(i) == far && relay.HolderAt(i) != TightropePracticeBot.Nobody) held++;
            int needed = relay.RequiredHolders - held;
            if (needed <= 0) return -1;

            // 연습방 호스트는 사람이다. 이미 잡은 사람을 우선하고, 빈 자리는 봇 ClientId 순으로 맡는다.
            // 서로 다른 봇이 같은 봉을 향해 달리거나 사람이 먼저 잡기만을 기다리는 교착을 피한다.
            int rank = 0;
            foreach (var candidate in NetworkPlayer.All)
            {
                if (candidate == player || !RelayParticipant(candidate) || relay.IsHeld(candidate)
                    || candidate.OwnerClientId == NetworkManager.ServerClientId || candidate.OwnerClientId >= player.OwnerClientId) continue;
                float d = course.GetDistance(candidate.transform.position);
                if (far ? d >= relay.GapTo + .4f && d <= relay.ExitDistance + 5f
                    : d >= relay.EntryDistance - 5f && d < relay.GapFrom) rank++;
            }
            if (rank >= needed) return -1;
            for (int i = 0; i < relay.PoleCount; i++)
            {
                if (relay.IsFarPole(i) != far || !relay.IsPoleAvailable(i)) continue;
                if (rank-- == 0) return i;
            }
            return -1;
        }

        static bool RelayParticipant(NetworkPlayer candidate) => candidate != null && candidate.IsSpawned
            && candidate.State == PlayerState.Normal
            && (!candidate.TryGetComponent(out PlayerBalance candidateBalance) || !candidateBalance.HasFallen);

        static int RelayParticipantCount()
        {
            int count = 0;
            foreach (var candidate in NetworkPlayer.All) if (RelayParticipant(candidate)) count++;
            return count;
        }

        static bool RelayEveryoneAcross(TightropeCourse course, TightropeRelayGate relay)
        {
            foreach (var candidate in NetworkPlayer.All)
                if (RelayParticipant(candidate)
                    && (course.GetDistance(candidate.transform.position) < relay.GapTo + .6f
                        || (!relay.IsHeld(candidate) && candidate.IsAirborne))) return false;
            return true;
        }
    }
}
