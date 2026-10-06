# Setup

015 → 023 → 016의 병합 후 연결한다. 공용 경로는 선행 Task 병합 뒤 이 Task로 인계되며 동시에 수정하지 않는다. 씬·프리팹은 Unity 에디터로만 저장한다.

## JIRA

- 관련 JIRA: 없음

## 작업 구역

- 지정 플레이 씬: `Assets/Scenes/Prototypes/`
- 연결 코드: `Assets/Scripts/Tightrope/Prototype/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Tightrope/Rope/TightropeRun.cs` — 015 병합 후, 재도전·성공 연결.
- `Assets/Scripts/Network/Session/NetworkSessionManager.cs`
- `Assets/Scripts/Network/PlayerSync/NetworkPlayer.cs`
- `Assets/Scripts/Network/PlayerSync/LocalPlayerViews.cs`
- `Assets/Scripts/Player/PlayerInputReader.cs`
- `Assets/Scripts/Player/PlayerCommandDebugHud.cs`
- `Assets/Scripts/Player/PlayerMover.cs`
- `Assets/Scripts/Player/TightropeControlScheme.cs`
- `Assets/Scripts/Tightrope/Rope/CourseControlSwitcher.cs`
- `Assets/Scripts/Tightrope/Rope/TightropeRunDebug.cs`
- `Assets/Scripts/Tightrope/Piggyback/PiggybackSystem.cs`
- `Assets/Scripts/Tightrope/Piggyback/PiggybackFollow.cs`
- `Assets/Scripts/Tightrope/Saws/TightropeSaws.cs` — 021 제출본의 소유권과 충돌하면 멈춘다. 발판 상태 hook만 최소 연결.
- `Assets/Scripts/Tightrope/Results/` — 015 병합 후 ID 매핑 연결이 필요한 부분만.
- `Assets/Scripts/Telemetry/`, `Assets/Scripts/Tightrope/Telemetry/` — 023 병합 후 행동 adapter 연결.
- `Assets/Scripts/Tightrope/UI/` — 016 병합 후 입력/재도전 계약 연결.

## 수정 금지

- `Assets/Resources/Fonts/`
- `Assets/Resources/Prefabs/Characters/`
- `Assets/Scripts/Settings/`
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] 지정 씬·기존 .meta 존재 확인, 기존 사용자 내용 보존
- [x] 연결 코드 폴더를 Unity에서 만들었다 (빈 폴더에는 .gitkeep)
- [x] plan.md에 공동 테스트 항목을 합의해 적었다

## 테스트 씬 처리

- 결정: 지정 플레이 씬 유지, 원본 테스트 씬 보존
- 결정자·날짜: 2026-10-06 사용자 후속 승인
