# Setup

PM이 작업 구역을 정한 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다.

- 관련 JIRA: (발급 대기)

## 작업 구역

- 테스트 씬: 해당 없음 (하네스 스크립트 수정)
- 스크립트: `Scripts/verify-unity.ps1`, `Scripts/check-work.ps1`, `Scripts/Lib/common.ps1`(작업 구역 경로 인식)
- 팀 안내: `Docs/Guides/team-workflow.md` ("검사해줘"는 Unity 에디터를 연 상태에서)
- 지식 문서: `Harness/Engine/Unity/Pitfalls/unity-test-editor-open.md`, `Harness/Engine/Unity/Pitfalls/index.md`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- 위 스크립트·지식 문서 — 하네스(PM 전용 영역), 담당 @MoHoDu

## 수정 금지

- `Assets/`
- `Scripts/` 중 위에 적지 않은 파일
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [ ] 관련 JIRA 번호를 모두 적었다
- [x] Unity 씬·스크립트 변경이 없는 하네스 Task임을 확인했다
- [x] `plan.md`에 공동 테스트 항목을 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.
