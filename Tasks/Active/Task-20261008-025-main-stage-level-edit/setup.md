# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA:

## 작업 구역

- 맵 배치 씬: `Assets/Scenes/Levels/main_stage.unity` (공용 파일, 아래 배정)
- 테스트 씬: 없음 (맵 배치 씬에서 직접 배치)
- 스크립트 폴더: 없음 (에셋 배치만, 코드 작성 없음)
- 에셋 폴더: `Assets/Resources/Packages/MainStage/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- `Assets/Scenes/Levels/main_stage.unity` — 공연장 맵 배치 (이 Task만 수정)

## 수정 금지

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [ ] 관련 JIRA 번호를 모두 적었다
- [ ] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [ ] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [ ] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
