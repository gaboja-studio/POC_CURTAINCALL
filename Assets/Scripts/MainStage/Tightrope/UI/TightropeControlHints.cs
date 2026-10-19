using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Tightrope.UI
{
    /// <summary>현재 조작 상태에 맞는 하단 안내. 구간 표시나 착지 예측은 표시하지 않는다.</summary>
    public static class TightropeControlHints
    {
        static GUIStyle style;

        public static string Describe(bool onRope, bool passenger, bool top, bool carrying)
        {
            if (passenger)
                return top
                    ? "A/D 균형 잡기 · Space 점프 · W/S + Space 앞뒤로 내리기 · Q/E + Space 옆줄로 내리기\nF 길게 눌러 목마 내리기 · 마우스 시점 회전"
                    : "A/D 균형 잡기 · 맨 위 동료부터 차례로 내리기\n마우스 시점 회전";
            if (onRope)
                return "W/S 앞뒤 이동 · A/D 균형 잡기 · Space 점프 · Q/E + Space 옆줄 점프\n"
                    + (carrying ? "위 동료가 F를 길게 눌러 내리기" : "F 가까운 동료 목마 / 빈 자리에서 받기 준비·취소")
                    + " · 마우스 시점 회전\n봉에서 날아갈 때만 WASD 공중 조절 · 청록색 띠 안에 착지하면 균형 흔들림 감소";
            return "W/S 앞뒤 이동 · A/D 옆걸음 · Space 점프\n마우스 방향 전환 · 줄 위로 걸어가면 자동 탑승·3인칭";
        }

        public static string DescribeGate(TightropeSaws saws, NetworkPlayer local)
        {
            if (saws == null || !saws.IsCooperativeGateActive) return null;
            if (saws.IsFinaleSoloGate)
                return saws.IsCooperativeGatePassable
                    ? $"최후반 관문 열림 {saws.FinaleGateOpenRemaining:0.0}초 · 열린 순간에 통과하세요"
                    : $"최후반 관문 {saws.FinaleGateUntilOpen:0.0}초 뒤 열림";
            if (saws.IsCooperativeGatePassable)
                return "톱이 뛰었어요 · 1층은 앞으로 통과!\n정면 톱은 닿으면 즉사 · 위층은 앞쪽에서 기다려 주세요";
            if (!saws.IsCooperativeStackApproved)
                return $"최후반 정면 톱 · 출발 표시에 {saws.CooperativeRequiredStackSize}명 목마로 모이세요\n"
                    + "4층 → 3층 → 2층 순서로 W+Space · 뒤 버튼은 2층이 마지막에";
            int floor = local != null ? saws.CooperativeAdmittedFloor(local) : 0;
            string action = floor switch
            {
                >= 3 => $"이번 목마의 {floor}층 · 맨 위가 되면 W+Space, 건넌 뒤 버튼을 피해서 앞쪽에서 대기",
                2 => "이번 목마의 2층 · 4·3층이 먼저 넘으면 W+Space로 뒤 버튼을 밟으세요",
                1 => "이번 목마의 1층 · 그대로 기다렸다가 2층이 버튼을 눌러 톱이 뛰면 전진",
                _ => "맨 위부터 4 → 3 → 2층 순서 · 뒤 버튼은 원래 2층 담당"
            };
            return action + "\n톱은 2.25m만 뛰어요 · 3·4층이 먼저 버튼을 밟으면 남은 위층이 즉사할 수 있어요";
        }

        public static void Draw()
        {
            var course = TightropeCourse.Current;
            var local = NetworkPlayer.Local;
            if (course == null || !course.IsTraversalActive || local == null || local.State != PlayerState.Normal
                || InputGate.IsBlocked) return;
            var mover = local.GetComponent<PlayerMover>();
            var relay = TightropeRelayGate.Current;
            var trapeze = TightropeTrapeze.Current;
            bool relayHeld = relay != null && relay.IsHeld(local);
            if (mover == null || (!mover.ControlEnabled && !relayHeld)) return;
            var stack = PiggybackSystem.Current;
            bool passenger = stack != null && stack.GetBelow(local) != null;
            bool carrying = stack != null && stack.GetAbove(local) != null;
            string content = Describe(!mover.FreeMovement, passenger, !carrying, carrying);
            string relayHint = relay != null ? relay.LocalHint : null;
            string trapezeHint = trapeze != null ? trapeze.LocalHint : null;
            var swing = TightropeSwingPole.Current;
            if (!string.IsNullOrEmpty(trapezeHint))
                content = trapeze.IsCrossing(local) ? trapezeHint : content + "\n" + trapezeHint;
            else if (!string.IsNullOrEmpty(relayHint))
                content = relayHeld ? relayHint : content + "\n" + relayHint;
            else
            {
                if (swing != null && swing.IsHeld(local)) content = swing.LocalHint;
                else if (swing != null && !string.IsNullOrEmpty(swing.LocalHint)) content += "\n" + swing.LocalHint;
                if (swing != null && !string.IsNullOrEmpty(swing.LocalFeedback)) content += "\n" + swing.LocalFeedback;
            }
            if (relay != null && !string.IsNullOrEmpty(relay.LocalFeedback)) content += "\n" + relay.LocalFeedback;
            var framing = TightropeCameraFraming.Current;
            if (framing != null && framing.IsLocalViewActive)
                content += framing.CurrentMode == CameraViewKind.FirstPerson ? "   [1인칭]" : "   [3인칭]";
            var catches = TightropeCatchSystem.Current;
            if (catches != null && catches.LocalReadyRemaining > 0f)
                content += $"\n동료 받기 준비 {catches.LocalReadyRemaining:0.0}초 — 이동·점프하면 취소 (A/D 균형 유지)";
            if (catches != null && !string.IsNullOrEmpty(catches.LocalFeedback)) content += "\n" + catches.LocalFeedback;
            if (trapeze != null && !string.IsNullOrEmpty(trapeze.LocalFeedback)) content += "\n" + trapeze.LocalFeedback;
            var saws = Saws.TightropeSaws.Current;
            var waves = TightropeLandingWaves.Current;
            if (waves != null && !string.IsNullOrEmpty(waves.LocalFeedback)) content += "\n" + waves.LocalFeedback;
            string gateHint = DescribeGate(saws, local);
            if (!string.IsNullOrEmpty(gateHint)) content += "\n" + gateHint;
            if (saws != null && saws.IsTeamPassageOpen)
                content += $"\n{(saws.IsTeamPassageWarning ? "주변 가로 톱 재가동 주의" : "주변 가로 톱 정지")} {saws.TeamPassageRemaining:0.0}초 · {saws.TeamPassageStartDistance:0}~{saws.TeamPassageEndDistance:0}m";
            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(12, 12, 8, 8)
            };
            style.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 48f), 12, 22);
            float width = Mathf.Min(1000f, Screen.width - 24f);
            float height = style.CalcHeight(new GUIContent(content), width);
            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - height - 16f, width, height), content, style);
        }
    }
}
