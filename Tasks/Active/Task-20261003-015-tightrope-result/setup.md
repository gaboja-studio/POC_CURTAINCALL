# Setup

작업자와 AI는 아래 작업 구역과 배정된 공용 파일만 수정한다. 씬은 Unity 에디터로만 저장한다.

## JIRA

- 관련 JIRA: 없음

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeResults/`
- 스크립트 폴더: `Assets/Scripts/Tightrope/Results/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Tightrope/Rope/TightropeRun.cs` — 014 병합 완료 후 015로 인계. 종료 전 결과 경계·이유만 추가하고 Fire 판정 순서를 유지한다. 015 병합 후 022로 인계한다.

## 수정 금지

- `Assets/Scenes/Prototypes/`
- `Assets/Resources/Fonts/`
- `Assets/Resources/Prefabs/Characters/`
- `Assets/Scripts/Settings/`
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (.meta 생성됨)
- [x] 스크립트 폴더를 만들었다 (빈 폴더에는 .gitkeep)
- [x] plan.md에 공동 테스트 항목을 합의해 적었다

## 테스트 씬 처리

- 결정: 미정 (유지 / 삭제), 인계와 검증 동안 보존
- 결정자·날짜: 종료 시 사람 결정
