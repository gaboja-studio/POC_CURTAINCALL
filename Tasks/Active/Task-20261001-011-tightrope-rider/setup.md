# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-53

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeRider/` (TightropeRider.unity, 테스트용 프리팹은 Prefabs 하위)
- 스크립트 폴더: `Assets/Scripts/Tightrope/Rider/`
- 에셋 폴더: 테스트 씬 폴더에 포함 (005 코스 프리팹은 배치만)
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다. (2026-10-03 PM 배정)

- `Assets/Scripts/Settings/TightropeSettings.cs`, `Assets/Resources/GameSettings/Tricks/TightropeSettings.asset` — 손잡기 칸(동료 거리 1.0m·감소 50%)만 추가. 012(톱날 칸)와 같은 파일이라 먼저 병합되는 쪽 뒤에 최신 통합을 받는다
- `Assets/Scripts/Player/PlayerMover.cs` — 뒤쪽 착지에 필요한 최소 수정만(착지 위치 보정 진입점 등). 020(신체 손상)도 Player를 고치므로 병합 순서는 PM이 정한다
- `Assets/Scripts/Network/PlayerSync/TestLaneLanding.cs` — 판정으로 대체 후 삭제
- `Assets/Scenes/Tests/NetworkPlayerSync/NetworkPlayerSync.unity` — 위 삭제에 따라 `TestLaneLanding` 컴포넌트만 제거(그 외 수정 금지)

## 수정 금지

- `Assets/Scenes/`
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

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
