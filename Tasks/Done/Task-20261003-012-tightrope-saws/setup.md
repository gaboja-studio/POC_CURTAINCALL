# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-64

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeSaws/` (TightropeSaws.unity — 005 외줄 테스트 씬 복사본, 2026-10-03)
- 스크립트 폴더: `Assets/Scripts/Tightrope/Saws/` (톱날·발판·`SawSettings`)
- 에셋 폴더: `Assets/Resources/Prefabs/Objects/Obstacles/Saws/` (가로·수직 톱날, 발판 프리팹)
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Tightrope/Rope/TightropeRun.cs` — 묘기 시작 조건만 변경(전원 → 첫 사람이 줄에 오름, 2026-10-03 PM 배정. 005 병합 #18 이후)
- `Assets/Scripts/Tightrope/Rope/TightropeRunDebug.cs` — 위 변경에 맞춰 디버그 표시 문구 1줄만
- 그 밖에는 없음. 코스 프리팹은 테스트 씬에 놓기만 한다. 005 진입점(`TightropeCourse`·`TightropeRun`)·007 상태 요청은 공개 기능만 사용.
- `Assets/Scripts/Settings/TightropeSettings.cs`, `Assets/Resources/GameSettings/Tricks/TightropeSettings.asset` — 톱날 칸(`SawSettings`) 추가만(2026-10-05 PM 배정, 013·011·020 병합 후)

## 수정 금지

- `Assets/Scenes/` (자기 테스트 씬 폴더 제외)
- `ProjectSettings/`
- `Packages/`
- `Assets/Settings/`
- `Assets/InputSystem_Actions.inputactions`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 유지 (공간별 재배치로 Assets/Scenes/Tests/MainStage/Tightrope/·Tests/Systems/에 보존)
- 결정자·날짜: PM @MoHoDu, 2026-10-08
