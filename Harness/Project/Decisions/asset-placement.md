---
summary: C# 코드는 Assets/Scripts/, 씬은 Assets/Scenes/(테스트 씬은 Scenes/Tests/<Feature>/), 테스트 전용 프리팹은 테스트 씬 옆 Prefabs/, 불러와 쓰는 에셋은 Assets/Resources/
status: active
updated: 2026-09-30
source: human (2026-09-30 PM @MoHoDu)
---

# 에셋 배치 위치

## 언제 읽나

C# 스크립트·씬·프리팹·입력 에셋을 새로 만들 위치를 정하거나, Task `setup.md` 작업 구역을 적을 때.

## 결정

- 2026-09-30 / PM(@MoHoDu):
  - **Unity C# 코드는 반드시 `Assets/Scripts/` 아래**에 둔다(기능별 하위 폴더).
  - **씬은 반드시 `Assets/Scenes/` 아래**에 둔다. 테스트 씬은 `Assets/Scenes/Tests/<Feature>/`에 둔다. `Assets/_Sandbox/`는 쓰지 않는다.
  - 테스트에서만 쓰는 프리팹(더미·임시 캐릭터·테스트 UI)은 테스트 씬 옆 `Assets/Scenes/Tests/<Feature>/Prefabs/`에 둔다.
  - Unity에서 불러와 쓰는 에셋(입력 에셋, 다른 작업이 이어받는 프리팹 등)은 `Assets/Resources/` 분류에 둔다. 입력 에셋은 `Assets/Resources/Input/`.
  - `setup.md`의 수정 금지에는 `Assets/Scenes/`를 두고, 각 Task의 `Assets/Scenes/Tests/<Feature>/`만 작업 구역으로 연다(`verify-scope`는 작업 구역을 먼저 판정).

## 이유

코드와 씬 위치를 하나로 고정해 누구나 찾기 쉽게 하고, 테스트 씬과 테스트 전용 에셋을 한 폴더로 묶어 정리할 때 함께 지울 수 있게 한다.

## 바꾸려면

`Harness/Engine/Unity/Policies/code-folders.md`, `Docs/Guides/assets-folder-rules.md`, `Docs/Domains/assets-structure.md`, Task `setup.md` 템플릿을 함께 고친다.
