# Plan

## Context

PR #148의 Git 경로 수정은 병합됐지만 workspace-flow의 예상 오류 문구 검사가 출력 줄바꿈 때문에 Ubuntu에서 실패했다. 실제 금지 동작은 정상이다. 사용자가 새 Task·브랜치로 후속 수정을 승인했다.

## Approach

1. Run의 오류 정규식 비교에 기존 파일과 같은 (?s)를 사용한다. 실패 종료 코드·쓰기 차단 검사는 유지한다.
2. 격리 fixture의 Put·Run·Check·Reject를 재사용해 강제 줄바꿈·ANSI 오류, 다른 오류, 성공 종료를 검증한다.
3. 운영 코드·workflow는 변경하지 않는다. 수정 제거 변이가 새 회귀에서 실패하는지도 확인한다.

## Files

- 수정: setup.md의 회귀·지식 경로 및 이 Task 문서.
- 읽기 전용: merge-integration.ps1, integration-sync.test.ps1, common.ps1.

## Risks

- 오류 문구 비교를 완화하면서 다른 실패나 정상 종료까지 통과시키지 않아야 한다.
- 다른 Task·private 상태를 보존한다. 새 worktree는 만들지 않는다.

## Auto Verification

- 구역·문서·구문·diff 검사.
- 프로젝트 밖 scratchpad에서 workspace-flow 및 integration-sync 전체 회귀.
- 줄바꿈 수정 제거 변이 검출. 실제 HEAD/index/private 보존 확인.
- check-work -Mode EditMode로 컴파일·테스트 확인.
- 실제 Ubuntu GitHub CI는 새 PR 등록 전 미실행으로 구분한다.

## 공동 테스트 항목

실제 팀원 공동 테스트는 불필요 — 검사 코드만 변경. 아래 항목은 자동 fixture 검증이며 공동 테스트 PASS로 기록하지 않는다.

1. 예상 오류가 강제 줄바꿈·ANSI를 포함해도 실패 종료일 때만 인정한다.
2. 다른 오류·성공 종료는 예상 거부로 인정하지 않는다.
3. 기존 작업 흐름·동기화 회귀 통과, 수정 제거 시 새 회귀 실패, 실제 저장소·private 상태 보존.
