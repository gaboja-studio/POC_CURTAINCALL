---
summary: 앉기 몸통 검사는 걸어 넘는 높이 위에서 하고, 같은 높이 난간 넘기와 운반 소품 깎기는 별도로 처리한다.
status: active
updated: 2026-11-17
source: Task-20261117-152
---

# 계단 앞 정지·통로 난간 섬·운반 소품

- 앉기 몸통 검사가 발목부터 시작하면 계단 경사 충돌체(Ramp/FlightRamp)·바닥 팔레트·통로 옆 난간을 막힘으로 보고 `agent.speed=0`이 된다. `TickStuck`은 속도 0을 이동 의도로 보지 않아 영구 정지한다(상태만 경계/추격으로 바뀜). 검사는 걸어 넘는 높이(0.45m) 위, 반경은 굽기 반경(0.24)보다 작게, 앞 발은 NavMesh 높이로 잡는다.
- 런타임 `PursuitNavMeshRepair`는 길 표본이 밟고 있는 가파른 경사로도 "물체 속"으로 판정해 계단 전체를 깎을 수 있다. 표본 바로 위 0.4m 안의 윗면은 제외한다.
- 화물 통로 꺾임의 1.1m 난간은 같은 높이 NavMesh를 섬으로 끊는다. 오르기 링크는 높이차만 보므로 같은 높이 넘기 링크(`PursuitClimbLinks` vault)가 따로 필요하다. 동·서·북 통로의 5m 구멍은 의도된 끊김이다.
- 플레이어가 옮기는 트램펄린은 굽기에 없다. 내려놓은 동안만 깎고, 들고 있을 때 깎으면 들고 있는 플레이어를 잡지 못한다. 운반 중에는 충돌체가 트리거라 크기는 내려놓은 뒤 잰다.
- 부서지는 벽·상자·문의 막는 충돌체는 굽기에서 빼고 `PursuitDoorObstacle`로 실행 중에만 깎는다. 새 대상은 다시 굽기(장애물 등록) 후 `UnifiedWaitingRoomSceneTests`·`PursuitBreakables_*`로 확인한다. 낮은 상자(소음 없음)는 넘기 대상, 문·덮개는 아니다.
- 통합 대기실 PlayMode 검사는 `waitingRoomVariant=Integrated`를 지정해야 한다. 기본 변형은 NavMesh가 없어 추격자가 깨어남 상태로 멈춘다.
- 검증: `PursuitCoverage_StairsVentAndRailCornersReachTarget`(환풍구 계단·계단실 경사로·통로 두 모서리).
