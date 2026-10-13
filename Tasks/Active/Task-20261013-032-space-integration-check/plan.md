# Plan

> 초안. CC-107~110 병합 후 공간 담당자와 연결 범위를 합의해 확정한다.

## Context

공통 시스템(런 진행·부상/생존·보상/공동 자금)은 각자 테스트 씬에서 따로 만들어진다. 이 연결 Task는 그 상태가 실제 공간 이동에서도 이어지고, 하루 흐름이 끝까지 돌아가는지 확인한다.

## Approach

1. 선행 Task(029·028·030·031)가 통합 브랜치에 병합된 뒤 시작한다.
2. 공간 전환 시 공통 상태를 유지·전달하는 연결만 `setup.md` 작업 구역에서 만든다.
3. 공간 담당자 소유 파일은 직접 고치지 않는다. 필요하면 PM이 협의 후 공용 파일로 배정하거나 별도 Task로 분리한다.
4. 통합 확인 후 `integration/common-game-systems`로 제출한다.

## Files

- 수정 예정: `Assets/Scripts/Systems/SpaceLink/`, `Assets/Scenes/Tests/Systems/SpaceLink/SpaceLink.unity`
- 읽기 전용 참고: `Docs/Domains/common-game-systems.md`, `Harness/Project/Decisions/space-workspaces.md`, CC-108~110 결과물

## Risks

- 공간 담당자 작업 일정과 얽힐 수 있다. 공간이 아직 없으면 임시 테스트 씬으로만 확인하고 그 사실을 기록한다.
- 실패 항목은 이 Task에 붙이지 말고 후속 Task로 분리한다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 자동 테스트 구성이 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 테스트 씬에서 하루 흐름 전체 실행, Console 오류 0 기록

## 공동 테스트 항목

초안 (인계 전 확정):

1. 런을 시작해 공연→자유시간→밤→다음 Day까지 공간을 오가며 한 바퀴 돌릴 수 있다.
2. 공간을 옮겨도 부상·생존 상태와 공동 자금이 그대로 유지된다.
3. 두 명 이상 접속해도 모두 같은 Day·단계·자금을 본다.
