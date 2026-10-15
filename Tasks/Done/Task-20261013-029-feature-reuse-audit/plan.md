# Plan

## Context

공통 게임 시스템의 후속 구현이 기존 상태·결과 시스템과 중복되지 않도록 CC-107에서 재사용 가능한 기능과 부족한 연결을 확인한다. 이번 Task는 코드 수정이 아니라 근거가 추적되는 조사 결과를 남기는 일이다.

## Approach

1. `setup.md`에 명시한 조사 범위에서 기존 상태·세션·공연 결과·부상/생존·공간 진입 관련 구현을 읽는다.
2. 각 기능을 재사용 가능, 수정 후 재사용, 신규 구현 필요, 범위 외로 구분하고, 코드 경로와 확인한 책임/동작을 근거로 적는다.
3. 불명확한 소유권·게임 규칙·연결 방식은 사실처럼 단정하지 않고 Open Decision으로 남긴다.
4. 조사 결과를 `Docs/Domains/common-game-systems.md`에 기록하고 후속 Task의 계획에 전달한다. 코드·씬·프로젝트 설정은 수정하지 않는다.
5. 문서 검사 및 공동 확인 후 `integration/common-game-systems`로 제출한다.

## Files

- 수정 예정: `Docs/Domains/common-game-systems.md`
- 읽기 전용 참고: `Docs/References/game-flow.md`, `Harness/Project/Facts/game-flow.md`, `Docs/Domains/network-session.md`, `Assets/Scripts/Systems/Network/Session/GameSessionState.cs`, 기존 공연 결과 및 플레이어 상태 구현(조사 중 경로를 결과 문서에 명시)

## Risks

- 코드에서 관찰한 동작과 최신 기획의 기대 동작이 다를 수 있으므로 둘을 분리해 기록한다.
- 후속 Task의 범위를 선결정하지 않는다. 실제 파일 소유권과 변경 필요성을 확인한 뒤 작업 구역을 배정한다.
- Unity 테스트 에셋이나 코드는 이 조사 Task 범위가 아니다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: 해당 없음 (코드 변경 없음)
- 3 테스트: 해당 없음 (기존 기능 조사만 수행)
- 4 실행 로그: 해당 없음 (Unity 실행/에셋 변경 없음)

## 공동 테스트 항목

1. 후속 Task 담당자/PM이 재사용 목록의 각 주요 항목에 코드 경로와 관찰 근거가 있는지 확인한다.
2. 재사용·수정·신규 구현 분류와 미확정 결정 사항을 CC-108~111 계획에 반영할 수 있는지 확인한다.
