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

        public const string WarningBanner = "hud.warning-banner";
        public const string InteractionAccepted = "interaction.accepted";
        public const string HangCountdown = "tightrope.hang-countdown";
        public const string PerformanceSuccess = "results.success";
        public const string PerformanceFailure = "results.failure";
        public const string BalanceWarning = "tightrope.balance-warning";
        public const string TimerExpired = "rest-timer.expired";
        public const string ButtonHover = "menu.button-hover";
        public const string ButtonClick = "menu.button-click";
        public const string WindowOpened = "menu.window-opened";
        public const string WindowClosed = "menu.window-closed";
        public const string SettingsTab = "settings.tab-changed";
        public const string VolumePreview = "settings.volume-preview";
        public const string SettingsToggle = "settings.toggle-changed";
        public const string ScheduleTrick = "schedule.trick-changed";
        public const string ScheduleDifficulty = "schedule.difficulty-changed";
        public const string ShopSelection = "shop.selection-changed";
        public const string ShopAccepted = "shop.transaction-accepted";
        public const string ShopRejected = "shop.transaction-rejected";
        public const string PerformanceGrandSuccess = "results.grand-success";
        public const string RewardCoin = "results.reward-coin";
        public const string PursuitEscalation = "pursuit.stage-escalation";
        public const string PursuitHeartbeat = "pursuit.heartbeat";
        public const string MicrophoneOn = "voice.microphone-on";
        public const string MicrophoneOff = "voice.microphone-off";
        public const string InteractionRejected = "interaction.rejected";

        /// <summary>카탈로그에 없을 때 채우는 기본 상황(키, 이름, 설명, 시트 ID).</summary>
        public static readonly (string key, string label, string description, string cueId)[] Defaults =
        {
            (RecallBroadcast, "소집 경보 방송", "쉬는 시간 종료 60초 전 무대 복귀 소집 배너가 뜰 때. ExplorationTimerView.Show", SoundCueIds.RecallBroadcast),
            (PerformanceLocalDeath, "공연 중 내 사망", "공연 중 내 단원이 죽어 '사망했습니다' 카드가 뜰 때. PerformanceEndingPresentation.ObserveDeath", SoundCueIds.PersonalDeath),
            (WaitingRoomLocalDeath, "대기실 내 사망", "대기실에서 내 단원이 죽어 '나 사망' 카드가 뜰 때. CommonWaitingRoomUI.ObserveDeaths", SoundCueIds.PersonalDeath),
            (EscapeLocalDeath, "탈출로 내 사망·포획", "탈출로에서 내 단원이 사망·포획돼 관전으로 바뀔 때(카드 없음). EscapeLocalDeathSound", SoundCueIds.PersonalDeath),
            (RestTimerWarning, "타이머 30초 경고", "휴식·밤 정비 타이머가 30초 경고(붉은 테두리) 상태로 바뀔 때. RestTimerView.Show", SoundCueIds.TimerWarning),
            (RestTimerFinalTick, "타이머 마지막 10초 틱", "휴식·밤 정비 타이머 10초~1초 동안 초마다. RestTimerView.Show", SoundCueIds.TimerFinalTick),
            (WarningBanner, "경고 배너음", "사망·연결 실패·추격 시작 같은 빨간 삼각형 경고 배너가 뜰 때 재생.", SoundCueIds.WarningBanner),
            (InteractionAccepted, "상호작용 확인 결정음", "서명·줍기·상점 열기 등 F 입력이 받아들여졌을 때 재생.", SoundCueIds.InteractionAccepted),
            (HangCountdown, "매달림 카운트다운", "줄에 매달려 '남은 N초'가 표시되는 동안 초마다 재생.", SoundCueIds.HangCountdown),
            (PerformanceSuccess, "공연 성공 결과", "결과 패널에 '공연 성공'이 뜰 때 재생.", SoundCueIds.PerformanceSuccess),
            (PerformanceFailure, "공연 실패 결과", "결과 패널에 '공연 실패'가 뜰 때 재생.", SoundCueIds.PerformanceFailure),
            (BalanceWarning, "균형 위험 경고음", "외줄 균형 바늘이 주황·빨강 구간에 있는 동안 재생.", SoundCueIds.BalanceWarning),
            (TimerExpired, "타이머 종료음", "휴식 시간·탈출로 제한 시간이 0이 됐을 때 재생.", SoundCueIds.TimerExpired),
            (ButtonHover, "버튼 마우스 올림음", "메인 메뉴·ESC 메뉴·설정·확인창의 공용 버튼에 마우스를 올릴 때 아주 작게 재생.", SoundCueIds.ButtonHover),
            (ButtonClick, "버튼 클릭음", "메뉴·설정·확인창·결과창 버튼을 눌렀을 때 재생. 월드 F 입력(UI-302)과 구분.", SoundCueIds.ButtonClick),
            (WindowOpened, "패널 열림음", "ESC 메뉴·설정 창·나가기 확인창이 열릴 때 재생.", SoundCueIds.WindowOpened),
            (WindowClosed, "패널 닫힘음", "패널을 닫거나 확인창에서 취소해 이전 화면으로 돌아갈 때 재생.", SoundCueIds.WindowClosed),
            (SettingsTab, "설정 탭 전환음", "설정 창에서 탭을 바꿀 때 재생.", SoundCueIds.SettingsTab),
            (VolumePreview, "볼륨 미리듣기음", "설정에서 효과음·UI 볼륨 슬라이더를 조절할 때 바뀐 크기를 확인하도록 재생.", SoundCueIds.VolumePreview),
            (SettingsToggle, "토글 전환음", "설정의 켜기·끄기 토글을 바꿀 때 재생.", SoundCueIds.SettingsToggle),
            (ScheduleTrick, "공연 변경음", "일정판 티켓 슬롯을 눌러 공연이 바뀌고 체크·테두리가 표시될 때 재생.", SoundCueIds.ScheduleTrick),
            (ScheduleDifficulty, "난이도 변경음", "일정판 화살표로 공연 난이도를 바꿀 때 재생. 바꿀 수 없는 칸은 UI-303 사용.", SoundCueIds.ScheduleDifficulty),
            (ShopSelection, "상점 탭·품목 선택음", "상점 패널에서 탭을 바꾸거나 품목을 고를 때 재생.", SoundCueIds.ShopSelection),
            (ShopAccepted, "거래 완료음", "기물 구매·판매나 서비스 결제가 승인될 때 재생. 공동 자금 증감음(UI-507·508)과 함께 재생.", SoundCueIds.ShopAccepted),
            (ShopRejected, "거래 거절음", "공동 자금 부족 등으로 거래가 거절될 때 재생.", SoundCueIds.ShopRejected),
            (PerformanceGrandSuccess, "공연 대성공 결과", "결과 패널에 '대성공' 리본이 뜰 때 재생. 공연 성공(UI-703)보다 화려하게.", SoundCueIds.PerformanceGrandSuccess),
            (RewardCoin, "보상 동전 이동음", "결과창에서 보상 동전이 공동 자금 쪽으로 날아갈 때 동전마다 재생. (2~8개, 0.064초 간격)", SoundCueIds.RewardCoin),
            (PursuitEscalation, "추격 단계 상승음", "추격 시작 1분 뒤 시야 확대, 2분 뒤 벽 투시, 3분 뒤 최인접 추격처럼 추격 단계가 오를 때 알림과 함께 재생.", SoundCueIds.PursuitEscalation),
            (PursuitHeartbeat, "추격자 근접 맥박", "추격자가 가까워져 화면 가장자리가 붉게 맥박치는 동안 재생. 가까울수록 크게. 대기실·탈출로 공용.", SoundCueIds.PursuitHeartbeat),
            (MicrophoneOn, "마이크 켬", "단축키나 마이크 아이콘으로 내 마이크를 켤 때 재생.", SoundCueIds.MicrophoneOn),
            (MicrophoneOff, "마이크 끔", "단축키나 마이크 아이콘으로 내 마이크를 끌 때 재생. UI-1501 끔 변형.", SoundCueIds.MicrophoneOff),
            (InteractionRejected, "상호작용 거부음", "현재 일정 칸의 난이도를 변경할 수 없을 때 재생.", SoundCueIds.InteractionRejected),
        };
    }
}
