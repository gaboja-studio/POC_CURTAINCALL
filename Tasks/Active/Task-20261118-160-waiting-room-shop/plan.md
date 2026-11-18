# Task-20261118-160

- **Title:** 대기실 수화기 상점 개선(신체 복구 추가 예약·공연 기물 해금 알림)
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-room/shop
- **Base:** dev

## Context
대기실 ③ 수화기 상점의 불편함 개선(2026-11-18 사용자 요청).
1. 신체 복구 예약 뒤에도 더 고칠 팔다리가 있으면 팔다리 개수 버튼으로 추가 예약할 수 있게 한다.
2. 작업실·소품실 해금 전 공연 기물은 상점 목록에서 빼고, 해금되면 HUD 역할 알림을 띄운 뒤 목록에 추가한다.

## 계획
- `ExpertServiceBook`: 입장 전(Booked) 예약자의 추가 예약 허용. 한 번 최대치(BodyRestoreMaxParts)에서 이미 예약한 수를 뺀 만큼만, 예약 때 단가로, 팀 이용 횟수는 그대로.
- `ExpertService`: 추가 예약 비용·문구·장부 문구(`ledger.medical-add`, `expert.bought-more`).
- `WaitingRoomShopUI`: 추가 예약 모드(버튼 "n개 추가 예약", 둘째 줄 "예약 n개 · 추가 가능 m개"). 잠긴 공연 기물은 목록에서 제외.
- `WaitingRoomShop`: 잠김 → 열림 전환을 보고 모든 화면에 알림. HUD 프로필 새 경로 `shopItemsAdded`(기본 시스템 보상).

## 미정·주의
- 추가 예약 총량은 기존 "한 번에 최대 3개" 규칙(GameSettings)을 그대로 따른다.
- 서버 판정의 해금 전 구매 거절(CRAFT_ROOMS_LOCKED)은 안전장치로 유지.
