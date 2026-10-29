# 3단계 — 상점 (첫 공간)

- 사용자 지시: 상점은 원래의 기둥 그대로 간다. 1차로 만든 원통형 부스는 삭제했다.
- 원본 `WaitingRoomGreybox/S03_Shop`(Pillar 1.59×10×1.79, Phone, DeliverySlot, 비활성 Label·Sign)을 그대로 복제해 `WaitingRoom_LayoutA/S03_Shop`에 두었다. 크기·구성·컴포넌트 값은 바꾸지 않았다.
- 배치: 조감도 상점 자리(로비 남서, 기둥 중심 로컬 (-10.8, -6.0))로 옮기고 180° 돌렸다. 원본은 수화기가 옛 로비 서쪽(-X)을 봤고, 새 자리에서는 로비 중심(+X)을 본다.
- 추가(복제 원본 밖, `LayoutA_Additions`): Task-060 기준의 "수화기 위 단일 램프" 스폿 1개와 기존 철망 작업등 복제 1개(그림자 끔, 밝기 절반), 주변 트렁크·팔레트.
- 연결: `UI/WorldLabels`의 `WaitingRoomWorldLabels.delivery`를 새 배송구로 재연결. 상점 UI·일정판 UI의 장면 검색이 꺼진 원본을 고르던 문제를 활성 컴포넌트만 고르게 수정했다(아래).

## 공용 코드 수정 — 다른 기능과 겹침 알림

- `Assets/Scripts/UI/SceneIntegration/WaitingRoomShopUI.cs`, `WaitingRoomScheduleUI.cs`: `GetComponentsInChildren<ShopPhone|ScheduleBoardView>()` 결과에서 `isActiveAndEnabled`인 것만 고른다.
- 이유: 비활성 루트에서 `GetComponentsInChildren()`은 꺼진 원본까지 반환하고, 루트 순서상 원본이 먼저라 UI가 열리지 않는 원본 수화기·일정판에 묶였다. 함정 기록: `Harness/Project/Pitfalls/inactive-root-component-scan.md`.
- 2단계 일정판 복제 때도 같은 문제가 있었으나 그때는 상호작용을 검증하지 않아 드러나지 않았다.

## 검증

- `BuildShop`/`Validate`: 원본 직렬화 동일, 활성 ShopPhone·ShopDeliverySlot 각 1개이며 새 기둥 아래, 기둥 크기 원본과 동일, WorldLabels.delivery가 새 배송구, 수화기·배송구 앞 0.75m 지점에 바닥과 캡슐 여유가 있고 도달 거리(1.8m·1.2m) 안.
- Play Mode 이동 검사에서 수화기 앞 (-9.16, -5.51), 배송구 앞 (-9.18, -5.93)까지 실제 CharacterController로 도달.
- 미검증: 실제 구매 UI 열기, 배송 낙하 위치, 네트워크 구매 동기화. 다음 공간은 소품실(04), 숙소·화장실(02) 순.
