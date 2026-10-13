# Plan

> 초안. CC-107(Task-029) 조사 결과와 보상 규칙 결정을 반영해 인계 전에 확정한다.

## Context

기획상 공연 결과는 보상으로, 보상은 팀 공동 자금으로 이어진다. 현재 코드에는 보상 조정값 틀(`Assets/Scripts/Systems/Settings/RewardTable.cs`)과 플레이 기록(`Assets/Scripts/Systems/Telemetry/`)이 있지만, 공동 자금은 확인되지 않았다(최종 확인은 CC-107).

## Approach

1. `setup.md` 작업 구역에서 공연 결과→보상→공동 자금 흐름만 구현한다.
2. 보상액·호응 산정은 사람이 정할 규칙이다. 조정값으로 두고 결정 전에는 테스트 입력으로만 다룬다.
3. 런 진행(CC-108)의 공연 종료 시점과 생존 상태(CC-109)를 받아 쓰고, 공간 연결은 CC-111로 넘긴다.
4. 검사 후 `integration/common-game-systems`로 제출한다.

## Files

- 수정 예정: `Assets/Scripts/Systems/Reward/`, `Assets/Scenes/Tests/Systems/Reward/PerformanceReward.unity`
- 읽기 전용 참고: `Assets/Scripts/Systems/Settings/RewardTable.cs`, `Assets/Scripts/Systems/Telemetry/`, `Docs/Domains/common-game-systems.md`, `Harness/Project/Facts/game-flow.md`

## Risks

- `RewardTable`·Telemetry를 고쳐야 하면 작업 구역 밖이다. PM에게 공용 파일 배정을 요청한다.
- 보상·자금 수치는 밸런스 값이다. 나중에 데이터 시트로 빼낼 수 있게 코드에 고정하지 않는다.
- 멀티플레이에서 공동 자금을 누가 확정하는지(호스트 판정) 확인이 필요하다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 테스트 씬에서 보상·자금 변화 확인, Console 오류 0 기록

## 공동 테스트 항목

초안 (인계 전 확정):

1. 공연이 끝나면 결과에 맞는 보상이 계산된다(테스트 입력 기준).
2. 보상이 공동 자금에 더해지고, 모든 플레이어가 같은 금액을 본다.
3. 다음 Day로 넘어가도 공동 자금이 유지된다.
