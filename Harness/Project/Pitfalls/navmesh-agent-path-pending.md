---
summary: 다수 NavMeshAgent가 SetDestination을 짧은 주기로 반복하면 pathPending에 갇혀 제자리 걸음을 한다. 동기 CalculatePath+SetPath로 길찾기한다.
status: active
updated: 2026-11-04
source: Task-20261104-102
---

# NavMeshAgent pathPending 제자리 걸음

## 언제 읽나

추격자(또는 여러 NavMeshAgent)가 벽 앞이나 빈 곳에서 걷기·달리기 동작만 하고 움직이지 않을 때. 순찰만 할 때는 멀쩡하다가 여러 마리가 한꺼번에 먼 목표로 바뀌면(추격 전환) 생긴다.

## 원인

- `SetDestination` 경로 계산은 비동기다. 모든 에이전트가 프레임당 `NavMesh.pathfindingIterationsPerFrame`(기본 100) 예산을 나눠 쓴다.
- 0.2초마다 `SetDestination`을 다시 부르면 끝나지 않은 계산이 매번 처음부터 다시 시작된다. 26체가 동시에 먼 경로를 요청하면 `pathPending=true, hasPath=false` 상태가 계속되고, 같은 위치에서 `NavMesh.CalculatePath`를 부르면 바로 성공한다.
- `NavMeshObstacle`(carving)의 center/size를 매 프레임 같은 값으로 다시 넣어도 NavMesh가 다시 깎인다. 이때 경로가 무효화되고 `autoRepath`가 같은 비동기 대기열에 들어간다.

## 대처

- `PursuitNavigation.TryPlan`: 목표 근방을 반경 0.6→8m로 넓혀 가며 샘플링한 뒤 동기 `CalculatePath`로 계산하고 `agent.SetPath`로 넘긴다. 부분 경로면 경로 끝(가장 가까운 도달 지점)을 목표로 삼는다.
- `agent.autoRepath = false`로 두고 직접 재계산한다. 고정 목표는 목표·NavMesh(`PursuitNavigation.Version`)가 바뀌거나 끼였을 때만 계산한다. 추격은 대상이 거리의 15%(0.3~3m) 이상 움직였을 때 0.4초±25% 주기로 계산한다.
- 동기 계산은 몰리면 스파이크가 된다(26체 동시 전환 시 한 프레임 88회·12ms). 프레임당 8회 상한(`PursuitNavigation.HasBudget`)과 주기 흩뜨리기로 최악 1.3ms로 묶었다.
- 장애물 값은 바뀐 때만 대입한다(`PursuitDoorObstacle`).
- `NavMeshPath`는 네이티브 객체라 MonoBehaviour 필드 초기화에서 `new NavMeshPath()`를 쓰면 안 된다. 생성자가 실패하고 뒤에 선언된 필드까지 전부 null이 된다(`poseBuffer` NRE). 처음 쓸 때 만든다.

## 재현

WaitingRoom.unity 플레이 → `PursuitDirector.DebugStart()` → `DebugSetStage(3)`, `DebugSetInvulnerable(true)` → `PlayerMover`를 붙인 빈 오브젝트를 대상으로 둔다. 각 에이전트의 `pathPending`과 프레임당 이동량을 기록한다.
