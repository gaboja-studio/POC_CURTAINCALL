# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: CC-121 — https://hrjoo122770.atlassian.net/browse/CC-121

## 작업 구역

- 없음 — #61 병합·기존 공동 테스트 PASS 및 통합 소유 표의 반납을 setup에 동기화(2026-10-15). Task 문서만 유지한다.

## 배정된 공용 파일

- 없음 — 기존 배정은 아래 이력. PlatformControlScheme·ControlRulesTests 두 파일만 Task-045에 재배정한다.

## 작업 구역 이력 (반납)

- 테스트 씬: `Assets/Scenes/Tests/Systems/ControlRules/` (ControlRules.unity)
- 스크립트 폴더: `Assets/Scripts/Systems/Control/` — 단 PlatformControlScheme.cs·Editor/Tests/ControlRulesTests.cs는 2026-10-15 Task-045에 반환·재배정, 이 Task의 현재 수정 구역에서 제외
- 에셋 폴더: `Assets/Resources/Settings/Control/`
- 지도 갱신: `Docs/Domains/common-game-systems.md`, `Docs/Domains/tightrope-course.md`, `Docs/Domains/player-sync.md`
- Task 문서: 이 Task 폴더 (자동 포함)

## 공용 파일 배정 이력 (반납)

아래는 구현 당시 배정이며 현재 수정 권한이 아니다.

- `Assets/Scripts/Systems/Player/PlayerController.cs`, `Assets/Scripts/Systems/Player/PlayerInputReader.cs`, `Assets/Scripts/Systems/Player/PlayerControlScheme.cs`, `Assets/Scripts/Systems/Player/PlayerCommandDebugHud.cs`, `Assets/Scripts/Systems/Player/TightropeControlScheme.cs` — 공용 조작 정리 (2026-10-14 PM)
- `Assets/Scripts/MainStage/Tightrope/Rope/PlatformControlScheme.cs`, `Assets/Scripts/MainStage/Tightrope/Rope/CourseControlSwitcher.cs`, `Assets/Scripts/MainStage/Tightrope/Prototype/TightropeInputGate.cs`, `Assets/Scripts/MainStage/Tightrope/UI/TightropeUIFocus.cs` — 공연장 소유. 공용으로 내보내거나 받아 오는 참조만 고친다(공간 작업 시작 전, PM 진행)
- 외줄 조작 규칙(`TightropeControlScheme`)을 받는 곳: `Assets/Scripts/MainStage/Tightrope/Control/`

## 수정 금지

- `Assets/Scenes/` (외줄 확인용 씬은 열어서 확인만, 저장하지 않음. 이 Task 테스트 씬 제외)
- `Assets/Scripts/Systems/Player/PlayerMover.cs`, `Assets/Scripts/Systems/Network/`, `Assets/Scripts/Systems/Settings/` (030·034 소유)
- `Assets/Scripts/Systems/Camera/`, `Assets/Scripts/Systems/LevelPreview/` (038 소유, 읽기만)
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM에게 요청

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-121)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨, 2026-10-14)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`, 2026-10-14)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다 (2026-10-14 PM 승인)

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 보류 — 모든 기능을 확인하는 총 테스트 씬이 생기면 기능별 테스트 씬을 한꺼번에 처리
- 결정자·날짜: PM @MoHoDu, 2026-10-14
