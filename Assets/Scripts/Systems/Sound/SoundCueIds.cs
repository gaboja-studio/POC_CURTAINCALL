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

        public const string EscapeWallCollapse = "ESC-002";
        public const string EscapeFootstep = "ESC-003";
        public const string EscapeRoar = "ESC-005";
        public const string EscapeLanding = "ESC-006";
        public const string EscapeArmWarning = "ESC-007";
        public const string EscapeGrab = "ESC-009";
        public const string EscapeThrow = "ESC-010";
        public const string EscapeRopeWarning = "ESC-011";
        public const string EscapeRopeDisappear = "ESC-012";
        public const string EscapeRopeRestore = "ESC-013";
        public const string EscapeFireWarning = "ESC-014";
        public const string EscapeFireIgnition = "ESC-015";
        public const string EscapeFireBurning = "ESC-016";
        public const string EscapeBridgeDrive = "ESC-018";
        public const string EscapeBridgeReady = "ESC-019";
        public const string EscapeSawLaunch = "ESC-022";
        public const string EscapeDoorOpen = "ESC-027";

        /// <summary>새 카탈로그를 만들 때 넣는 기본 항목(ID, 상황, 설명).</summary>
        public static readonly (string id, string situation, string description)[] Defaults =
        {
            (RecallBroadcast, "방송 배너음", "쉬는 시간 종료 60초 전 무대 복귀 소집 방송 배너. 임시 합성 종소리를 대체."),
            (PersonalDeath, "개인 사망음", "내 단원이 사망해 사망 카드가 뜰 때 재생."),
            (TimerWarning, "타이머 30초 경고음", "휴식·정비 타이머가 30초 경고 상태로 바뀔 때 재생."),
            (TimerFinalTick, "마지막 10초 틱", "타이머 마지막 10초 동안 초마다 재생."),
            (EscapeWallCollapse, "추격자 등장벽 붕괴", "벽이 무너지는 순간. 콘크리트 파편 잔향 포함."),
            (EscapeFootstep, "추격자 걷기 발소리", "추격자의 발이 바닥에 닿는 순간."),
            (EscapeRoar, "추격자 포효", "첫 등장 후 돌진을 예고할 때."),
            (EscapeLanding, "추격자 도약 착지", "도약 후 바닥에 내려앉는 순간."),
            (EscapeArmWarning, "추격자 팔 공격 예고", "팔 공격의 대상과 방향이 표시될 때."),
            (EscapeGrab, "추격자 포획", "뻗은 손이 플레이어를 붙잡는 순간."),
            (EscapeThrow, "추격자 투척", "손에서 플레이어가 놓이는 순간."),
            (EscapeRopeWarning, "소실 외줄 전기 경고", "외줄 소실 예고의 전기 점멸마다."),
            (EscapeRopeDisappear, "외줄 소실", "외줄이 타며 사라지는 소실 연출(약 1초)이 시작되는 순간. 연출이 끝나면 지지가 사라진다."),
            (EscapeRopeRestore, "외줄 복원", "사라진 외줄이 다시 나타나는 순간."),
            (EscapeFireWarning, "후반 추격불 시작 경고", "후반 추격불의 점화 예고 중."),
            (EscapeFireIgnition, "추격불 구간 점화", "각 화염벽 구간이 점화되는 순간."),
            (EscapeFireBurning, "추격불 화염벽 연소", "점화 후 이어지는 공간 연소 루프."),
            (EscapeBridgeDrive, "접이교 전개 구동", "접이교가 펼쳐지는 동안의 구동 루프."),
            (EscapeBridgeReady, "접이교 전개 완료", "접이교가 완전히 펼쳐져 통과 가능해질 때."),
            (EscapeSawLaunch, "휴대 톱 발사", "원형 톱날이 발사되는 순간."),
            (EscapeDoorOpen, "출구 셔터 개방", "최종 출구가 실제로 열리기 시작할 때."),
        };
    }
}
