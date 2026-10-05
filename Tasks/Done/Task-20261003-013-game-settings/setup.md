# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-63

## 작업 구역

- 테스트 씬: 새로 만들지 않음. 플레이 확인은 기존 Assets/Scenes/Tests/Tightrope/(005)·Assets/Scenes/Tests/NetworkPlayerSync/(007)에서 하고 씬은 수정하지 않는다.
- 스크립트 폴더: `Assets/Scripts/Settings/` (세팅 클래스)
- 에셋 폴더: `Assets/Resources/GameSettings/` (`GameSettings`·`BaseGameSettings` 에셋, `Tricks/TightropeSettings`, `Tricks/Rewards/`는 015용 빈 폴더)
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

세팅으로 값을 옮기고(숫자 칸 삭제) 세팅을 읽게 바꾸는 것만 한다. 동작·판정 로직은 바꾸지 않는다.

- `Assets/Scripts/Player/` (004 → 007, 이번에 013)
- `Assets/Scripts/Tightrope/Rope/` (005, 이번에 013)
- `Assets/Scripts/Network/Session/NetworkSessionManager.cs` (정원·최소 인원)
- `Assets/Scripts/Network/PlayerSync/NetworkPlayer.cs` (균형 전송 기준)
- `Assets/Resources/Prefabs/Characters/Players/` (Player 프리팹 숫자 칸 정리)
- `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` (코스 프리팹 숫자 칸 정리)
- `Assets/Resources/Prefabs/Controllers/Network/` (NetworkManager 프리팹 숫자 칸 정리)

## 수정 금지

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`
- `Assets/Settings/` (URP 설정. 게임 세팅은 `Assets/Resources/GameSettings/`)
- `Assets/InputSystem_Actions.inputactions`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨) — 새 씬 없음, 기존 005·007 테스트 씬 사용
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`) — 2026-10-03 에디터로 생성
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
