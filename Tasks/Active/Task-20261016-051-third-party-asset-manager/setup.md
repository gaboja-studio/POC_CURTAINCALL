# Setup

PM 내부 권한 검사 수정. 사용자 승인으로 fix 브랜치에서 dev에 직접 제출한다.
Unity 검사는 2026-10-16 PM의 이번 요청으로 생략한다. 구역 검사는 실제 PR 대상 origin/dev 기준으로 실행한다.
이전 Task-039 구현은 dev의 PR #68(2026-10-15 KST)에 반영됐다. 아래 경로는 이번 PM 후속 작업으로 재배정하며 당시 Task 문서는 보존한다.

## JIRA

- 관련 JIRA: 없음 — PM이 직접 승인한 에셋 관리자 위임

## 작업 구역

- 검사: `Scripts/Lib/common.ps1`, `Scripts/verify-scope.ps1`, `Scripts/verify-third-party.ps1`, `Scripts/doctor.ps1`
- 회귀: `Scripts/Tests/third-party-scope.test.ps1`
- 공통 규칙: `Harness/Core/Policies/team-roles.md`, `Harness/Core/Policies/git-workflow.md`
- 저장 안내·템플릿: `Harness/Core/Skills/save-work.md`, `Harness/Core/Templates/Task/setup.md`
- Unity 규칙: `Harness/Engine/Unity/Policies/asset-ownership.md`, `Harness/Engine/Unity/Policies/code-folders.md`
- 역할·결정: `Harness/Project/Facts/team.md`, `Harness/Project/Decisions/third-party-assets.md`
- 지도·안내: `Docs/Domains/assets-structure.md`, `Docs/Guides/assets-folder-rules.md`, `Docs/Guides/team-workflow.md`, `Docs/Guides/third-party-asset-guide.md`
- CI 검사 설명: `.github/workflows/verify-docs.yml`
- 통합 작업표: `Integrations/Active/Integration-harness-main-stage-workspaces/meta.md`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `ProjectSettings/EditorBuildSettings.asset` — 기존 1인칭 테스트 씬 목록 변경을 내용 그대로 별도 저장. 에셋 관리자에게 설정 권한을 주는 것은 아님.

## 수정 금지

- `Assets/`
- `Packages/`
- `ProjectSettings/` — 위에 명시한 기존 변경 저장만 예외
- 다른 Task 문서와 기존 구현 내용

## 인계 전 체크 (PM)

- [x] PM 승인 범위와 이전 Task dev 반영 근거 확인
- [x] Unity 씬·에셋 새 생성 불필요
- [x] 기존 파일·별도 저장할 변경 확인
- [x] 검사 기준을 plan.md에 기록

## 테스트 씬 처리

- 결정: 기존 1인칭 테스트 씬 유지. 신규 씬 생성·삭제 없음
- 결정자·날짜: 기존 변경 보존, PM 저장 요청 2026-10-16
