# Setup

작업 구역 안에서만 구현한다. 씬·프리팹은 Unity 에디터로 저장한다.

## JIRA

- 관련 JIRA: 없음

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeUI/`
- 스크립트 폴더: `Assets/Scripts/Tightrope/UI/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Resources/Prefabs/UIs/Tightrope/` — 데모 팝업·HUD 전용. 기존 BalanceGauge·폰트는 재사용만 한다.

## 수정 금지

- `Assets/Scenes/Prototypes/`
- `Assets/Resources/Fonts/`
- `Assets/Resources/Prefabs/Characters/`
- `Assets/Scripts/Network/`
- `Assets/Scripts/Player/`
- `Assets/Scripts/Settings/`
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (.meta 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 .gitkeep)
- [x] plan.md에 공동 테스트 항목을 합의해 적었다

## 테스트 씬 처리

- 결정: 미정 (유지 / 삭제), 인계와 검증 동안 보존
- 결정자·날짜: 종료 시 사람 결정
