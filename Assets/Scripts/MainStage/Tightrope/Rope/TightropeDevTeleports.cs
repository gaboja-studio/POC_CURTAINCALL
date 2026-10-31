using System.Collections.Generic;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// Dev 치트 창(F9)의 외줄 순간이동 지점(Task-089): 출발 플랫폼, 구간마다 시작 지점(줄 위), 도착 플랫폼.
    /// 내 자리 줄을 우선 쓰고, 그 거리에서 끊긴 줄(그네 구간·막힌 줄)이면 쓸 수 있는 다른 줄로 바꾼다. 코스·판정 값은 바꾸지 않는다.
    /// </summary>
    static class TightropeDevTeleports
    {
        const float SectionEntryOffset = .5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => DevCheats.RegisterTeleportSource(Collect);

        static void Collect(NetworkPlayer local, List<DevCheats.TeleportPoint> points)
        {
            var course = TightropeCourse.Current;
            if (course == null || !course.isActiveAndEnabled || course.LaneCount <= 0) return;
            int slot = Mathf.Max(0, local.Slot);
            var facing = Quaternion.LookRotation(course.Forward, Vector3.up);
            var start = course.GetStartPose(slot);
            points.Add(new DevCheats.TeleportPoint("외줄 · 출발", start.position, start.rotation));
            var sections = course.Shape.Sections;
            if (sections != null)
            {
                float from = 0f;
                for (int i = 0; i < sections.Length; i++)
                {
                    float distance = Mathf.Min(from + SectionEntryOffset, course.FinishDistance - SectionEntryOffset);
                    if (TryUsableLane(course, slot % course.LaneCount, distance, out int lane))
                    {
                        string name = string.IsNullOrWhiteSpace(sections[i].label) ? $"{i + 1}구간" : sections[i].label;
                        points.Add(new DevCheats.TeleportPoint($"외줄 · {name} 시작", course.GetLanePosition(lane, distance), facing));
                    }
                    from = Mathf.Clamp(sections[i].endFraction * course.FinishDistance, from, course.FinishDistance);
                }
            }
            var finish = course.GetFinishPose(slot);
            points.Add(new DevCheats.TeleportPoint("외줄 · 도착", finish.position, finish.rotation));
        }

        static bool TryUsableLane(TightropeCourse course, int preferred, float distance, out int lane)
        {
            for (int offset = 0; offset < course.LaneCount; offset++)
            {
                lane = (preferred + offset) % course.LaneCount;
                if (course.IsLaneUsable(lane, distance)) return true;
            }
            lane = -1;
            return false;
        }
    }
}
