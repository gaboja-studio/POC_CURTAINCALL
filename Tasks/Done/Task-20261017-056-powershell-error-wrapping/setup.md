# Setup

## JIRA

- 관련 JIRA: 없음 — PM 본인의 하네스 CI 수정, 순차 gate 없음.

## 작업 구역

- 회귀: `Scripts/Tests/workspace-flow.test.ps1`
- 지식: `Harness/Project/Pitfalls/powershell-error-output-wrapping.md`, `Harness/Project/Pitfalls/index.md`
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
- 운영 common.ps1·workflow·다른 Task 문서 (055·A/B 마감 포함)

## 인계 전 체크 (PM)

- [x] 관련 JIRA 없음 및 작업 구역·검증 항목 기록
- [x] 테스트 씬·폴더 불필요 — 격리 Git/PowerShell 회귀
- [x] plan.md 검증 항목 합의 — 기존 승인 계획의 3항목 유지
- [x] PM 본인 직접 준비·배정·착수 — 사용자 새 Task·브랜치 수정 요청
- 2026-10-17 새 Task 요청에 따라 PM 직접 fix 준비·작업. 일반 착수 gate는 수정하지 않음.
- 기존 승인된 수정 방법을 이 후속 Task에 적용: Read/Edit/Write, 편집 전 읽기·변경안 설명. 전용 편집 dryRun 없음.
- origin/dev 806e10d 기준 안전 전환. 씬 저장 확인, private pin c104565 불변.
- 2026-10-17 사용자 저장·PR 제출·온라인 검사 확인 요청. 미리보기 확인 후 등록, 병합·실제 동기화·C·private 변경은 제외.

## 테스트 씬 처리

- 결정: 불필요 — 검사 코드만 수정하며 Unity 에셋은 변경하지 않음.
- 결정자·날짜: PM @MoHoDu, 2026-10-17.
