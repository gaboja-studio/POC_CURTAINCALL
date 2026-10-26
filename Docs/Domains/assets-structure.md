# Unity Assets 구조

## 목적

폴더는 배치·탐색 기준이며 수정 권한 경계가 아니다. 작업 목적 안에서 관련 기능을 수정·연결하고 겹치는 변경은 조율한다.

## 현재 확인된 경로 (2026-09-30)

- `Assets/Resources/` — 경로 로딩 대상 분류 폴더. `Animations/`, `Effects/`, `Images/`, `Models/`, `Sounds/`, `Prefabs/`가 있으며 콘텐츠는 아직 없다.
- `Assets/Resources/Prefabs/` — `Characters/{Enemies,NPCs,Players}/`, `Objects/{Decorations,ETCs,Interactables,Obstacles,Tools}/`, `Controllers/`, `UIs/` 분류. 역할과 예외는 가이드에 정의.
- `Assets/Scenes/SampleScene.unity` — 템플릿 씬. 겹치는 씬 수정은 담당자와 조율.
- `Assets/Settings/` — URP 렌더 설정과 Volume Profile. 공용 에셋.
- `Assets/InputSystem_Actions.inputactions` — 루트의 공용 입력 액션.
- `Assets/_ThirdParty/` — 외부 에셋 private submodule(DOTween·Pro·DemiLib). PM·지정 에셋 관리자만 추가·수정·저장·제출(`Harness/Project/Decisions/third-party-assets.md`).
- `Assets/Readme.asset`, `Assets/TutorialInfo/` — 템플릿 잔여물. 보존/정리 결정 전까지 게임 기능 작업 구역 아님.

## Task 작업 경로 (2026-09-30 폴더 생성, 콘텐츠 없음)

- `Assets/Scripts/<Feature>/` — 담당자가 필요할 때 만드는 기능 코드 폴더. 현재 `Network/Session/`, `Player/`, `Tightrope/Rope/`, `Tightrope/Piggyback/`(빈 폴더).
- `Assets/Scenes/Tests/<Feature>/` — 담당자가 만드는 Task별 테스트 씬 폴더. 테스트 전용 프리팹은 그 안의 `Prefabs/`.
- `Assets/Resources/Input/` — 플레이어 조작 입력 에셋.
- 위치 규칙: C# 코드는 `Assets/Scripts/`에만, 씬은 `Assets/Scenes/`에만 둔다(`Harness/Project/Decisions/asset-placement.md`).

## 진입점·의존성·수정 주의

폴더는 배치·탐색 기준이며 수정 권한 경계가 아니다. 작업 목적 안에서 관련 기능을 수정·연결하고 겹치는 변경은 조율한다.
폴더는 배치·탐색 기준이며 수정 권한 경계가 아니다. 작업 목적 안에서 관련 기능을 수정·연결하고 겹치는 변경은 조율한다.
- 새 에셋·폴더의 `.meta`/GUID 및 기존 참조를 보존한다. 템플릿 삭제, 공용 설정 변경, 새 공유 코드 계층은 별도 결정이 필요하다.
- 구조·용도가 바뀌면 가이드의 재점검 체크리스트를 수행하고 이 지도와 함께 갱신한다.

## 기획자용 에셋 도우미

- Windows `asset-helper.vbs`, Mac `asset-helper-mac.zip`의 Finder 앱 → `Scripts/Asset-helper/launch.ps1` → `start.ps1` 로컬 UI.
- `core.ps1`: 상태 스냅샷·비공개 에셋 저장·게임 gitlink만 저장·맞추기·보관 복구. `worker.ps1`이 별도 프로세스에서 실행한다.
- [사용 안내](../Guides/asset-helper.md), [LFS 복구 함정](../../Harness/Project/Pitfalls/asset-helper-lfs-restore.md).
