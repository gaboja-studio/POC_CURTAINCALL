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

        public const string WarningBanner = "UI-102";
        public const string InteractionAccepted = "UI-302";
        public const string HangCountdown = "UI-502";
        public const string PerformanceSuccess = "UI-703";
        public const string PerformanceFailure = "UI-704";
        public const string BalanceWarning = "UI-901";
        public const string TimerExpired = "UI-907";
        public const string ButtonHover = "UI-1001";
        public const string ButtonClick = "UI-1002";
        public const string WindowOpened = "UI-1003";
        public const string WindowClosed = "UI-1004";
        public const string SettingsTab = "UI-1005";
        public const string VolumePreview = "UI-1006";
        public const string SettingsToggle = "UI-1007";
        public const string ScheduleTrick = "UI-1101";
        public const string ScheduleDifficulty = "UI-1102";
        public const string ShopSelection = "UI-1201";
        public const string ShopAccepted = "UI-1202";
        public const string ShopRejected = "UI-1203";
        public const string PerformanceGrandSuccess = "UI-1301";
        public const string RewardCoin = "UI-1302";
        public const string PursuitEscalation = "UI-1401";
        public const string PursuitHeartbeat = "UI-1402";
        public const string MicrophoneOn = "UI-1501";
        public const string MicrophoneOff = "UI-1501-off";
        public const string InteractionRejected = "UI-303";

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
            (WarningBanner, "경고 배너음", "사망·연결 실패·추격 시작 같은 빨간 삼각형 경고 배너가 뜰 때 재생."),
            (InteractionAccepted, "상호작용 확인 결정음", "서명·줍기·상점 열기 등 F 입력이 받아들여졌을 때 재생."),
            (HangCountdown, "매달림 카운트다운", "줄에 매달려 '남은 N초'가 표시되는 동안 초마다 재생."),
            (PerformanceSuccess, "공연 성공 결과", "결과 패널에 '공연 성공'이 뜰 때 재생."),
            (PerformanceFailure, "공연 실패 결과", "결과 패널에 '공연 실패'가 뜰 때 재생."),
            (BalanceWarning, "균형 위험 경고음", "외줄 균형 바늘이 주황·빨강 구간에 있는 동안 재생."),
            (TimerExpired, "타이머 종료음", "휴식 시간·탈출로 제한 시간이 0이 됐을 때 재생."),
            (ButtonHover, "버튼 마우스 올림음", "메인 메뉴·ESC 메뉴·설정·확인창의 공용 버튼에 마우스를 올릴 때 아주 작게 재생."),
            (ButtonClick, "버튼 클릭음", "메뉴·설정·확인창·결과창 버튼을 눌렀을 때 재생. 월드 F 입력(UI-302)과 구분."),
            (WindowOpened, "패널 열림음", "ESC 메뉴·설정 창·나가기 확인창이 열릴 때 재생."),
            (WindowClosed, "패널 닫힘음", "패널을 닫거나 확인창에서 취소해 이전 화면으로 돌아갈 때 재생."),
            (SettingsTab, "설정 탭 전환음", "설정 창에서 탭을 바꿀 때 재생."),
            (VolumePreview, "볼륨 미리듣기음", "설정에서 효과음·UI 볼륨 슬라이더를 조절할 때 바뀐 크기를 확인하도록 재생."),
            (SettingsToggle, "토글 전환음", "설정의 켜기·끄기 토글을 바꿀 때 재생."),
            (ScheduleTrick, "공연 변경음", "일정판 티켓 슬롯을 눌러 공연이 바뀌고 체크·테두리가 표시될 때 재생."),
            (ScheduleDifficulty, "난이도 변경음", "일정판 화살표로 공연 난이도를 바꿀 때 재생. 바꿀 수 없는 칸은 UI-303 사용."),
            (ShopSelection, "상점 탭·품목 선택음", "상점 패널에서 탭을 바꾸거나 품목을 고를 때 재생."),
            (ShopAccepted, "거래 완료음", "기물 구매·판매나 서비스 결제가 승인될 때 재생. 공동 자금 증감음(UI-507·508)과 함께 재생."),
            (ShopRejected, "거래 거절음", "공동 자금 부족 등으로 거래가 거절될 때 재생."),
            (PerformanceGrandSuccess, "공연 대성공 결과", "결과 패널에 '대성공' 리본이 뜰 때 재생. 공연 성공(UI-703)보다 화려하게."),
            (RewardCoin, "보상 동전 이동음", "결과창에서 보상 동전이 공동 자금 쪽으로 날아갈 때 동전마다 재생. (2~8개, 0.064초 간격)"),
            (PursuitEscalation, "추격 단계 상승음", "추격 시작 1분 뒤 시야 확대, 2분 뒤 벽 투시, 3분 뒤 최인접 추격처럼 추격 단계가 오를 때 알림과 함께 재생."),
            (PursuitHeartbeat, "추격자 근접 맥박", "추격자가 가까워져 화면 가장자리가 붉게 맥박치는 동안 재생. 가까울수록 크게. 대기실·탈출로 공용."),
            (MicrophoneOn, "마이크 켬", "단축키나 마이크 아이콘으로 내 마이크를 켤 때 재생."),
            (MicrophoneOff, "마이크 끔", "단축키나 마이크 아이콘으로 내 마이크를 끌 때 재생. UI-1501 끔 변형."),
            (InteractionRejected, "상호작용 거부음", "현재 일정 칸의 난이도를 변경할 수 없을 때 재생."),
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
