using CurtainCall.Settings;

namespace CurtainCall.Campaign
{
    public enum RunStartPhase { Performance, Morning, Between, Night }

    /// <summary>개발용 새 런의 시작 지점. 진행 중 스냅샷을 임의 수정하지 않고 정상 진입 상태를 만든다.</summary>
    public readonly struct RunStartPoint
    {
        public readonly int Day, PerformanceIndex;
        public readonly RunStartPhase Phase;
        public readonly string TrickId;
        public RunStartPoint(int day, RunStartPhase phase, int performanceIndex, string trickId)
        { Day = day; Phase = phase; PerformanceIndex = performanceIndex; TrickId = trickId; }

        public bool IsValid(out string reason)
        {
            var rules = GameSettings.Base.Run;
            reason = "";
            if (Day < 1 || Day > rules.DayCount + 1) reason = "선택할 수 없는 일차입니다.";
            else if (Phase < RunStartPhase.Performance || Phase > RunStartPhase.Night) reason = "선택할 수 없는 시점입니다.";
            else if (Day > rules.DayCount && Phase != RunStartPhase.Morning) reason = "마지막 날에는 아침 쉬는 시간만 있습니다.";
            else if (Phase == RunStartPhase.Morning && Day == 1) reason = "1일차는 아침 쉬는 시간 없이 공연으로 시작합니다.";
            else if (Phase == RunStartPhase.Performance && (PerformanceIndex < 0 || PerformanceIndex >= rules.PerformancesPerDay)) reason = "공연 순번을 확인해 주세요.";
            else if (Phase == RunStartPhase.Between && (PerformanceIndex < 1 || PerformanceIndex >= rules.PerformancesPerDay)) reason = "공연 사이 쉬는 시간의 다음 공연을 선택해 주세요.";
            else if (Phase != RunStartPhase.Night && Day <= rules.DayCount && TrickId != "Tightrope" && TrickId != "HumanCannon") reason = "구현된 공연을 선택해 주세요.";
            return reason.Length == 0;
        }

        public CampaignPhase TargetPhase => Phase == RunStartPhase.Performance ? CampaignPhase.Performance
            : Phase == RunStartPhase.Night ? CampaignPhase.Night : CampaignPhase.RestTime;
    }
}
