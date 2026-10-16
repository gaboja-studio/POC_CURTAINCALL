# Setup

## JIRA

- 관련 JIRA: 없음 — PM 로컬 하네스 수정, 순차 gate 없음.

## 작업 구역

- 하네스 구현: `Scripts/Lib/common.ps1`
- 전환 호출부: `Scripts/start-work.ps1`, `Scripts/new-integration.ps1`, `Scripts/close-integration.ps1`, `Scripts/worktree.ps1`
- fetch 재귀 억제: `Scripts/assign-task.ps1`, `Scripts/new-task.ps1`, `Scripts/new-pr.ps1`
- 진단: `Scripts/doctor.ps1`, `Scripts/verify-third-party.ps1`
- 회귀: `Scripts/Tests/third-party-branch-sync.test.ps1`, `Scripts/Tests/workspace-flow.test.ps1`, `Scripts/Tests/third-party-scope.test.ps1`
- 지식: `Harness/Project/Pitfalls/third-party-branch-sync.md`, `Harness/Project/Pitfalls/index.md`
- 준비 기록: `Integrations/Active/Integration-third-party-branch-sync/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음

## 배정된 공용 파일

- 없음 — Unity 씬·프리팹·설정 변경 없음.

## 수정 금지

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.githooks/`
- `.github/`

## 인계 전 체크 (PM)

- PM 본인 작업. 2026-10-17 계획 승인으로 로컬 Task 발급 후 준비 커밋 없이 구현하는 단회 예외.
- 정상 작업자 착수 gate는 우회 구현하거나 완화하지 않는다.
- 2026-10-17 후속 저장 요청·경로 판단 위임: fix 브랜치 commit/push 허용, 제출 대상 dev. 별도 원격 통합은 만들지 않는다.
- dev 직접 push·PR/merge, 새 worktree, private 속성·gitlink 변경은 이번 범위 밖이다.

## 테스트 씬 처리

- 결정: 불필요 — PowerShell/Git fixture 회귀만 추가, Unity 씬 변경 없음.
- 결정자·날짜: PM @MoHoDu, 2026-10-17 승인 계획.
