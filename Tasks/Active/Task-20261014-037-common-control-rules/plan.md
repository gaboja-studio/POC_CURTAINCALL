# Plan

## Context

공용 조작 코드가 공용 폴더(`Assets/Scripts/Systems/Player/`)에 있지만 아직 외줄타기에 묶여 있다(Confluence 19398663 6장, 2026-10-14 확인).

- `PlayerController`가 조작 규칙이 비면 `TightropeControlScheme`을 직접 만들어 쓴다 → 대기실에서 규칙을 안 끼우면 외줄처럼 움직인다.
- UI가 열렸을 때 입력을 막는 `TightropeInputGate`가 공연장 폴더(`MainStage/Tightrope/Prototype/`)에 있고 `TightropeUIFocus`(외줄 UI)만 본다. 공용 `PlayerInputReader`·`PlayerCommandDebugHud`가 이것을 직접 부른다.
- 공간 작업자(기획자, AI로 작업)가 대기실 UI·이동을 만들기 시작하면 바로 부딪히므로 그 전에 정리한다. **외줄타기 동작은 바꾸지 않는다.**

## Approach

1. **기본 이동 규칙**: `PlatformControlScheme`(8방향 이동·점프, 외줄 의존 없음)을 공용 기본 이동 규칙으로 옮긴다(`.meta`째, GUID 유지). 공용은 규칙이 비면 "현재 기본 이동 규칙"을 쓴다. `TightropeControlScheme`은 공연장 폴더(`MainStage/Tightrope/Control/`)로 옮긴다(`CourseControlSwitcher`가 줄 위에서 끼우는 방식은 그대로).
2. **에디터에서 기본 이동 규칙 바꾸기**: 조작 설정 에셋 1개(`Assets/Resources/Settings/Control/`)에 "기본 이동 규칙" 칸을 둔다. 인스펙터에서 프로젝트 안의 조작 규칙 에셋 목록 중 하나를 고른다. 메뉴(예: `CurtainCall/조작/기본 이동 규칙 선택`)로 바로 열리게 한다. 플레이 중 바꾸면 규칙을 끼우지 않은 플레이어에게 바로 반영.
3. **공간별 이동 규칙 연결(공용 수정 없이)**: 각 공간은 자기 폴더에 `PlayerControlScheme`을 상속한 규칙 에셋을 만들고 (a) 위 목록에 자동으로 나타나 기본으로 고르거나 (b) 씬에 둘 수 있는 공용 "조작 규칙 구역/적용" 컴포넌트에 넣어 들어갈 때 끼운다. 공용 코드는 구체 규칙 이름을 모른다. 예시로 `LevelPreviewControlScheme`(바라보는 방향 기준 이동)도 목록에 나오는지 확인.
4. **UI 입력 막기 공용화**: `TightropeInputGate`를 공용 입력 막기로 옮기고 이름에서 외줄을 뺀다. 어떤 UI든 "열림/닫힘"을 알리는 공용 신호(요청한 쪽 목록, 하나라도 열려 있으면 막음)를 둔다. `TightropeUIFocus`는 그 신호를 보내기만 한다. 막는 키 목록은 입력 에셋의 역할 기준으로 바꾸되 외줄 개발 키(P·O·B·R·M·K·L)도 지금처럼 막히게 유지.
5. 지도 `Docs/Domains/common-game-systems.md`·`tightrope-course.md`·`player-sync.md`의 경로·설명 갱신. Confluence 19398663 5·6장 갱신은 PM.

## Files

- 수정 예정: `Assets/Scripts/Systems/Player/`(조작 관련), 새 `Assets/Scripts/Systems/Control/`, 공연장 `MainStage/Tightrope/Rope/`·`Prototype/`·`UI/TightropeUIFocus.cs`, `Assets/Resources/Settings/Control/`, 지도 3개
- 읽기 전용 참고: `Systems/LevelPreview/`, `Assets/Resources/Input/PlayerControls.inputactions`, `Assets/Resources/Prefabs/Characters/Players/Player.prefab`
- 건드리지 않음: `PlayerMover.cs`·`NetworkPlayer.cs`·`BaseGameSettings`(030 소유), `PlayerBalance`(외줄 전용 유지 결정), `LocalPlayerViews.cs`의 외줄 재시작 참조(030)

## Risks

- `.meta` 없이 옮기면 Player·코스 프리팹의 규칙 참조가 Missing이 된다.
- 기본 규칙이 외줄 규칙 → 플랫폼 이동으로 바뀌므로, 외줄 씬에서 코스 진입 전 첫 프레임 조작이 달라질 수 있다. `CourseControlSwitcher`가 덮어쓰는 시점을 확인해 외줄 체감이 같게 한다.
- 038(카메라)과 병렬: 1인칭 시점 이동은 "바라보는 방향 기준" 규칙이 필요하다. 이 Task는 카메라 코드를 부르지 않고, 기존 `LevelPreviewControlScheme` 방식(카메라 방향을 읽는 규칙)을 목록에서 고를 수 있게만 한다. 1인칭용 정식 규칙은 038 결과를 보고 PM이 정한다.
- 공용 파일을 034·030과 나눠 쓰지 않는다(아래 공용 파일 배정 참고). 겹치면 PM에게 요청.

## Auto Verification

- 구역 검사: `pwsh -File Scripts/verify-scope.ps1`
- 1 문서/규칙: `pwsh -File Scripts/verify-fast.ps1`
- 2 컴파일: `pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 기본 규칙 선택(비었을 때 기본, 바꾸면 반영)·입력 막기(요청자 여러 명) EditMode 테스트, 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: `proto_0.0.2.unity`·이 Task 테스트 씬에서 Missing 0, Console 오류 0

## 공동 테스트 항목

2026-10-14 PM 승인. 플레이 테스트는 작업자가 하고 결과를 handoff에 적는다.

1. 외줄타기(`proto_0.0.2.unity`)에서 플랫폼 이동·줄 위 조작·UI 입력 막기가 전과 똑같다.
2. 조작 규칙을 끼우지 않은 테스트 씬에서 캐릭터가 기본 이동 규칙(8방향 이동·점프)으로 움직이고, 에디터에서 기본 이동 규칙을 다른 규칙으로 바꾸면 바로 그 규칙으로 움직인다.
3. 외줄이 아닌 테스트용 UI를 열면 이동·점프 입력이 막히고, 닫고 키를 뗀 뒤 다시 움직인다.
