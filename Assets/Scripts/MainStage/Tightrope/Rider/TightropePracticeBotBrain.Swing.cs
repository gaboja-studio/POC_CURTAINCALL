using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropePracticeBotBrain
    {
        bool HandleSwingGap(TightropeCourse course, NetworkPlayer other, ref PlayerInputFrame input)
        {
            var pole = TightropeSwingPole.Current?.ForPlayer(player);
            if (pole == null || !course.HasSwingGap || !course.IsGameplayActive) return false;
            if (pole.IsHeld(player))
            {
                input.Move = 1f; input.ActionPressed = pole.CanLetGo;
                Status = "회전봉 반동 · 초록색에서 놓기"; return true;
            }
            if (pole.IsCrossing(player))
            { input.Move = 1f; input.Posture = 0f; Status = "공중 전진 · 동료 쪽으로 착지"; return true; }
            if (player.IsAirborne || mover.IsAirborne || PiggybackSystem.Current?.IsInStack(player) == true) return false;
            int currentLane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            if (!course.HasSwingGapOnLane(currentLane)) return false;
            float distance = course.GetDistance(player.transform.position), partnerDistance = course.GetDistance(other.transform.position);
            if (distance >= course.SwingGapFrom - 4f && distance < course.SwingGapFrom)
            {
                input.Posture = Correction(balance != null ? balance.Value : 0f);
                float gap = course.SwingGapFrom - SwingPoleMath.EntryOffset - distance;
                input.Move = gap > .25f ? 1f : gap < -.25f ? -1f : 0f;
                if (Mathf.Abs(gap) <= .4f && !pole.IsOccupied && Time.time >= nextInteractAt)
                { input.InteractTapped = true; nextInteractAt = Time.time + .8f; }
                Status = "끊어진 줄 · 회전봉 잡기 대기"; return true;
            }
            if (distance >= course.SwingGapTo && partnerDistance < course.SwingGapTo)
            {
                float gap = pole.LandingDistance - distance;
                input.Move = Mathf.Abs(gap) > .2f ? Mathf.Sign(gap) : 0f;
                input.Posture = Correction(balance != null ? balance.Value : 0f);
                var catches = TightropeCatchSystem.Current;
                if (input.Move == 0f && catches != null && !catches.IsReady(player) && pole.IsCrossing(other) && Time.time >= nextInteractAt)
                { input.InteractTapped = true; nextInteractAt = Time.time + .8f; }
                Status = "먼저 건넜어요 · 동료 받기 준비"; return true;
            }
            return false;
        }
    }
}
