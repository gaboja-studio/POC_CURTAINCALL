# Plan

## Context

CC-132 4단계: 둘째 공연 뒤 밤 정비(문 잠김·타이머 없음) → 스케줄표로 세트리스트 작성·확정 → 전원 준비 완료 → 다음 날 세트리스트대로 공연 호출. 기준은 `night-maintenance.md`, `schedule-board.md`, `game-flow.md`이고, 2026-10-17 담당자 결정 4개(meta)를 따른다.
확인된 공용 사실: 밤 종료는 `RunState.RefreshNight`(전원 준비만 확인, 세트리스트는 보지 않음), `CampaignRun.SetSetlist(day, tricks)`는 호스트 전용·잠금 없음·4일차 실행 없음, 공연 씬은 묘기와 무관하게 하나(`TrickId`만 바뀜), 선택 가능한 묘기는 줄타기·인간 대포 2개. 공용 `CampaignFlowTestUI`의 "준비 완료" 버튼은 저장 검사를 우회한다.

## Approach (순서대로)

1. **씬 구성** — 3단계 씬과 같은 4인 테스트 환경을 만든다. PlaySetup 끄기, Main Camera에 CinemachineBrain·PlayerFollowCamera, `ShopStorage`·`ShopStorageViews`와 ③ 수화기·배송구·④ 선반을 배치(3단계 스크립트 재사용, 수정 없음), 접속 패널(`NetworkSessionTestUI`)과 4단계 전용 테스트 패널. 런 진행은 우회 버튼 때문에 공용 패널 대신 4단계 패널에서 한다(공연 종료·밤 진입 버튼 포함). 배치는 `NightSchedule/Editor` 메뉴로 다시 만들 수 있게 한다.
2. **밤 진입** — `NightDoorLock`: `CampaignRun.StepChanged`가 Night면 대기실 문(로비↔⑥ 문틀)에 막는 벽·"잠김" 표지를 켜고, 아침이 되면 끈다. 위치·켜짐은 런 상태로 정하므로 네 화면이 같다. 타이머는 밤에 공용 상태가 None이라 표시만 확인(2단계 화면이 씬에 없으면 HUD 문구 "밤 정비 — 타이머 없음").
3. **스케줄 규칙(순수 로직, EditMode 테스트)** — `ScheduleBook`(Logic asmdef): 일차별 2칸, 1일차 고정, 밤마다 편집 가능 칸(1일차 밤 2·3일차, 2일차 밤 3일차만, 3일차 밤 4일차), 확정 시 잠금, 확정 여부(밤 단위), 같은 공연 중복 허용, 칸별 난도 = 그 묘기의 런 내 출연 순번(1·2·3 상한), 세대 교체·런 재시작 시 초기화.
4. **스케줄표 상태·동기화** — `ScheduleBoard`(DontDestroyOnLoad, 호스트 판정): 호스트만 편집·확정, 참가자는 상태만 받는다. 전용 메시지 채널은 `ReliableFragmentedSequenced`(3단계 크기 문제 재발 방지), 늦게 들어온 사람은 동기화 요청. 확정 시 호스트가 `SetSetlist`(2·3일차)를 부른다. 초기화는 세대 증가뿐 아니라 런 종료·세대 감소도 감지한다(3단계 결함 #2 교훈).
5. **스케줄표 화면** — 로비 `ScheduleBoard` 앞 F → 카메라 클로즈업(내 화면만, 닫으면 복귀) → 플래너 노트 창(임시 IMGUI, 아트는 후속): 1~4일차 공연·전문가·난도 표시, 밤이면 호스트에게 칸 선택·확정 버튼, 참가자에겐 "호스트가 작성 중"과 실시간 내용. 창이 열려 있는 동안 이동 입력 차단(`InputGate`).
6. **준비 완료** — 4단계 패널·스케줄표 창의 준비 완료 버튼. 호스트는 그날 밤 확정 전 누를 수 없고("스케줄표를 먼저 확정하세요"), 호스트가 준비해야 밤이 끝나므로 미작성 상태로 넘어가지 않는다. 참가자는 언제든 가능.
7. **다음 날 호출 확인** — 아침 쉬는 시간 `TrickId`·공연 1·2가 확정한 순서와 같은지 테스트 패널·로그로 확인.

## Files

- 새로 만듦: `Assets/Scripts/WaitingRoom/NightSchedule/` — `Logic/ScheduleBook.cs`·asmdef, `ScheduleBoard.cs`, `ScheduleChannel.cs`, `ScheduleBoardView.cs`, `NightDoorLock.cs`, `NightScheduleTestUI.cs`, `Editor/NightScheduleSceneBuilder.cs`, `Tests/EditMode/ScheduleBookTests.cs`·asmdef.
- 수정: `WaitingRoomNightSchedule.unity`, 이 Task 문서.
- 읽기·재사용만: `WaitingRoom/Base/`, `WaitingRoom/ShopStorage/`, `Systems/Campaign/`·`Network/`·`Player/`.

## 결정 반영 (2026-10-17 담당자)

1. 3일차 밤: 4일차 작성 가능·확정 필수(다른 밤과 같은 규칙), `SetSetlist`는 부르지 않음(실행 안 함). 공동 테스트 2의 "4일차 선택 불가"는 PM에게 문구 갱신 요청.
2. 스케줄표 F → 카메라 클로즈업을 넣는다. 공용 카메라 요청 구조(`Systems/Camera` — 우선순위 요청 등록/해제)를 읽기·재사용만 하고, 부족하면 PM 요청.

## Risks

- 저장 전 준비 불가는 이 구역 UI에서만 막는다. 공용 패널·다른 씬의 `SetReady`는 우회 가능 → 진짜 강제는 공용 `RunState` 연결 Task로 PM 요청.
- 난도는 이 화면에서 계산·표시만 한다. 공연장으로 난도를 넘기는 공용 경로가 없어(run-state.md) 연결 Task 필요.
- 3단계 상점의 동기화 결함(#1 메시지 크기)은 4단계 씬에도 그대로 있다. 4인 테스트에서 상점을 많이 쓰면 화면이 어긋날 수 있다 → 3단계 fix 요청과 병행.
- 공연 씬이 하나라 "세트리스트대로 호출"은 `TrickId`로만 확인된다.
- 1단계 `WaitingRoomSpawn`의 스폰·균형 끔에 의존한다. 밤 복귀 스폰 위치(open-items #4)는 1단계 동작 그대로.

## Auto Verification

- EditMode: `ScheduleBookTests`(편집 가능 칸·잠금·확정·중복·난도·초기화).
- 구역 → 문서 → 컴파일 → 테스트: `pwsh -File Scripts/check-work.ps1`.
- AI Play 1인(호스트): 공연 2회 종료 → 밤 → 문 막힘·확정 전 준비 거절 → 확정 → 준비 → 2일차 아침 `TrickId` 확인. 키 입력은 장치 이벤트(`InputSystem.QueueStateEvent`)로, 누락 시 같은 진입점 호출로 대체하고 그렇게 기록한다.

## 공동 테스트 항목

CC-132 단계 기준. 실행 씬: `Assets/Scenes/Tests/WaitingRoom/NightSchedule/WaitingRoomNightSchedule.unity`. 4인(호스트 1·참가자 3). handoff에 일시·참여자·씬·재현·예상·실제를 기록한다. 현재 전부 미실행이다.

1. 밤 진입: 둘째 공연 뒤 복귀. 예상: 대기실 문 잠김·타이머 정지가 네 화면 동일.
2. 스케줄 작성·수정: 1일차 밤 2·3일차 작성, 2일차 밤 3일차 수정. 예상: 1일차 고정, 3일차 밤 4일차 작성·확정 가능하나 실행 안 됨(담당자 결정, PM 문구 갱신 요청), 호스트 확정 결과·난도 표시가 네 화면 동일, 확정 전 호스트 준비 거절.
3. 다음 날 진행: 전원 준비 완료. 예상: 밤 종료 후 1~3일차가 순서대로, 작성한 세트리스트대로 공연 호출.
