---
summary: 실행 중 굽는 임시 NavMesh를 기본 에이전트(0번)로 만들면 추격자 NavMesh와 겹쳐 추격 경로가 끊긴다. 전용 에이전트(NavMesh.CreateSettings)로 굽고 같은 필터로 조회한다.
status: active
updated: 2026-11-12
source: Task-20261112-117
---

# 실행 중 임시 NavMesh가 추격자 경로에 섞임

## 언제 읽나

대기실에서 길안내·카메라 경로 등 실행 중 `NavMeshBuilder`로 NavMesh를 굽는 기능을 추가하거나 고칠 때. 리허설 뒤 추격자가 엉뚱한 곳에서 멈추거나 막힌 길 끝에서 노려보기만 할 때.

## 원인

- `NavMesh.GetSettingsByIndex(0)`로 구운 데이터는 추격자(`NavMeshAgent` 기본 에이전트 0번)와 같은 종류다. `NavMesh.AddNavMeshData` 뒤에는 추격자의 `SamplePosition`·`CalculatePath`가 두 NavMesh를 함께 본다.
- 두 NavMesh는 서로 이어지지 않는다. 임시 NavMesh는 구울 때의 소품(예: 트램펄린 0.5m > 넘는 높이 0.4m)을 구멍으로 굽는다. 컴포넌트가 사라질 때까지 남는다.
- 결과: 경로가 부분 경로가 되어 추격자가 끝에서 멈춘다(달리기 아님 → 원격 접촉 거부).

## 대처

- `NavMesh.CreateSettings()`로 전용 에이전트를 만들고 `NavMeshQueryFilter{agentTypeID}`로만 조회한다. 정리 때 `NavMesh.RemoveSettings`.
- 적용: `WaitingRoomTutorialCamera`(2026-11-04), `RehearsalPathGuide`(2026-11-12).
- 추격자 오르기 통로(`PursuitClimbLinks`)는 반대로 추격자 에이전트에 붙이는 링크다. 내장 Jump 구역이며 단계 조건에서만 `agent.areaMask`에 넣는다.
