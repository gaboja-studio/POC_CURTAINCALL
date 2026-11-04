# Task-20261104-101

- **Title:** 추격자 길찾기 정지·벽 막힘 수정
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/pursuit/systems
- **Base:** dev

## Context
추격자가 순회·추격 중 벽 앞에서 멈추거나 막힌 채 걷는 버그 수정(2026-11-04 플레이 스크린샷).
- 순회: 다음 순회지까지 NavMesh 최단 경로로 이동. 순회지가 이동 불가 위치면 근방의 도달 가능 지점으로 조정해 이동.
- 추격: 플레이어까지 NavMesh 최단 경로로 추격.

## 계획
1. 원인 재현(WaitingRoom.unity 플레이, 4단계 강제·가짜 대상): 추격 전환 시 26체 중 대부분이 `pathPending` 상태로 경로 없이 제자리 달리기.
   - `Navigate`가 0.2초마다 `SetDestination`을 재요청 → 비동기 경로 예산(100 iterations/frame)을 26체가 나눠 쓰는 동안 매번 초기화되어 먼 경로가 끝나지 않음. 같은 위치의 동기 `CalculatePath`는 성공.
   - 순찰 단독(대상 없음)은 0~3단계 모두 정지 재현 안 됨.
2. 길찾기를 동기 `NavMesh.CalculatePath` + `agent.SetPath`로 교체. 목표 근방 샘플 반경 확대, 부분 경로는 가장 가까운 도달 지점으로 조정.
3. 순회지는 활성화 시 도달 가능 지점으로 보정해 캐시, 도달 판정도 보정 지점 기준.
4. 정지 감지(이동 의도 대비 실제 이동 부족) → NavMesh 재배치·재경로, 반복 실패 시 해당 목표 포기.
5. 최인접 추격 단계는 직선 거리 대신 경로 거리로 대상 선택(캐시).
6. NavMesh와 현재 충돌체 불일치 확인(삼각형 166/3518이 랙·좌석·기계 충돌체와 겹침) 후 필요 시 재베이크.
7. 2026-11-04 사용자 결정(부하 개선): 고정 목표(순회·소리·복귀)는 목표당 1회 계산 + 문 상태 변경(`PursuitNavigation.Version`)·끼임 시에만 재계산. 추격은 대상 이동(거리의 15%, 0.3~1m) 시에만 0.4초±25% 주기로 재계산. 최인접 대상 경로 거리 캐시 0.75초±25%. 전체 프레임당 계산 상한 8회(초과분은 다음 프레임, 기존 경로·지난 값 유지).

## 미정·주의
- NavMesh 에셋은 `Assets/_ThirdParty/curtaincall/Pursuit/Navigation/WaitingRoomPursuit.asset`(비공개 서브모듈) 공유. 재베이크 시 WaitingRoom/WaitingRoomPursuit 두 씬에 함께 반영.
- 다인 원격 화면은 호스트 판정 결과를 받으므로 별도 실기 확인 대상.
