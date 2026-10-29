using CurtainCall.Campaign;
using CurtainCall.UI.RestTimerV28;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>런의 실제 타이머 상태를 읽는다. 게임 시각·표시 시간 경과로 초를 추정하지 않는다.</summary>
    public static class RestTimer
    {
        public static RestTimerSnapshot Read(in CampaignSnapshot state, float durationSeconds)
        {
            bool rest = state.Phase == CampaignPhase.RestTime;
            bool night = state.Phase == CampaignPhase.Night;
            var mode = RestTimerMode.Unavailable;
            if (rest || night)
                mode = state.Timer == RestTimerState.Unlimited ? RestTimerMode.Unlimited
                    : state.Timer == RestTimerState.Running || state.Timer == RestTimerState.Stopped
                        ? RestTimerMode.Limited : RestTimerMode.Unavailable;
            // Campaign은 별도 시각 필드 없이 Morning/공연 사이 쉬는 시간/Night로 시간대를 확정한다.
            var period = night ? RestTimeOfDay.Night : !rest ? RestTimeOfDay.Unknown
                : state.Rest == RestKind.Morning ? RestTimeOfDay.Morning
                : state.Rest == RestKind.Guide || state.Rest == RestKind.Between ? RestTimeOfDay.Day : RestTimeOfDay.Unknown;
            return new RestTimerSnapshot(rest || night, state.Day, period, mode,
                state.TimeRemaining, durationSeconds, state.Timer == RestTimerState.Stopped);
        }
    }
}
