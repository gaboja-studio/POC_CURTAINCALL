# Plan

## Context

런은 1~3일차 공연과 4일차 아침 쉬는 시간까지다. 하루는 공연과 쉬는 시간을 반복하고 밤 정비로 끝난다. 지금 코드에는 방 상태(대기/진행/종료)만 있고 세대·일차·시간대 진행이 없다. 이 Task는 런 상태(세대·일차·현재 시간대)와 전환 규칙, 쉬는 시간 타이머, 그리고 2단계 Task들이 함께 쓸 세팅 5묶음 틀을 만든다.

근거: `Integrations/Active/Integration-common-game-systems/plan.md`, `Docs/Domains/common-game-systems.md`(Task-029), `Harness/Project/Facts/game-flow.md`, `Harness/Project/Facts/run-state.md`, `Harness/Project/Decisions/run-rules.md`, `Harness/Project/Decisions/game-settings.md`

## Approach

1. 세팅 5묶음(공통 / 각 묘기 / 대기실 / 탐색 / 탈출로) 틀을 만든다. 공통에 정해진 값(쉬는 시간 타이머 A/B 기본 B, 기물 소지 팔 2개 2·팔 1개 1·배낭 +2, 최소 인원 2, 일수 3, 하루 공연 2회, 쉬는 시간 5분, 소집 경보 1분, 가이드 쉬는 시간 길이 임시값)을 넣는다. 대기실·탐색·탈출로 세팅은 빈 틀만 두고 2단계(035)가 채운다.
2. 런 상태: 세대 번호, 일차, 시간대(도입 → 공연 → 쉬는 시간 → 공연 → 밤 정비 → 다음 날 아침 쉬는 시간 …), 묘기 순서는 1일차 고정(줄타기 → 인간 대포)만 두고 2일차 이후는 세트리스트 자리로 비워 둔다. 호스트가 확정하고 모두에게 공유한다.
3. 전환: 공연 종료 신호 → 쉬는 시간 또는 밤 정비, 쉬는 시간 종료(타이머·전원 무대 입구 통로 진입) → 공연, 밤 정비 전원 준비 완료 → 다음 날, 4일차 아침 쉬는 시간 종료 → 런 실패 → 세대 교체. 탈출 성공 신호 → 엔딩.
4. 쉬는 시간 타이머: B안 = 대기실에서 나온 순간부터, A안 = 쉬는 시간 시작부터. 세팅으로 전환. 종료 1분 전 소집 경보 알림.
5. 방 상태(`GameSessionState`)는 로비 → 런 진행 → 런 종료 단위로만 쓴다. 게임 시작 후 재접속 불가·로비 재입장 허용은 지금 세션 동작 그대로인지 확인만 한다.
6. 공연 종료·장소 진입 신호는 다른 Task가 보낸다. 테스트 씬에는 이를 흉내 내는 디버그 버튼을 둔다.

## Files

- 수정함(#59): `Assets/Scripts/Systems/Campaign/`(`CampaignRun`·`RunState`·`CampaignSnapshot`·`CampaignFlowTestUI`), `CampaignFlow.unity`(테스트 UI 배치), `Systems/Settings/`(`BaseGameSettings`·`GameSettings` 수정, `WaitingRoom`·`Exploration`·`EscapeRoute` Settings 추가), 세팅 에셋 3개 추가·연결·최소 인원 2, `Docs/Domains/common-game-systems.md`(PM 허용)
- 읽기 전용 참고: `Assets/Scripts/Systems/Network/Session/`, `Docs/Domains/network-session.md`

## Risks

- 세팅 틀은 2단계 넷이 모두 쓴다. 이름·위치를 바꾸면 2단계가 깨지므로 1단계 합치기 전에 확정한다.
- 수치는 모두 임시값이다. 세팅에 두고 코드 상수로 고정하지 않는다.
- 방 상태와 런 시간대를 섞지 않는다(공연 종료에 `EndGame`을 쓰지 않는다).

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 테스트 씬에서 1일차 → 4일차 아침 → 세대 교체까지 디버그 버튼으로 진행, Console 오류 0 기록

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 새 런이 세대 1·1일차·첫 공연(줄타기)으로 시작하고, 버튼으로 공연 종료·쉬는 시간 종료·밤 준비 완료를 주면 기획 순서대로 시간대가 바뀐다.
2. 쉬는 시간 타이머가 세팅 B안(대기실에서 나온 뒤)·A안(시작부터)대로 시작하고, 종료 1분 전 경보가 온다.
3. 4일차 아침 쉬는 시간이 끝나면 런 실패 → 세대 2·1일차로 다시 시작한다.

## 결과 (2026-10-14, #59 병합)

계획과 달라진 점만 적는다. 자세한 상태는 `handoff.md`.

- 공통 세팅은 새 클래스 대신 기존 `BaseGameSettings`에 "런 진행"·"기물 소지" 칸을 더했다(이름 유지로 2단계 영향 없음).
- 상태 공유는 네트워크 프리팹 없이 named message로 했다(새 에셋 없이, 기존 `TightropeFire` 방식). 접속 없이 확인하는 오프라인 런을 추가했다.
- 시간대 전환 신호는 `CampaignRun` 공개 메서드(호스트 호출) + `StepChanged`·`RecallAlarm`·`RunEnded` 이벤트로 제안했다. 형식 확정은 합치기 ①(meta Open).
- 탈출(개시·성공·전원 리타이어)과 세트리스트 자리도 런 상태에 넣었다. 탈출 성공(엔딩)일 때만 방 상태를 `Ended`로 바꾼다.
- 테스트 어셈블리 없음(`NO_PROJECT_TESTS`), 플레이 테스트는 작업자가 테스트 씬에서 확인.
