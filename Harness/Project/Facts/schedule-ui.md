---
summary: W3 티켓형 v2 실게임 프리팹·공연/난이도 입력·포커스 overlay와 보존된 v1 계약
status: active
updated: 2026-11-14
source: Task-20261019-061, Task-20261114-132 사용자 승인·실제 포인터 PlayMode 2건·EditMode 5건
---

# W3 공연 스케줄표 UI

## 현행 실게임 v2 (2026-11-14)
- `CommonWaitingRoomUI.unity`는 `Assets/Resources/Prefabs/UIs/Roles/Retained/ScheduleUi/SchedulePanelV2.prefab`을 사용한다. 기존 v1 프리팹/원본은 보존한다.
- 사용자 확정: 공연명 최우선(62pt), 난이도 보조(30pt). 화살표만 난이도 변경, 나머지 슬롯은 공연 변경. 3일×2공연과 기존 권한/해금/1일차/보상 규칙 유지.
- `ScheduleTicketCard`의 슬롯/이전/다음 Button이 입력을 분리한다. 1~3 경계/미해금은 비활성, 성공한 변경은 1.5초 적용 문구·체크·테두리로 표시한다.
- `WaitingRoomScheduleUI.Board`는 비포커스 WorldSpace, 포커스 ScreenSpaceOverlay(110)로 같은 UI를 전환한다. 전체 화면 입력 차단 배경, 툴팁250, 닫기 때 판의 원래 위치/크기 복귀. 다른 플레이어 Renderer/Collider/네트워크는 변경하지 않는다.
- `ScheduleScreenHold.Keep/Forget`으로 집중 표시 Canvas를 보존하고 해제한다. 음성 개발 IMGUI도 포커스 중 숨긴다.
- GPT Image 5원본/15 Sprite subasset은 `Assets/_ThirdParty/curtaincall_schedule_v2/`에 있다. 승인 시안·제작 프롬프트·sprite-catalog.json을 보관한다. Unity 패키지는 Task의 `export-kit.cs`에서 로컬 재생성한다. 2개 비교/선택 장식은 런타임 미사용.
- 조립기 `Assets/Scripts/UI/SceneIntegration/Editor/ScheduleTicketKitBuilder.cs`의 Build/Install. 전용 Static TMP 폰트로 기존 폰트 원본을 수정하지 않는다. 현재 프로젝트에 Sprite Editor 패키지가 없어 TextureImporter의 SpriteMetaData를 사용한다.
- 검증: 입력·경계·재열기 PlayMode 검사 및 동일 PC의 독립 에디터 2개 LAN 연결로 공연·난이도 수신, 참가자 읽기 전용, 실제 접속 캐릭터의 판 앞 교차 중 호스트 화살표 입력을 확인했다. Task-20261114-132의 multiplayer-verification.md 참조. 별도 PC·Relay는 미검증이다.
- 최신 회귀에서는 첫날 공연 뒤 `SetTutorialDone`/`RefreshGuideRest`를 보내야 집결 가능하다. 구 난이도 테스트의 준비 단계도 이 조건에 맞췄다. GameSettings와 RunState 규칙은 변경하지 않았다.

## 보존된 v1 계약

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
- 산출물: `Assets/Resources/Prefabs/UIs/Roles/Retained/ScheduleUi/SchedulePanel.prefab`, `Assets/Scenes/Tests/ScheduleUi/ScheduleUi.unity`. 확인 씬 Play 후 기본 데모를 명시적으로 연다. 검증 기록은 Task의 `schedule-ui-verification.md`다.
- `Scripts/check-work.ps1`와 W3 선택 PlayMode 검사를 사용한다. 합성 입력/실행 캡처·물리 입력·사람 외형 승인은 구분한다. 실게임/실기기/공동 검증은 별도다.
