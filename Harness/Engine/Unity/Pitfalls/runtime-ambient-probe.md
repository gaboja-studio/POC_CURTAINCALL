---
summary: 런타임에 환경광 색(Trilight/Flat)을 바꿔도 화면이 어두워지지 않음 → ambientProbe·reflectionIntensity를 직접 조절
status: active
updated: 2026-11-03
source: Task-20261103-098 추격 전등 연출
---

# 런타임 환경광 변경이 반영되지 않음

## 언제 읽나

플레이 중 조명을 끄거나 암전·밝기 연출을 만들 때.

## 증상

`Light.intensity`를 0으로, `RenderSettings.ambientSkyColor/EquatorColor/GroundColor`를 0으로 내려도 방이 거의 그대로 밝다.

## 원인

실제 셰이딩은 에디터가 미리 계산한 `RenderSettings.ambientProbe`(SH)를 쓴다. 런타임에 환경광 색을 바꿔도 프로브는 다시 계산되지 않는다. 남는 회색은 Skybox 기본 반사(`reflectionIntensity`).

## 해결

시작 시 `ambientProbe`와 `reflectionIntensity`를 보관하고 `ambientProbe = 원본 * 배율`, `reflectionIntensity = 원본 * 배율`로 조절한 뒤 끝나면 원본으로 되돌린다. 예: `Assets/Scripts/WaitingRoom/Pursuit/PursuitLighting.cs`.
