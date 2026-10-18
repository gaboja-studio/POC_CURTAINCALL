# Plan

## Context

PR #147의 Ubuntu verify-fast job에서 Git 경로가 /usr/bin/git /bin/git으로 합쳐져 integration-sync 첫 Created 회귀가 실패했다. 문서 검사 자체는 성공, 후속 workspace-flow는 미실행이었다.

## Approach

1. 초기 $git·mock FixtureGit 두 곳을 common.ps1의 Application·ErrorAction Stop·Select-Object -First 1 패턴과 통일한다.
2. 실제 선택 대입문을 AST로 추출하고 격리 Get-Command stub의 후보 두 개로 단일 문자열·첫 후보·실행 성공을 검증한다. 수정식을 중복 복제하지 않는다.
3. 지식 파일·index를 기록한다. 운영 common.ps1·workflow·다른 테스트는 변경하지 않는다.
4. 미커밋 상태로 결과 보고. 제출 후 실제 Ubuntu CI 성공은 별도 확인한다.

## Files

- setup.md의 최소 구역만 수정한다.
- 참고: Scripts/Lib/common.ps1의 Invoke-TransitionGit, workspace-flow·third-party-scope의 단일 경로 선택.

## Risks

- 모사 테스트를 실제 Ubuntu 실행·공동 테스트로 보고하지 않는다.
- 로컬 WSL Ubuntu에 pwsh 없음. 설치/환경 변경 없이 ENV_BLOCKED로 기록한다.
- A/B 마감·실제 동기화·C/private는 별도 승인이다.

## Auto Verification

- 구역·문서/규칙·구문·diff check, verify-knowledge.
- 실제 선택식의 기존 실패→수정 후 성공 비교.
- 격리 integration-sync·workspace-flow·third-party-branch-sync·third-party-scope 회귀.
- check-work -Mode EditMode의 구역→문서→컴파일→테스트.
- HEAD/index/private·원격 쓰기 경계 보존. 실패 뒤 후속 단계는 중단한다.

## 공동 테스트 항목

1. 복수 Git 후보에서 실제 두 선택식이 첫 후보의 단일 경로를 반환하고 --version 실행 성공. 자동 회귀와 실제 공동 검증을 구분한다.
2. 기존 동기화 생성·재사용·차단·사용자 상태 보존 회귀가 유지됨. 실제 원격 PR 운용은 수행하지 않는다.
3. 제출 승인 후 GitHub Ubuntu job 전체 통과 확인. CI 성공을 실제 부모·자식 동기화 E2E·공동 플레이로 대체하지 않는다.
