---
summary: 현재 Unity 프로젝트 구성 — 씬, 렌더 파이프라인 에셋, 입력, 테스트 어셈블리 유무
status: active
updated: 2026-09-29
source: 2026-09-29 저장소 확인
---

# Unity 프로젝트 구성

## 언제 읽나

씬·에셋 경로를 찾거나, 테스트 어셈블리를 만들거나, 렌더/입력 설정을 건드리는 Task를 계획할 때.

## 사실

- 씬: `Assets/Scenes/SampleScene.unity` (템플릿 기본 씬, 대표 씬 미정)
- 렌더: URP, `Assets/Settings/`에 `PC_RPAsset`/`PC_Renderer`, `Mobile_RPAsset`/`Mobile_Renderer`, `DefaultVolumeProfile`
- 입력: `Assets/InputSystem_Actions.inputactions`, Active Input Handling = Input System only
- 템플릿 잔여물: `Assets/Readme.asset`, `Assets/TutorialInfo/` (삭제 여부는 사람 결정)
- 게임 코드·asmdef: 없음
- 테스트 어셈블리: 없음 → 현재 테스트 결과는 `NO_PROJECT_TESTS`
- 버전 값: `Harness/Engine/Unity/Decisions/version.md`

## 확인 방법

```
find Assets -name "*.unity" -o -name "*.asmdef"
```
