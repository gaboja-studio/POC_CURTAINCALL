# Unity Assets 구조

## 목적

Unity 에셋·기능 코드 작업 시 실제 경로와 Task 소유 범위를 찾는 지도다. 폴더별 역할·배치 기준·예외·재점검 절차의 원본은 [Assets 폴더 규칙](../Guides/assets-folder-rules.md)을 읽는다. 최종 작업 권한은 현재 Task `setup.md`와 Integration 공용 파일 소유 표에서 확인한다.

## 현재 확인된 경로 (2026-09-30)

- `Assets/Resources/` — 경로 로딩 대상 분류 폴더. `Animations/`, `Effects/`, `Images/`, `Models/`, `Sounds/`, `Prefabs/`가 있으며 콘텐츠는 아직 없다.
- `Assets/Resources/Prefabs/` — `Characters/{Enemies,NPCs,Players}/`, `Objects/{Decorations,ETCs,Interactables,Obstacles,Tools}/`, `Controllers/`, `UIs/` 분류. 역할과 예외는 가이드에 정의.
- `Assets/Scenes/SampleScene.unity` — 템플릿 씬. 공용 씬 수정은 파일 소유 배정 필요.
- `Assets/Settings/` — URP 렌더 설정과 Volume Profile. 공용 에셋.
- `Assets/InputSystem_Actions.inputactions` — 루트의 공용 입력 액션.
- `Assets/Readme.asset`, `Assets/TutorialInfo/` — 템플릿 잔여물. 보존/정리 결정 전까지 게임 기능 작업 구역 아님.

## Task 작업 경로 (2026-09-30 폴더 생성, 콘텐츠 없음)

- `Assets/Scripts/<Feature>/` — PM이 Task별로 생성·배정하는 기능 코드 폴더. 현재 `Network/Session/`, `Player/`, `Tightrope/Rope/`, `Tightrope/Piggyback/`(빈 폴더).
- `Assets/Scenes/Tests/<Feature>/` — PM이 만드는 Task별 테스트 씬 폴더. 테스트 전용 프리팹은 그 안의 `Prefabs/`.
- `Assets/Resources/Input/` — 플레이어 조작 입력 에셋.
- 위치 규칙: C# 코드는 `Assets/Scripts/`에만, 씬은 `Assets/Scenes/`에만 둔다(`Harness/Project/Decisions/asset-placement.md`).

## 진입점·의존성·수정 주의

- 코드 배치는 [`code-folders.md`](../../Harness/Engine/Unity/Policies/code-folders.md), 공용 파일은 [`asset-ownership.md`](../../Harness/Engine/Unity/Policies/asset-ownership.md)를 따른다. 루트 `Scripts/`와 `Assets/TutorialInfo/Scripts/`는 기능 코드 배정 대상이 아니다.
- 다른 Task의 `setup.md` 소유 구역을 수정하지 않는다. 기능 간 연결은 별도 연결 Task에 경로를 배정한다.
- 새 에셋·폴더의 `.meta`/GUID 및 기존 참조를 보존한다. 템플릿 삭제, 공용 설정 변경, 새 공유 코드 계층은 별도 결정이 필요하다.
- 구조·용도가 바뀌면 가이드의 재점검 체크리스트를 수행하고 이 지도와 함께 갱신한다.
