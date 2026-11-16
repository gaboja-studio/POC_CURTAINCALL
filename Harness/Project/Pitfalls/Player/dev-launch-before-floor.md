---
summary: 치트 최초 호스팅은 목적지 바닥보다 플레이어를 먼저 생성한다. 입력 잠금만으로는 중력 낙하와 MapExit 즉사를 막지 못한다.
status: active
updated: 2026-11-17
source: Task-20261116-143
---

# 치트 공간 이동 중 바닥 없는 시작 씬

## 언제 읽나
개발 시작점으로 공간 이동한 직후 죽어 있거나, 커튼 로딩 중 플레이어가 추락할 때.

## 원인과 대처
- Start → 2일차 아침 최초 호스팅에서 시작 좌표 (-4.5,0,-.13)에 플레이어가 생성된다. 목적지 로딩 전 약 3.6초에 y=-15.562로 떨어져 MapExit가 적용되고, 대기실 스폰은 위치만 옮겨 사망 상태가 남았다.
- InputGate는 입력만 막는다. `DevStartLauncher.HoldingPlayerMovement`와 공간 전환 커튼 동안 PlayerMover의 중력·외부 속도 진행을 보류한다. 배치 및 커튼 해제 뒤 즉시 원래 시뮬레이션을 재개한다.
- 같은 준비 구간에 낙하 이력을 초기화하고 지나간 공간의 WorldDeath 보고를 받지 않는다. GameSettings의 사망 높이나 정상 플레이의 피해 규칙은 바꾸지 않는다.

## 검사
- `DevLaunchFromStartDoesNotFallBeforeDestinationPlacement`: 실제 PlayMode 최초 호스팅/대기실 진입 후 Normal 유지.
- `DevLaunchHoldsGravityAndWorldDeathOnlyUntilPlacementCompletes`: 준비 중 위치 유지·낙하 속도 제거·WorldDeath 거부, 완료 후 동일 사망 판정 즉시 허용.
