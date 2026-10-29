---
summary: 비활성 루트에서 GetComponentsInChildren()을 부르면 꺼진 원본 컴포넌트도 반환된다
status: active
updated: 2026-10-27
source: Task-20261027-075
---

# 비활성 루트의 컴포넌트 검색

## 언제 읽나

씬 루트를 순회하며 `GetComponentsInChildren<T>()`로 수화기·일정판 같은 기능을 찾는 코드를 쓰거나, 원본을 비활성 루트로 보존하고 복제본을 새 루트에 둘 때.

## 증상

Task-075에서 WaitingRoomGreybox 루트를 비활성으로 보존하고 WaitingRoom_LayoutA에 상점 기둥을 복제했다. `scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ShopPhone>()).FirstOrDefault()`가 새 수화기가 아닌 꺼진 원본(WaitingRoomGreybox/S03_Shop/Phone)을 반환했다. 루트 순서상 원본이 먼저라 상점 UI가 열리지 않는 원본에 묶인다.

## 원인

`includeInactive=false`여도 호출 대상 GameObject 자신이 비활성이면 그 아래 컴포넌트가 결과에 포함된다. 비활성 상태를 걸러 주는 것은 활성 루트 아래의 비활성 하위뿐이다.

## 대처

- 런타임 검색은 `.FirstOrDefault(c => c.isActiveAndEnabled)` 또는 `activeInHierarchy`로 거른다. Task-075에서 WaitingRoomShopUI·WaitingRoomScheduleUI의 수화기·일정판 검색을 이렇게 고쳤다.
- 검증 코드에서 개수를 셀 때도 `activeInHierarchy`로 거른다.
- `FindAnyObjectByType`·`FindObjectsByType`(기본값)은 비활성 오브젝트를 제외하므로 해당하지 않는다.

## 확인 방법

비활성 원본과 활성 복제본이 함께 있는 WaitingRoomBase에서, 비활성 루트에 대해 `GetComponentsInChildren<ShopPhone>().Length`가 1인지 보고, 런타임 검색 결과가 `WaitingRoom_LayoutA` 아래인지 확인한다.
