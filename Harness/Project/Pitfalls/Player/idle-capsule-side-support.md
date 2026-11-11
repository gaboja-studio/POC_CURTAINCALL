---
summary: 둥근 CharacterController가 가구 모서리 접촉을 접지로 보고해 정지 플레이어가 뜰 수 있다. 지지면 조회와 실제 발 접촉 높이를 구분한다.
status: active
updated: 2026-11-11
source: Task-20261109-114, actual LAN host play, CommonIdleSupportRegression
---

# 상자 옆 정지 상태의 부양

## 언제 읽나
- 발밑이 비어 있는데 CharacterController.isGrounded=true인 경우.
- 씬 상자 충돌체가 외형과 일치해도 가구 옆에서 떠 있는 경우.

## 재현과 원인
- WaitingRoomBase 현재 숙소 FootTrunk_2 중심(21.45,.19,76.92), 크기(.69,.39,.82).
- 실제 스폰된 LAN 호스트를 x20.85,z76.92의 높이1.4에서 낙하시키면 발 원점Y.242105에서 정지한다.
- CC접지=true, Simulated=true, IsAirborne=false, 공통 WalkableSupport=false. 상자 모서리 구형 접촉 법선Y=.24는 걸을 수 없는 경사다.
- 일반 철 상자 옆에서도Y.55에서 재현했다. 남은 옛 충돌체나 중력 비활성으로 단정하지 않는다.

## 처리와 주의
- PlayerMover 일반 자유 이동의 정지·접지 상태에서 실제 지지가 없으면 기존 중력의 접촉면 투영으로 바깥/아래로 이탈한다.
- 이동 입력·점프·전용 기물 운동·실제 발판 지지가 있으면 보정하지 않는다. 새 씬 조건이나 조정값은 없다.
- WalkableSupport 조회에는 여유 거리가 있다. 조회 성공만으로 멈추면Y.1799에서 다시 멈췄다.
- 발과 지지면 높이 차이가 기존 skinWidth+Physics.defaultContactOffset 이내인지 확인해야 한다.
- 실제 상자 윗면 정지와 모서리 이탈을 자동 PlayerMover.Update로 검사한다. 수동 Move로 결과를 만들어 내지 않는다.

## 근거
- Task 실제 기록: `live-play-audit.md`, 로컬 증거 `common-rules-live-20261111/dorm-runtime.txt`.
- PlayMode `DormFootTrunk_IdleActualMoverSlidesOffUnsupportedCornerAndKeepsRealTopSupport`: 수정 후1/1 PASS.
