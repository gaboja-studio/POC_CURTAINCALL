# Task-20261114-129

- **Title:** 정비 시간 무대 입구 통로 봉쇄·소등
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-room/maintenance-stage-corridor
- **Base:** dev
- **PR:** #381

## Context
2026-11-14 사용자: 정비 시간에 공연장으로 가는 복도(무대 입구 통로) 조명을 끄고, 로비 → 복도 입구를 다른 곳과 같은 철창으로 닫는다. 쉬는 시간에는 열려 있어야 한다.

## 계획
- 철창: `StageCorridorClosure`가 최종본 배치 씬(WaitingRoom.unity, `FinalLayout_Swap` 표식)에서 실행 중 로비 북쪽 입구(x -14.43~-4.97, z 91.75, 높이 9m)에 밤 정비 철창과 같은 구성(머리 판·살·GateCollision·PursuitDoorObstacle)으로 세운다. 씬 파일은 바꾸지 않는다.
- 잠김: `NightDoorLock`이 이 철창만 `Night`·`Dawn`에 닫는다(탐색 잠금 설정·리허설 동선 제한과 무관).
- 조명: `NightPower`가 S06_StageCorridor·S07_StageEntrance 조명을 같은 조건에서 끈다.
- 설정: 대기실 설정 `정비 시간 무대 통로 봉쇄·소등`(기본 켬).
- 정비 시간 추격 임무에서 철창이 닫혀 있으면 "공연장 입구 이동" 줄을 뺀다.

## 미정·주의
- 정비 시간 전원 무대 입구 집결 → 다음 날 공연으로 바로 가는 기존 경로(RunState.NotifyAllAtStageEntrance Night 분기)는 철창 때문에 실제 플레이에서 막힌다. 코드는 그대로 둔다(개발 버튼용).
- 최종본 배치가 아닌 WaitingRoomBase·WaitingRoomPursuit에는 철창·소등을 적용하지 않는다.
- 철창 모양(9.5m×9m 큰 살 벽)은 사람이 플레이로 확인한다.
