# Setup

PM이 작업 문서와 조사 구역을 정한 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다.

- 관련 JIRA: CC-107 — https://hrjoo122770.atlassian.net/browse/CC-107

## 작업 구역

- 조사 결과 문서: `Docs/Domains/common-game-systems.md`
- Task 문서: 이 Task 폴더 (자동 포함)
- Unity 테스트 씬 및 스크립트: 해당 없음 (기존 구현을 읽어 문서화하는 조사 Task)

## 배정된 공용 파일

- 없음

## 수정 금지

- 조사 대상으로 지정된 기존 코드와 씬은 읽기 전용이며 수정하지 않는다.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-107)
- [x] Unity 씬·스크립트 변경이 없는 문서 조사 Task임을 확인했다
- [x] `Docs/Domains/common-game-systems.md` 조사 결과 문서 자리(빈 틀)를 만들었다 (내용은 작업자가 채움)
- [x] `plan.md`에 조사 기준과 공동 확인 항목을 기록했다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다. 인계 전에 문서 결과를 완성하고 확인한다.
