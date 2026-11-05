---
summary: 추적자 경로 캐시·정찰 재개·우회·도달 불가 목표를 수정하거나 검증할 때
status: active
updated: 2026-11-05
source: Task-20261105-110 코드 조사·실제 WaitingRoom 경로 조사
---
# 추적자 정찰 경로

- `PursuitNavigation.TryPlan`은 동기 NavMesh 경로를 구한다. 벽 안 목표는 가까운 NavMesh로 옮기며, 다른 섬의 목표는 부분 경로 끝을 도달 가능한 목표로 사용한다.
- `PursuitActor.Navigation`은 고정 목표 경로를 캐시한다. **Stop이 실제 경로를 지워도 목표 캐시는 남는다.** 따라서 같은 목표라도 아직 도착하지 않았고 `agent.hasPath`가 없거나 경로가 낡으면 다시 계산해야 한다. 그렇지 않으면 걷기 연출만 나오며 정체 감지도 작동하지 않는다.
- 경로 적용의 `SetPath` 반환값도 실패 판정에 포함한다. 정찰 목표 보정이 5m를 넘으면 다음 순회지로 넘어가고, 막힌 이동은 1초 후 재경로·3초 후 포기한다.
- 현재 WaitingRoom 씬 조사: NavMeshSurface 1개, 링크 28개, 배치 추적자 26개. 배치 순회 112구간 모두 완전 경로이며 링크 중앙 높이 직선 검사에서 막힌 링크 0개였다. 문 장애물은 `PursuitDoorObstacle.Awake`에서 생성되므로 편집 모드 obstacle 0개는 누락의 증거가 아니다.
- 회귀: `PursuitNavigationTests`의 벽 우회·벽 내부 목표·다른 섬 목표, PlayMode `PursuitNavigation_PatrolRestoresPathAfterListeningStop`의 경로 재개.
