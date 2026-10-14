# Plan

## Context

"검사해줘"(`check-work.ps1`)의 3단계 테스트가 항상 FAIL로 찍힌다. `verify-unity.ps1`은 연결된 에디터가 없으면 `ENV_BLOCKED`(exit 2)로 멈추고, 연결돼 있으면 테스트를 batch `unity test`로 돌린다. batch 실행은 같은 프로젝트를 연 에디터가 있으면 "another Unity instance is running"으로 거부된다. 그래서 어느 상태에서도 테스트 단계를 통과할 수 없다(Task-029, 2026-10-14 재현). 기존 함정: `Harness/Engine/Unity/Pitfalls/unity-test-editor-open.md`.

## Approach

1. 테스트를 열린 에디터의 Pipeline 명령(`list_tests`·`run_tests`, 비동기면 `test_status`)으로 실행하도록 `verify-unity.ps1`을 바꾼다. `--mode`·`--filter`를 넘긴다.
2. 출력 계약은 그대로 둔다: `Tests(<Mode>): NO_PROJECT_TESTS` / `Tests(<Mode>): total N, passed N, failed N, skipped N`. `check-work.ps1`의 결과 해석이 그대로 동작하게 한다. 실패가 있으면 exit 1.
3. 명령 이름은 `$Commands` 표에 넣는다(`pipeline-command-drift.md` 규칙). 명령이 없으면 `COMMAND_MISSING`으로 알린다.
4. 결과 보고서: 지금처럼 `.harness/local/test-results-<mode>.xml`을 남기거나, 에디터 결과를 같은 위치에 JSON으로 남긴다(작업 시작 때 제안).
5. 함정 문서를 "해결됨 — 에디터 Pipeline 테스트 사용"으로 갱신하고, 팀 안내서에 "검사해줘는 에디터를 연 상태에서" 한 줄을 넣는다.
6. `Scripts/Lib/common.ps1`의 `Get-SetupAreas`가 `Scripts/`·`Harness/` 경로도 작업 구역으로 인식하게 한다(지금은 `Assets|ProjectSettings|Packages|Docs`만 인식해 하네스 Task 인계가 막힘).

## Files

- 수정 예정: `Scripts/verify-unity.ps1`, (필요 시) `Scripts/check-work.ps1`, `Scripts/Lib/common.ps1`, `Harness/Engine/Unity/Pitfalls/unity-test-editor-open.md`, `Docs/Guides/team-workflow.md`
- 읽기 전용 참고: `Harness/Core/Policies/verification-ladder.md`, `Harness/Engine/Unity/Pitfalls/pipeline-command-drift.md`, `Scripts/save-work.ps1`

## Risks

- PlayMode 테스트는 에디터를 Play 상태로 바꾼다. 저장 안 된 씬이 있으면 기존 검사처럼 먼저 멈춘다.
- 테스트가 오래 걸리면 명령 시간 제한에 걸린다. 비동기 실행과 상태 폴링을 쓴다.
- 지금 프로젝트에는 테스트가 0개라, 실패 경로(failed > 0)는 임시 테스트로 확인한 뒤 지운다.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 고친 `check-work.ps1`로 실행해 `NO_PROJECT_TESTS (EditMode)`가 나오는지 확인
- 4 실행 로그: EditMode·PlayMode 각각 check-work 출력 기록

## 공동 테스트 항목

1. 에디터를 연 상태에서 "검사해줘"를 하면 컴파일 PASS, 테스트 `NO_PROJECT_TESTS`로 기록된다(FAIL 아님).
2. 임시로 실패하는 테스트를 넣으면 테스트 단계가 FAIL과 실패 개수로 기록되고 저장·제출이 멈춘다.
3. 에디터를 닫으면 지금처럼 `ENV_BLOCKED`로 멈춘다(결과를 지어내지 않는다).
