using System;
using CurtainCall.Campaign;
using CurtainCall.UI.DayClockV2;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>런의 실제 남은 초와 설정된 하루 시각을 분리해 프리팹에 전달한다.</summary>
    public static class DayClock
    {
        public static bool TryRead(in CampaignSnapshot state, float totalSeconds,
            DayClockCompiledSchedule schedule, out DayClockSnapshot snapshot)
        {
            snapshot = default;
            if (schedule == null) return false;
            DayClockPhase phase;
            switch (state.Phase)
            {
                case CampaignPhase.RestTime:
                    phase = state.Rest == RestKind.Morning ? DayClockPhase.MorningBreak : DayClockPhase.NoonBreak;
                    break;
                case CampaignPhase.Performance:
                    phase = state.PerformanceIndex == 0 ? DayClockPhase.MorningShow : DayClockPhase.AfternoonShow;
                    break;
                case CampaignPhase.Night: phase = DayClockPhase.Maintenance; break;
                case CampaignPhase.Dawn: phase = DayClockPhase.Sleep; break;
                default: return false;
            }
            bool timedPhase = state.Phase == CampaignPhase.RestTime || state.Phase == CampaignPhase.Night;
            bool unlimited = timedPhase && state.Timer == RestTimerState.Unlimited;
            bool hasRemaining = timedPhase && (state.Timer == RestTimerState.Running || state.Timer == RestTimerState.Stopped);
            bool hasTime = TryRange(schedule, phase, out int start, out int duration);
            double progress = hasRemaining && totalSeconds > 0 && DayClockScheduleMath.IsFinite(totalSeconds)
                && DayClockScheduleMath.IsFinite(state.TimeRemaining)
                ? Math.Clamp(1d - state.TimeRemaining / totalSeconds, 0d, 1d) : 0d;
            // 시작 포함·끝 제외. 완료 프레임에도 다음 단계의 영역을 가리키지 않는다.
            double minute = (start + Math.Min(duration * progress, Math.Max(0, duration - .001d))) % 1440d;
            snapshot = new DayClockSnapshot(true, state.Day, phase, minute, hasTime,
                state.TimeRemaining, hasRemaining, state.Timer == RestTimerState.Stopped)
            { isUnlimited = unlimited };
            return true;
        }

        public static bool TryRange(DayClockCompiledSchedule schedule, DayClockPhase phase, out int start, out int duration)
        {
            start = duration = 0;
            if (schedule == null) return false;
            bool found = false;
            int firstEnd = 0, lastStart = 0;
            foreach (var part in schedule.intervals)
            {
                if (part.phase != phase) continue;
                if (!found) { start = part.startMinute; firstEnd = part.endMinute; }
                lastStart = part.startMinute;
                duration += part.endMinute - part.startMinute;
                found = true;
            }
            // 자정을 통과하는 명시 구간은 [0,end), [start,1440)로 분리돼 있다.
            if (found && phase != DayClockPhase.Sleep && start == 0 && duration > firstEnd)
                start = lastStart;
            if (phase == DayClockPhase.Sleep)
            {
                // 수면은 밤 정비 끝에서 시작하는 여집합 구간을 우선한다.
                foreach (var part in schedule.intervals)
                    if (part.phase == DayClockPhase.Maintenance && part.endMinute < 1440)
                        foreach (var sleep in schedule.intervals)
                            if (sleep.phase == DayClockPhase.Sleep && sleep.startMinute == part.endMinute)
                            { start = sleep.startMinute; duration = sleep.endMinute - sleep.startMinute; return true; }
            }
            return found;
        }
    }
}
