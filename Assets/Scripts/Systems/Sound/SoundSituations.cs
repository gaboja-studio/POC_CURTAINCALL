namespace CurtainCall.Sound
{
    /// <summary>
    /// 코드가 소리를 요청하는 게임 상황 키. 재생: <c>SoundManager.Play(SoundSituations.RecallBroadcast)</c>.
    /// 상황마다 어떤 시트 ID를 낼지·켜고 끄기·음량 배율은 사운드 설정 창 '적용 상황' 탭에서 바꾼다(코드 수정 없음).
    /// 새 재생 지점을 만들 때 여기 상수를 추가하고 <see cref="Defaults"/>에도 넣는다.
    /// </summary>
    public static class SoundSituations
    {
        /// <summary>쉬는 시간 종료 60초 전 소집 경보(ExplorationTimerView).</summary>
        public const string RecallBroadcast = "waiting-room.recall-broadcast";
        /// <summary>공연 중 내 단원 사망 카드(PerformanceEndingPresentation).</summary>
        public const string PerformanceLocalDeath = "performance.local-death";
        /// <summary>대기실에서 내 단원 사망 카드(CommonWaitingRoomUI).</summary>
        public const string WaitingRoomLocalDeath = "waiting-room.local-death";
        /// <summary>탈출로에서 내 단원 사망·포획 확정(카드 없이 관전 전환, EscapeLocalDeathSound).</summary>
        public const string EscapeLocalDeath = "escape.local-death";
        /// <summary>휴식·밤 정비 타이머 30초 경고 진입(RestTimerView).</summary>
        public const string RestTimerWarning = "rest-timer.warning";
        /// <summary>휴식·밤 정비 타이머 마지막 10초 매초(RestTimerView).</summary>
        public const string RestTimerFinalTick = "rest-timer.final-tick";

        /// <summary>카탈로그에 없을 때 채우는 기본 상황(키, 이름, 설명, 시트 ID).</summary>
        public static readonly (string key, string label, string description, string cueId)[] Defaults =
        {
            (RecallBroadcast, "소집 경보 방송", "쉬는 시간 종료 60초 전 무대 복귀 소집 배너가 뜰 때. ExplorationTimerView.Show", SoundCueIds.RecallBroadcast),
            (PerformanceLocalDeath, "공연 중 내 사망", "공연 중 내 단원이 죽어 '사망했습니다' 카드가 뜰 때. PerformanceEndingPresentation.ObserveDeath", SoundCueIds.PersonalDeath),
            (WaitingRoomLocalDeath, "대기실 내 사망", "대기실에서 내 단원이 죽어 '나 사망' 카드가 뜰 때. CommonWaitingRoomUI.ObserveDeaths", SoundCueIds.PersonalDeath),
            (EscapeLocalDeath, "탈출로 내 사망·포획", "탈출로에서 내 단원이 사망·포획돼 관전으로 바뀔 때(카드 없음). EscapeLocalDeathSound", SoundCueIds.PersonalDeath),
            (RestTimerWarning, "타이머 30초 경고", "휴식·밤 정비 타이머가 30초 경고(붉은 테두리) 상태로 바뀔 때. RestTimerView.Show", SoundCueIds.TimerWarning),
            (RestTimerFinalTick, "타이머 마지막 10초 틱", "휴식·밤 정비 타이머 10초~1초 동안 초마다. RestTimerView.Show", SoundCueIds.TimerFinalTick),
        };
    }
}
