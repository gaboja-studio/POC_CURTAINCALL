# Task-20261118-161

- **Title:** 대기실 수화기 상점 개선(신체 복구 추가 예약·공연 기물 해금 알림)
- **Type:** fix
- **Assignee:** @MoHoDu
- **Branch:** fix/waiting-room/shop
- **Base:** dev

## Context
대기실 ③ 수화기 상점·공연 기물의 불편함 개선(2026-11-18 사용자 요청).
1. 신체 복구 예약 뒤에도 더 고칠 팔다리가 있으면 팔다리 개수 버튼으로 추가 예약.
2. 상점 해금: 누군가 기물을 처음 주워 습득하면 모든 플레이어의 상점에 HUD 알림과 함께 추가. 기물 목록에서 "처음부터 판매"를 기물별로 체크(탐색 기물은 모두 체크). (작업실·소품실 개방 조건은 이 규칙으로 대체)
3. 공연 기물 설명을 기획 "공연 기물 고도화"(Confluence 44793873) 기준으로 갱신(상점·호버 설명).
4. 대기실에서 공연 기물 칸의 슬롯 키(1·2) → 칸 위에 설명 켜기/끄기. 다른 칸 키로 교체, ESC·이동 시 끔.
5. 연습실 구역에서는 공연 기물을 내구도 소모 없이 연습. 다른 대기실 구역에서는 사용 불가. 공연·탈출로는 정상 소모.

## 계획
- `ExpertServiceBook`/`ExpertService`/`WaitingRoomShopUI`: 입장 전 추가 예약(예약 때 단가, 팀 이용 횟수 유지).
- `ItemCatalog.Entry.shopUnlockedFromStart` + `WaitingRoomShop.ShopLocked/RecordAcquired`: 해금 기록은 탐색 장부 `shop-unlock:<id>`(팀 공유·늦은 입장 동기화, 세대 교체에도 유지). `PropCarry` 줍기·선반 꺼내기에서 기록.
- 알림: HUD 프로필 경로 `shopItemsAdded`(기본 시스템 보상).
- 슬롯 설명: `InventorySlotTooltip`(HUD 역할 Retained/Tooltips 프리팹), `CommonInventoryUI` 키 처리, ESC는 `CommonMenuUI`가 메뉴 대신 설명을 닫음.
- 연습실: `ExplorationTimerZones.IsPracticeRoom`(지역명 "연습실" 구역), `PropCarry.PlaceOf/UseEquipmentUid`(연습=소모 없음, 그 밖 대기실=거절), `NetworkPlayer` 사용 가능 판정·서버 요청에서 차단.

## 미정·주의
- 추가 예약 총량은 기존 "한 번에 최대 3개" 규칙(GameSettings)을 그대로 따른다.
- 상점 해금은 런 동안 유지(세대 교체 유지, 새 게임 초기화). 처음부터 판매가 아닌 공연 기물은 맵에서 줍기 전까지 살 수 없다.
- 슬롯 설명은 선택 키 1·2로 연다(장비 칸에 표시되는 Q·E는 기존대로 기물 사용).
