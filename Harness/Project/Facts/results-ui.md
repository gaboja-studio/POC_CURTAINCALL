---
summary: C12 공연 결과 표시·확인 의도·장식 동전 계약과 독립 확인 씬
status: active
updated: 2026-10-21
source: Task-20261019-061
---

# C12 공연 결과 패널

## 언제 읽나
- 결과 표시 자료를 공급하거나 확인 콜백·장식 동전을 연결하기 전. 실제 정산·게임 진행 규칙은 이 UI가 소유하지 않는다.

## 산출물·보존
- 프리팹: `Assets/Resources/Prefabs/UIs/ResultsUi/ResultsPanel.prefab`.
- 독립 씬: `Assets/Scenes/Tests/ResultsUi/ResultsUi.unity`. Play 시작 Hidden, Preview28버튼으로 명시적으로 연다.
- 공개 코드: `Assets/Scripts/Systems/ResultsUi/`. 제공 Runtime/Builder는 private 원위치 재사용, 버튼은 기존 ResultsV1 격리 유지.
- 신규6/재사용19 Sprite, 시트25조각 RGBA·정본19 SHA 일치 확인. 기존 importer/PNG/GUID/폰트·제작 자료는 보존한다.
- Medium(Spectator)/SemiBold(SettingsUi)는 기존 Static 읽기 전용. 전용 ResultsUi Bold Static2048/245문자 생성. PerformanceHud 폰트는 C12 글리프 부족으로 사용하지 않는다.

## 표시 계약
- Overlay1920×1080/Match0.5/PPU100. 패널1120×640/(400,256), 상단 HUD 보호·전체 화면 딤 없음, 패널 Body만 raycast 차단.
- 실패/성공/대성공 리본, 멤버2–4명·고유 ID1–4·로컬 최대1명. 표시 순서/문구/거리/보상/자금은 호출자가 공급하며 판정·계산하지 않는다.
- `ResultsPresentation`은 복사된 불변 스냅샷이다. 위치 유한값만 Clamp01, null/NaN/Infinity는 해당 위치 요소만 숨기고 이름/거리는 유지한다.
- 최대 위치는 외부 값이며 멤버 값에서 추론하지 않는다. 추락 겹침은 X를 유지하고 Y만14 이동한다.
- 물리 Medium24/보조18/번호18·SemiBold28·Bold48/자금32. fakeBold/richText/parseCtrl/autoSize 없음, 고정 영역 Ellipsis.
- 실제 필요높이 제목57.26·단원/거리28.63 때문에 원본 제목56/행28은 빈 렌더였다. 중심/글자 크기/패널은 유지하고 제목64·단원/거리32로 최소 보정했다. 후속 사용자 간격 요청으로 부제목 상단128(리본 아래16/제목 영역 아래28)·하단 전체32 이동·패널 높이640으로 확장했다.

## API·책임
- `CurtainCall.UI.ResultsPanelView.Open(presentation,takeFocus)`/`SetPresentation`/`Hide`. 갱신은 Visible·동일 Session만 수용, revision 순서는 호출자 책임이다.
- `AcknowledgeRequested(sessionId)`는 활성·상호작용 가능한 확인 버튼의 의도만 통지한다. 자동 Hide/지급/다음 일정/공동 준비 없음, 연속 요청 잠금은 외부 갱신 책임이다.
- Hide는 즉시 입력/자기 선택/동전을 정리하며 Hidden은 논리 종료 시점에 발생한다. 전역 ESC/Cancel/입력 소유 변경 없음.
- 표시160ms/숨김120ms는 unscaled. `PlayRewardTransfer(sessionId,count)`는 유효 목적지/동일 Session에서만 실행하는 장식이다.
- 동전 pool8/요청2–8/640ms·간격64ms, 실행 중 중복 거절·완료 뒤 명시 재요청 가능. 동일 Overlay root Canvas 목적지만 허용, Hide/갱신/재열기/목적지 교체·소실/disable 시 정리한다. 금액 불변.
- `ResultsUiProjectBuilder.Build()`는 유휴/구역/폰트/정본/기존 출력 검증 후 신규 importer6만 보정, additive 조립한다. 기존 출력/meta·dirty 씬·부분 출력은 보존하며 자동 삭제/재시도하지 않는다.

## 검증 경계
- 상세 실행 결과/최초 실패/보정/사람 확인·실게임 연결 제외는 Task의 `results-ui-verification.md`를 따른다. 합성 입력·실행 캡처를 물리 입력·사람 외형 승인으로 해석하지 않는다.
