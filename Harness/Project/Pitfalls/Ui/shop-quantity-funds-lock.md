---
summary: 서비스 자금 부족은 예약만 막고 수량 변경을 막지 않는다
status: active
updated: 2026-11-05
source: Task-20261105-109
---

# 서비스 수량과 자금 부족

## 언제 읽나
상점 서비스 수량을 늘려 공동 자금을 초과했을 때 수량을 다시 바꿀 수 없거나 예약 버튼 조건을 수정할 때.

## 원인과 대처
- ShopPanelView는 Entry.Available이 false면 모든 버튼의 표시와 RequestAction을 함께 막는다. 수량 버튼의 Interactable만 true로 만들어서는 해결되지 않는다.
- WaitingRoomShopUI는 예약 가능 여부와 수량 조절 가능 여부를 각각 계산하고, 둘 중 하나가 가능하면 항목을 열어 둔다. 예약 버튼은 자금 검사 결과로 따로 막는다.
- 예약 중·이용 불가 시간·사망 등 서비스 자체의 거절 조건은 수량 변경에도 적용한다. 비용·자금·예약 최종 판정은 기존 ExpertService가 유지한다.

## 확인 방법
W2_BodyRestore_QuantityRemainsUsableWhenFundsAreShort: 1개 가격만 가진 상태에서 2→3→1개를 실제 버튼으로 선택하고, 부족 시 예약 거절·수량 유지, 복귀 후 예약 성공 및 예약 후 양쪽 버튼 잠금을 검사한다.
