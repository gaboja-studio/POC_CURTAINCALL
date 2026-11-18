---
summary: 외줄의 소각 예고 중 실제 밧줄이 보이는 시간과 패키지 셰이더의 소실 속성을 확인한다.
status: active
updated: 2026-11-18
source: Task-20261118-162
---

# 소각 경고 중 밧줄이 너무 일찍 사라질 때

외줄 묘기 씬에서 쓰는 DeadleafGames의 `Mat_DissolveBurn`은 `Shader Graphs/DissolveShader`의 `_DissolveAmount`를 읽는다. 머티리얼 YAML에는 다른 셰이더의 `_burnAmount`도 남아 있다.

밧줄의 UV와 노이즈 분포에 따라 소실량이 1에 도달하기 전에 모든 픽셀이 잘릴 수 있다. 실제 `WalkingRope`와 `TouringRigging` 조합으로 예고 직전까지 가닥이 보이는지 렌더링한다. 소각 범위는 `TightropeFire.GetSegmentSpans`에서 얻는다. 경고 시각과 지지면 제거는 기존 화재 상태가 결정한다.
