# Setup

## JIRA

- 관련 JIRA: 없음 — PM 본인의 로컬 하네스 CI 수정, 순차 gate 없음.

## 작업 구역

- 회귀: `Scripts/Tests/integration-sync.test.ps1`
- 지식: `Harness/Project/Pitfalls/ubuntu-git-executable-resolution.md`, `Harness/Project/Pitfalls/index.md`
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
- 운영 common.ps1·workflow·다른 Task 문서 (A/B 마감 포함)

## 인계 전 체크 (PM)

- [x] 관련 JIRA 없음 및 작업 구역·검증 항목 기록
- [x] 테스트 씬·에셋 폴더 불필요 — 격리 Git/PowerShell 회귀
- [x] plan.md 공동 테스트 항목 합의 — 승인 계획의 최대 3항목
- [x] PM 본인 직접 준비·배정·착수 단회 예외 승인 — 실팀원 전달 N/A
- 2026-10-17 Read/Edit/Write 승인. 매 편집 전 재독·변경안 설명, 전용 편집 dryRun 없음.
- 최신 dev c55da6b에서 안전 전환, private pin 불변. 새 worktree·원격 쓰기 없음.
- 커밋·push·PR 제출·병합, 실제 동기화·C·private 변경은 별도 승인.

## 테스트 씬 처리

- 결정: 불필요 — 테스트 하네스 CI 결함이며 Unity 에셋 변경 없음.
- 결정자·날짜: PM @MoHoDu, 2026-10-17 승인 계획.
