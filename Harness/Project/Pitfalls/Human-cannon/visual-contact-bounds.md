---
summary: 트램펄린 접촉은 현재 보이는 Renderer bounds도 사용하므로 소품 교체 시 실제 접촉 경계를 비교한다
status: active
updated: 2026-10-30
source: Task-20261030-082
---

# 트램펄린 외형과 접촉 판정

## 언제 읽나
트램펄린 프레임·손잡이·별 장식을 교체하거나 반동 시작 높이/코너 판정이 달라졌을 때.

## 확인한 동작
HumanCannonTrampoline.ContactBounds는 활성 Renderer.localBounds를 장치 좌표로 합친다. TryContact→TrampolineContact.TryTouch는 장치 전체 AABB와 플레이어 몸통의 접촉이며, 반동 시작 y는 Surface+device.max.y다. circularSurface·halfSize 값만 보존해도 동일한 런타임 판정은 보장되지 않는다. 현재 코드에서 옛 천의 (1.9,1.9) 코너도 장치에 닿아 반동한다.

## 대처와 확인
원본 씬과 새 씬에서 ContactBounds를 읽어 비교한다. Task-082 기준 Surface 상대 min=(-2.875,-.5,-2.4753408), max=(2.4753408,.15,2.4753408). 새 손잡이를 최고 .65m로 맞춰 원본 경계를 보존했다. Editor 아트 테스트에 이 계약을 추가했고, PlayMode 최고점은 실제 접촉 시작 높이로부터 측정한다. 규칙을 바꾸려면 별도 게임 의도 결정이 필요하다.
