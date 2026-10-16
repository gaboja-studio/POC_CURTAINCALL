using System;

namespace CurtainCall.HumanCannon
{
    public enum FinalSawPhase : byte { Dormant, Warning, Active }

    /// <summary>한 공유 주기의 단일 수직 톱 상태. SecondsToNext는 다음 단계까지 남은 시간이다.</summary>
    public readonly struct FinalSawSample
    {
        public readonly FinalSawPhase Phase;
        public readonly long CycleIndex;
        public readonly float Travel01;
        public readonly double SecondsToNext;

        public FinalSawSample(FinalSawPhase phase, long cycleIndex, float travel01, double secondsToNext)
        {
            Phase = phase;
            CycleIndex = cycleIndex;
            Travel01 = travel01;
            SecondsToNext = secondsToNext;
        }
    }

    /// <summary>
    /// 최초 진입 시각 하나로 모든 화면의 예고와 등속 이동을 계산한다.
    /// 후발 진입·발사는 시작 시각을 바꾸지 않으며 새 공연/연습 재시도 때만 Reset한다.
    /// </summary>
    public sealed class FinalSawTimeline
    {
        public bool Started { get; private set; }
        public double StartedAt { get; private set; }

        public bool TryStart(double now)
        {
            if (Started || !ValidTime(now)) return false;
            Started = true;
            StartedAt = now;
            return true;
        }

        public void Reset()
        {
            Started = false;
            StartedAt = 0;
        }

        /// <summary>
        /// Warning [주기 시작, 예고 끝), Active [예고 끝, 퇴장)로 경계를 나눈다.
        /// 정확한 퇴장 시각에는 다음 주기의 예고가 시작되어 톱이 겹치지 않는다.
        /// 늦게 받은 스냅샷도 startedAt을 그대로 사용하며 프레임 수를 누적하지 않는다.
        /// 미시작·미래 시작 시각·잘못된 수치에는 판정 없는 Dormant를 반환한다.
        /// </summary>
        public static FinalSawSample Sample(bool started, double startedAt, double now,
            float warningSeconds, float travelSeconds)
        {
            var dormant = new FinalSawSample(FinalSawPhase.Dormant, -1, 0, 0);
            if (!started || !ValidTime(startedAt) || !ValidTime(now) || now < startedAt
                || !ValidDuration(warningSeconds) || !ValidDuration(travelSeconds)) return dormant;

            double period = (double)warningSeconds + travelSeconds;
            // 두 구간 중 하나가 부동소수점 정밀도로 사라지는 설정은 허용하지 않는다.
            if (period <= warningSeconds || period <= travelSeconds) return dormant;
            double elapsed = now - startedAt;
            double cycle = Math.Floor(elapsed / period);
            if (double.IsInfinity(cycle) || cycle >= long.MaxValue) return dormant;

            double inCycle = elapsed % period;
            long cycleIndex = (long)cycle;
            if (inCycle < warningSeconds)
                return new FinalSawSample(FinalSawPhase.Warning, cycleIndex, 0, warningSeconds - inCycle);

            float travel01 = (float)((inCycle - warningSeconds) / travelSeconds);
            return new FinalSawSample(FinalSawPhase.Active, cycleIndex, travel01, period - inCycle);
        }

        static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        static bool ValidDuration(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
    }
}
