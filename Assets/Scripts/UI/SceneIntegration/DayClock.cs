using CurtainCall.Campaign;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>원형 하루 시계 한 장면. 표시만 하며 게임 시간을 바꾸지 않는다.</summary>
    public readonly struct DayClockReading
    {
        public readonly int Day;
        /// <summary>하루 시각(시, 0~24 소수).</summary>
        public readonly float Hours;
        public readonly string Period;
        public readonly bool HasTimer;
        public readonly float Remaining;
        /// <summary>남은 쉬는 시간 비율(1 → 0).</summary>
        public readonly float RemainingRatio;
        public readonly bool Alarm;

        public DayClockReading(int day, float hours, string period, bool hasTimer, float remaining, float remainingRatio, bool alarm)
        {
            Day = day; Hours = hours; Period = period; HasTimer = hasTimer;
            Remaining = remaining; RemainingRatio = remainingRatio; Alarm = alarm;
        }

        /// <summary>"오전"/"오후".</summary>
        public string Meridiem => Hours < 12f ? "오전" : "오후";

        /// <summary>12시간제 "h:mm".</summary>
        public string TimeText
        {
            get
            {
                int minutes = (int)System.Math.Floor(Hours * 60f + .0001f);
                int hour = minutes / 60 % 12;
                return $"{(hour == 0 ? 12 : hour)}:{minutes % 60:00}";
            }
        }

        public string RemainingText
        {
            get
            {
                int seconds = (int)System.Math.Ceiling(System.Math.Max(0f, Remaining));
                return $"{seconds / 60}:{seconds % 60:00}";
            }
        }
    }

    /// <summary>
    /// M2 하루 시계(게임 시스템 변경점 3-1). 오전 8~10시 아침 쉬는 시간, 10~12시 오전 공연, 12~14시 낮 쉬는 시간,
    /// 14~16시 오후 공연, 16~20시 밤 정비(타이머 없음), 그 외 수면. 쉬는 시간 실제 5분이 시계 2시간이다.
    /// 1일차는 오전 공연부터, 마지막 날 다음 날은 아침 쉬는 시간만 있다(런 진행이 정한다).
    /// </summary>
    public static class DayClock
    {
        public const float MorningRest = 8f, MorningShow = 10f, NoonRest = 12f, AfternoonShow = 14f, Night = 16f, Sleep = 20f;
        const float RestHours = 2f;

        /// <summary>하루 시간대 구간(시작·끝 시각과 종류). 12시간 원판에 한 바퀴로 그린다.</summary>
        public static readonly (float start, float end, DayClockBand band)[] Bands =
        {
            (MorningRest, MorningShow, DayClockBand.Rest),
            (MorningShow, NoonRest, DayClockBand.Performance),
            (NoonRest, AfternoonShow, DayClockBand.Rest),
            (AfternoonShow, Night, DayClockBand.Performance),
            (Night, Sleep, DayClockBand.Night),
        };

        /// <summary>런 상태를 하루 시계로 바꾼다. 하루 시계가 없는 시간대(로비·도입·탈출·엔딩)면 false.</summary>
        public static bool TryRead(in CampaignSnapshot s, float restTotal, out DayClockReading reading)
        {
            reading = default;
            switch (s.Phase)
            {
                case CampaignPhase.RestTime:
                {
                    bool morning = s.Rest == RestKind.Morning;
                    float ratio = restTotal > 0 ? Clamp01(s.TimeRemaining / restTotal) : 0f;
                    float start = morning ? MorningRest : NoonRest;
                    reading = new DayClockReading(s.Day, start + RestHours * (1f - ratio), morning ? "아침 쉬는 시간" : "낮 쉬는 시간",
                        s.Timer == RestTimerState.Running, s.TimeRemaining, ratio, s.AlarmRaised);
                    return true;
                }
                case CampaignPhase.Performance:
                    reading = new DayClockReading(s.Day, s.PerformanceIndex == 0 ? MorningShow : AfternoonShow,
                        s.PerformanceIndex == 0 ? "오전 공연" : "오후 공연", false, 0f, 0f, false);
                    return true;
                case CampaignPhase.Night:
                    reading = new DayClockReading(s.Day, Night, "밤 정비", false, 0f, 0f, false);
                    return true;
                case CampaignPhase.Dawn:
                    reading = new DayClockReading(s.Day, Sleep, "수면", false, 0f, 0f, false);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>12시간 원판에서 12시 방향부터 시계 방향 각도(도).</summary>
        public static float Angle(float hours) => hours % 12f / 12f * 360f;

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    public enum DayClockBand : byte { Rest, Performance, Night }
}
