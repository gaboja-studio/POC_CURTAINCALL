namespace CurtainCall.Sound
{
    /// <summary>
    /// 코드가 재생을 요청하는 사운드 시트 고유 ID. 새 상황을 연결할 때 여기 추가하고
    /// 사운드 설정 창에서 같은 ID로 큐를 만든다. 클립·음량은 코드에 두지 않는다.
    /// </summary>
    public static class SoundCueIds
    {
        /// <summary>UI-104 방송 배너음: 쉬는 시간 종료 60초 전 무대 복귀 소집 방송.</summary>
        public const string RecallBroadcast = "UI-104";
        /// <summary>UI-701 개인 사망음: 내 단원이 사망해 사망 카드가 뜰 때.</summary>
        public const string PersonalDeath = "UI-701";
        /// <summary>UI-905 타이머 30초 경고음: 휴식·정비 타이머가 30초 경고 상태로 바뀔 때.</summary>
        public const string TimerWarning = "UI-905";
        /// <summary>UI-906 마지막 10초 틱: 타이머 마지막 10초 동안 초마다.</summary>
        public const string TimerFinalTick = "UI-906";

        /// <summary>새 카탈로그를 만들 때 넣는 기본 항목(ID, 상황, 설명).</summary>
        public static readonly (string id, string situation, string description)[] Defaults =
        {
            (RecallBroadcast, "방송 배너음", "쉬는 시간 종료 60초 전 무대 복귀 소집 방송 배너. 임시 합성 종소리를 대체."),
            (PersonalDeath, "개인 사망음", "내 단원이 사망해 사망 카드가 뜰 때 재생."),
            (TimerWarning, "타이머 30초 경고음", "휴식·정비 타이머가 30초 경고 상태로 바뀔 때 재생."),
            (TimerFinalTick, "마지막 10초 틱", "타이머 마지막 10초 동안 초마다 재생."),
        };
    }
}
