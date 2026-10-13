# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: CC-108 — https://hrjoo122770.atlassian.net/browse/CC-108

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/Systems/Campaign/CampaignFlow.unity`
- 스크립트 폴더: `Assets/Scripts/Systems/Campaign/`
- 에셋 폴더: 없음 (이 Task에서 신규 에셋 불필요)
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- `Assets/Scripts/Systems/Settings/` — 세팅 5묶음 틀(새 세팅 클래스 포함)과 공통 값 칸 추가. 1단계 소유, 2단계는 PM이 순서 배정
- `Assets/Resources/Settings/GameSettings/` — 위 세팅 에셋

## 수정 금지

- `Assets/Scenes/` (단, 위에 명시한 `Assets/Scenes/Tests/Systems/Campaign/` 테스트 씬만 작업 구역으로 허용)
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가·이동은 PM에게 요청

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-108)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트 폴더를 만들었다 (`.meta` 생성됨)
- [x] `plan.md`에 제안 공동 테스트 항목과 미확정 연결 범위를 기록했다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
