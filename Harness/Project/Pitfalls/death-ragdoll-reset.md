---
summary: 공연 종료 조작 정리의 BalanceReset이 사망 레그돌까지 복구함. 사망한 몸이 서 있거나 공중에 남는 문제를 조사할 때
status: active
updated: 2026-10-22
source: Task-20261021-069
---

# 사망 레그돌이 조작 정리 때 복구됨

## 언제 읽나

외줄·대포에서 Fallen 확정 뒤 모델이 다시 서 있거나 레그돌이 사라질 때.

## 원인

SetBalanceActive는 ResetBalance를 부르고 BalanceReset은 PlayerRagdoll.Restore를 호출한다.
외줄 공연 종료의 ReleaseControls가 이를 실행해 사망 모델을 원래 슬롯에 다시 만들었다.
ForceFall은 이미 HasFallen이면 Fell을 다시 발행하지 않아 확정 사망 때 시각 보장이 누락될 수도 있다.

## 해결

- 콘텐츠 정리는 사망 상태를 유지하면서 SetDisplayedState로 균형 표시만 끈다.
- PlayerRagdoll.Restore는 네트워크 Fallen 동안 복구하지 않는다. 정상 부활·재시작은 Normal 확정 뒤 복구한다.
- 확정 Fallen은 ForceFall 여부와 별개로 멱등 GoLimp를 보장한다.
- 레그돌은 isKinematic=false·useGravity=true·constraints=None을 명시한다.

검사: TelemetryIntegrationTests.DeathRagdoll 2종. 활성 실제 씬·LAN 호스트 공개 사망/절단 API와 물리 낙하 검증이며 사람 입력·두PC 검증은 별도다.
