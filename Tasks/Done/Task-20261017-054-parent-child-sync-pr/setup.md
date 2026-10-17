# Setup

## JIRA

- 관련 JIRA: 없음 — PM 본인의 로컬 하네스 작업, 순차 gate 없음.

## 작업 구역

- 동기화: `Scripts/sync-integration.ps1`, `Scripts/Lib/common.ps1`
- 기존 연결: `Scripts/new-pr.ps1`, `Scripts/merge-integration.ps1`
- 회귀: `Scripts/Tests/integration-sync.test.ps1`, `Scripts/Tests/workspace-flow.test.ps1`
- CI: `.github/workflows/verify-docs.yml` (PM 명시 배정)
- 절차: `Harness/Core/Skills/sync-integration.md`, `Harness/Core/Skills/merge-integration.md`
- 정책: `Harness/Core/Policies/skill-routing.md`, `Harness/Core/Policies/git-workflow.md`
- 준비 기록: `Integrations/Active/Integration-parent-child-sync-pr/` (PM 명시 배정)
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
- 다른 Task 문서 (A 마감 포함)

## 인계 전 체크 (PM)

- 2026-10-17 사용자 승인: Read·Edit·Write, 편집 전 내용 확인·변경안 설명. 전용 dryRun 도구 없음.
- 준비 문서 로컬 commit 하나 후 fix 안전 생성. PM 본인 기술 배정·착수 직접 기록, 실제 팀원 전달 N/A.
- 일반 assign/start gate 변경 없음. 새 worktree·외부 push/PR/merge·private 변경 없음.

## 테스트 씬 처리

- 결정: 불필요 — 하네스와 격리 Git/gh mock 회귀 작업.
- 결정자·날짜: PM @MoHoDu, 2026-10-17 승인 계획.
