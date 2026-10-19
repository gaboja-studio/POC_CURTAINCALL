using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
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
                    : "A/D 균형 잡기 · F 길게 눌러 목마 내리기\n마우스 시점 회전";
            if (onRope)
                return "W/S 앞뒤 이동 · A/D 균형 잡기 · Space 점프 · Q/E + Space 옆줄 점프\n"
                    + (carrying ? "F 길게 눌러 목마 해제" : "F 가까운 동료 목마 / 빈 자리에서 받기 준비·취소")
                    + " · 마우스 시점 회전\n청록색 띠 안에 옆줄 착지하면 균형 흔들림 감소";
            return "W/S 앞뒤 이동 · A/D 옆걸음 · Space 점프\n마우스 방향 전환 · 줄 위로 걸어가면 자동 탑승·3인칭";
        }

        public static void Draw()
        {
            var course = TightropeCourse.Current;
            var local = NetworkPlayer.Local;
            if (course == null || !course.IsTraversalActive || local == null || local.State != PlayerState.Normal
                || InputGate.IsBlocked) return;
            var mover = local.GetComponent<PlayerMover>();
            if (mover == null || !mover.ControlEnabled) return;
            var stack = PiggybackSystem.Current;
            bool passenger = stack != null && stack.GetBelow(local) != null;
            bool carrying = stack != null && stack.GetAbove(local) != null;
            string content = Describe(!mover.FreeMovement, passenger, !carrying, carrying);
            var framing = TightropeCameraFraming.Current;
            if (framing != null && framing.IsLocalViewActive)
                content += framing.CurrentMode == CameraViewKind.FirstPerson ? "   [1인칭]" : "   [3인칭]";
            var catches = TightropeCatchSystem.Current;
            if (catches != null && catches.LocalReadyRemaining > 0f)
                content += $"\n동료 받기 준비 {catches.LocalReadyRemaining:0.0}초 — 이동·점프하면 취소 (A/D 균형 유지)";
            if (catches != null && !string.IsNullOrEmpty(catches.LocalFeedback)) content += "\n" + catches.LocalFeedback;
            var saws = Saws.TightropeSaws.Current;
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
