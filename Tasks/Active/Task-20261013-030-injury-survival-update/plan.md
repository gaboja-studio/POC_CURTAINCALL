# Plan

> 초안. CC-107(Task-029) 조사 결과와 최신 부상 기획을 반영해 인계 전에 확정한다.

## Context

부상 기획이 바뀌어 플레이어의 부상·생존 상태를 새 규칙에 맞춰야 한다. 현재 코드에는 온라인 플레이어 동기화와 추락/재시작 판정(`Docs/Domains/player-sync.md`)은 있지만, 부상 단계나 생존 상태 구현은 확인되지 않았다(최종 확인은 CC-107).

## Approach

1. `setup.md` 작업 구역에서 부상·생존 상태와 전환만 구현한다.
2. 부상 단계·회복·사망 수치는 조정값으로 두고 코드 상수로 고정하지 않는다.
3. 런 진행(CC-108)과는 상태만 주고받는 형태로 연결하고, 실제 공간 이벤트 연결은 CC-111로 넘긴다.
4. 검사 후 `integration/common-game-systems`로 제출한다.

## Files

- 수정 예정: `Assets/Scripts/Systems/Survival/`, `Assets/Scenes/Tests/Systems/Survival/InjurySurvival.unity`
- 읽기 전용 참고: `Docs/Domains/player-sync.md`, `Docs/Domains/common-game-systems.md`, `Harness/Project/Facts/game-flow.md`, 부상 기획 문서

## Risks

- 기존 플레이어 동기화 코드를 수정해야 하면 작업 구역 밖이다. 직접 고치지 말고 PM에게 공용 파일 배정을 요청한다.
- 부상·사망 수치는 미확정 게임 규칙이다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 테스트 씬에서 상태 전환 확인, Console 오류 0 기록

## 공동 테스트 항목

초안 (인계 전 확정):

1. 부상을 입으면 기획된 단계로 상태가 바뀌고, 단계가 다 차면 사망(생존 실패)으로 바뀐다.
2. Day가 넘어가거나 세대가 바뀔 때 부상·생존 상태가 기획대로 유지 또는 초기화된다.
