---
summary: 현재 Unity 프로젝트 구성 — 씬, 렌더 파이프라인 에셋, 입력, 테스트 어셈블리 유무
status: active
updated: 2026-10-13
source: 2026-10-13 저장소 확인 (Task-20261013-029)
---

# Unity 프로젝트 구성

## 언제 읽나

씬·에셋 경로를 찾거나, 테스트 어셈블리를 만들거나, 렌더/입력 설정을 건드리는 Task를 계획할 때.

## 사실

- 씬(2026-10-13 확인): 공간 맵 배치 씬 `Assets/Scenes/Levels/`(main_stage·waiting_room·backstage·escape_route, 둘러보기용 `PlaySetup` 수준), 외줄 프로토타입 `Assets/Scenes/Prototypes/proto_0.0.2.unity`, 기능 테스트 씬 `Assets/Scenes/Tests/`(MainStage/Tightrope/*, Systems/*), 템플릿 `SampleScene.unity`. Build Settings 등록은 `SampleScene`·`proto_0.0.2`뿐
- 렌더: URP, `Assets/Settings/`에 `PC_RPAsset`/`PC_Renderer`, `Mobile_RPAsset`/`Mobile_Renderer`, `DefaultVolumeProfile`
- 입력: 게임 입력은 `Assets/Resources/Input/PlayerControls.inputactions`(역할 Move·Posture·Action·DirectionLeft/Right·Interact) + 조작 규칙 `TightropeControlScheme.asset`. 템플릿 `Assets/InputSystem_Actions.inputactions`도 남아 있음. Active Input Handling = Input System only
- 템플릿 잔여물: `Assets/Readme.asset`, `Assets/TutorialInfo/` (삭제 여부는 사람 결정)
- 게임 코드: `Assets/Scripts/`(Systems/·MainStage/Tightrope/, C# 76개). asmdef 없음(전부 Assembly-CSharp, `internal`도 공간 코드에서 보임)
- 테스트 어셈블리: 없음 → 현재 테스트 결과는 `NO_PROJECT_TESTS`
- 버전 값: `Harness/Engine/Unity/Decisions/version.md`

## 확인 방법

```
find Assets -name "*.unity" -o -name "*.asmdef"
```
