---
summary: 카메라 렌더 캡처에는 Screen Space - Overlay UI가 빠짐 → capture_game_view source=screen 사용, 프로젝트 안에 저장했으면 PNG·.meta를 밖으로 옮김
status: active
updated: 2026-10-16
source: Task-20261014-035
---

# Overlay UI가 빠진 캡처

## 언제 읽나

AI가 Play Mode 화면을 캡처해 UI(HUD·테스트 패널)를 확인하려 할 때. 캡처에 3D 장면만 있고 UI가 안 보일 때.

## 증상

- `screenshot` 또는 `capture_game_view source=camera` 결과에 Canvas(Screen Space - Overlay) UI가 없다.
- 프로젝트 안 경로(`Assets/...`)에 저장한 캡처 PNG에 `.meta`가 생겨 git 변경으로 잡힌다.

## 원인

카메라 렌더는 카메라가 그리는 것만 담는다. Overlay Canvas는 카메라가 아니라 화면 위에 따로 그려져 카메라 캡처에 들어가지 않는다. `Assets/` 아래 파일은 Unity가 임포트해 `.meta`를 만든다.

## 대처

1. UI까지 보려면 `capture_game_view`에 `source=screen`을 쓴다(Game 창이 실제로 보이는 그대로).
2. 결과는 인라인으로 받거나 프로젝트 밖(scratchpad)에 저장한다. 꼭 프로젝트 경로에 저장해야 하면 Task가 소유한 경로를 명시하고, 확인 뒤 PNG·`.meta`를 scratchpad로 옮겨 git에 남기지 않는다.

## 확인 방법

캡처에 테스트 패널·HUD가 보이는지 확인하고, `git status`에 캡처 PNG·`.meta`가 없는지 본다.
