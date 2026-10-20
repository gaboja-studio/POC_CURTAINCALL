---
summary: W3 공연 스케줄표 읽기 전용 표시 계약·일차별 스크롤과 독립 확인 씬
status: active
updated: 2026-10-20
source: Task-20261019-061
---

# W3 공연 스케줄표 UI

## 언제 읽나
- 공연 일정표 조립·표시 자료 주입·닫기/열별 스크롤 어댑터 연결 전에 읽는다.

## 사실
- 제공 Runtime은 private schedule 키트의 `CurtainCall.UI.ScheduleV1`, 제공 조립기는 `CurtainCall.UI.ScheduleV1.Editor.SchedulePanelBuilder.BuildProject`다. 공개 코드는 `Assets/Scripts/Systems/ScheduleUi/` 전용이다.
- 기준 Canvas1920×1080/Match.5/PPU100, 패널1056×552/(432,272). 상단240 HUD 보호·전면 딤 없음. 종이992×344/재질 Simple 고정 alpha.16, 열3개304×312·viewport304×248/카드304×112·보폭124다. 전체 LayoutStage만 균등 축소한다.
- 최초 Hidden, Open160/Close120ms unscaled smoothstep. `Open`은 열 스크롤을 상단 초기화하고 기본 닫기 포커스. 닫기 외 카드·일차는 Selectable이 아니다.
- `SchedulePresentation`은 정확히3일·고유 일차ID/전체 고유 항목ID를 가진 복사·읽기 전용 자료다. 전달 순서/문구를 표시하며 current와 confirmed는 독립 플래그다. 일정 생성/편집/시간 계산/보상/경제/진행 규칙은 구현하지 않는다.
- `Open`/`SetPresentation`/`ScrollDay`/`Close`/`Hide`는 Unity 메인 스레드에서 호출한다. `SetPresentation`은 표시 중 동일 SessionId만 허용하며 일차ID별 정규화 스크롤을 보존한다. revision 최신 여부는 호출자 책임이다.
- `ScrollDay(index, deltaUi)`의 양수는 아래 방향이다. 휠은 열별 ScrollRect로 처리하며 키보드/패드 스크롤 연결·커서/게임 입력 억제·패널 우선순위/이전 포커스 복귀는 외부 책임이다. 전역ESC polling은 없다.
- `Close`는 CloseRequested와 Closed, `Hide`는 Closed만 통지한다. Closed는 fade 완료가 아닌 표시 종료 처리 시점이며 콜백 재진입은 epoch로 보호한다. 닫기는 일정/거래 취소 의미가 아니다.
- 실제 Pretendard Static Medium(Spectator)/SemiBold(SettingsUi)를 읽기 전용 참조하고, Bold는 기존 WorldLabels에 말줄임표가 없어 전용 ScheduleUi Static2048/107문자를 쓴다. 제목32/Bold, 일차·닫기28/SemiBold, 카드24·보조18/Medium. richText/parseCtrl/autoSize/raycast off·Ellipsis로 원문을 보존한다. 일차 라벨은 28pt 세로 잘림을 막도록 header48 안에서 y4/높이40이다.
- 생산20PNG=신규8+정본12이며 누적182 이력과 구분한다. 전체 원본/PNG/GUID/자료를 보존하고 신규 importer만 보정한다. 기존 정본/폰트 재작성은 하지 않는다.

## 확인 방법
- 산출물: `Assets/Resources/Prefabs/UIs/ScheduleUi/SchedulePanel.prefab`, `Assets/Scenes/Tests/ScheduleUi/ScheduleUi.unity`. 확인 씬 Play 후 기본 데모를 명시적으로 연다. 검증 기록은 Task의 `schedule-ui-verification.md`다.
- `Scripts/check-work.ps1`와 W3 선택 PlayMode 검사를 사용한다. 합성 입력/실행 캡처·물리 입력·사람 외형 승인은 구분한다. 실게임/실기기/공동 검증은 별도다.
