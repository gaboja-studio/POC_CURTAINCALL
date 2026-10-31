using CurtainCall.Settings;

namespace CurtainCall.Campaign
{
    /// <summary>개발용 시작 시점. 값은 끝에만 추가한다. Escape: 대기실 합류를 건너뛰고 탈출로로 바로 간다(Task-089).</summary>
    public enum RunStartPhase { Performance, Morning, Between, Night, Escape }

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
            else if (Phase < RunStartPhase.Performance || Phase > RunStartPhase.Escape) reason = "선택할 수 없는 시점입니다.";
            else if (Phase == RunStartPhase.Escape) { } // 탈출은 어느 날 쉬는 시간에서든 시작할 수 있다. 공연 선택은 쓰지 않는다.
            else if (Day > rules.DayCount && Phase != RunStartPhase.Morning) reason = "마지막 날에는 아침 쉬는 시간만 있습니다.";
            else if (Phase == RunStartPhase.Morning && Day == 1) reason = "1일차는 아침 쉬는 시간 없이 공연으로 시작합니다.";
            else if (Phase == RunStartPhase.Performance && (PerformanceIndex < 0 || PerformanceIndex >= rules.PerformancesPerDay)) reason = "공연 순번을 확인해 주세요.";
            else if (Phase == RunStartPhase.Between && (PerformanceIndex < 1 || PerformanceIndex >= rules.PerformancesPerDay)) reason = "공연 사이 쉬는 시간의 다음 공연을 선택해 주세요.";
            else if (Phase != RunStartPhase.Night && Day <= rules.DayCount && TrickId != "Tightrope" && TrickId != "HumanCannon") reason = "구현된 공연을 선택해 주세요.";
            return reason.Length == 0;
        }

        public CampaignPhase TargetPhase => Phase == RunStartPhase.Performance ? CampaignPhase.Performance
            : Phase == RunStartPhase.Night ? CampaignPhase.Night
            : Phase == RunStartPhase.Escape ? CampaignPhase.Escape : CampaignPhase.RestTime;
    }
}
