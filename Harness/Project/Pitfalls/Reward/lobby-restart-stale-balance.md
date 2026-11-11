---
summary: 로비 복귀의 Campaign None 메시지와 Session Waiting 복제 순서는 같지 않다. 보상 초기화를 두 상태의 동시 충족에 묶으면 이전 세션 잔액이 남는다.
status: active
updated: 2026-11-11
source: Task-20261109-114, actual three-process Relay restart, reward-resync-before snapshots
---

# 같은 방 재시작 후 기존 참가자의 자금 불일치

## 증상과 재현
- 같은 방에서 EndGame→ReturnToLobby→StartGame. 기존 참가자와 신규 참가자를 함께 비교한다.
- 실제 Relay3프로세스에서 호스트/신규 참가자는850, 기존 참가자는0. 기존 참가자는 이전 Reward SessionId·generation4·revision8을 유지했다.
- 의료 예약·치료는 Expert ShopChannel로 별도 복제되므로 정상이어도 공동 자금이 틀릴 수 있다.

## 원인
- 호스트 ReturnToLobby는 GameStateChanged(Waiting)를 먼저 호출한다.
- 그 구독자인 CampaignRun이 ClearRun→Apply(default)로 Campaign None을 먼저 보낸 뒤 세션 상태가 복제된다.
- 기존 보상 HandleStep(None)은 로컬 GameState==Waiting도 요구했다. None이 먼저 도착하면 초기화를 놓쳤다.
- 새 Reward SessionId는 IsNewerThan의 이전 세션 보호에 막힌다. 5초 재요청으로도 회복하지 못한다.
- 실제 기존 참가자에서 Clear→RequestSnapshot 후850으로 복구되어 초기화 누락을 확인했다.

## 처리 기준
- 권위 있는 Campaign None 단계 전환에서 이전 보상 상태를 정리한다. 서로 다른 채널의 Waiting 도착을 전제하지 않는다.
- 보상 스냅샷의 세션·revision 보호를 무조건 완화하지 않는다.
- None 뒤 새 보상→새 Campaign 순서도 유지해야 한다. 늦은 Waiting으로 새 보상을 다시 폐기하지 않는다.
- 빠른 로비 복귀·재시작은 중간 NetworkVariable 값이 합쳐질 수 있으므로 별도로 검사한다.
- 이 문제는 접속이 유지된 상태의 자금 불일치다. 원인 미확정 밤→아침 튕김과 같은 결함으로 표시하지 않는다.
- 수정 후: 메시지 순서3검사와 새 빌드 Relay2명의 일반/같은 프레임 재시작2회 통과. 양쪽 자금1000→2000→3000 일치.
