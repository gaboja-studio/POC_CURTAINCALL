# 1단계 — WaitingRoomGreybox 원본 보존·복제 준비

- 기준 카드: [CC-159](https://hrjoo122770.atlassian.net/browse/CC-159)
- 작업 씬: Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity
- 원본 WaitingRoomGreybox의 이름·계층·ID·변환·컴포넌트·직렬화 참조는 유지하고 루트만 비활성화했다.
- 새 루트: WaitingRoom_LayoutA/LayoutOrigin. 아직 방을 복제하거나 새로 만들지 않았다.
- 원본 오브젝트 6035개 / Renderer 4332개 / Collider 579개 보존.
- 변경 전후 원본 ID·이름·하위 활성 설정·월드 변환·모든 컴포넌트 직렬화 문자열 동일. 루트 활성값 변경만 비교에서 제외.
- 복원 확인: 원본 활성화 시 활성 Collider 354개, 다시 비활성화한 뒤 원본 활성 계층 Collider 0개.

## 복제 원본

| 공간 | WaitingRoomGreybox 아래 경로 | 오브젝트 | Collider | 원본 ID |
|---|---|---:|---:|---|
| 로비 | S01_Lobby | 32 | 18 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-1330454971-0 |
| 숙소 | S02_Dormitory | 32 | 14 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-1594412233-0 |
| 화장실 | S02_Dormitory/Restroom | 9 | 7 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-431661-0 |
| 상점 | S03_Shop | 7 | 2 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-1452541174-0 |
| 소품실 | S04_PropRoom | 16 | 8 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-816248522-0 |
| 무대 통로 입구/통로 | S06_StageCorridor_Temp | 63 | 27 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-965734069-0 |
| 공연 스테이지 입구 | S07_StageEntrance_Temp | 12 | 8 | GlobalObjectId_V1-2-0ce9037bd7c44c8b9d57785c26620388-1856629830-0 |

## 공용 루트 활성 상태 — 모두 변경 없음

- Main Camera: True
- Directional Light: True
- PlaySetup: False
- PlayerFollowCamera: True
- ShopStorage: True
- ShopStorageViews: True
- ScheduleState: True
- ExpertService: True
- ExpertServiceMouseLook: True
- WaitingRoomTestPanels: False
- UI: True

## 주의·다음 작업

- 방 아트는 Mood/Ceilings·Lights·Dressing·Rooms·ProtoFill·Furnishings·VisibilityLighting·InteriorArt와 공용 Walls에도 분산돼 있다. 다음 단계에서 각 방의 관련 묶음까지 함께 복제한다.
- S03_Shop에 수화기·배송구, WaitingRoomGameplay에 스폰·4단 선반·집결·전문가실, NightSchedule에 셔터, ExplorationTimer에 탐색 경계가 있다. 이 공간 종속 오브젝트들은 비활성 원본에 보존하며 단계별 복제/재연결한다.
- 루트 밖 ShopStorage·ShopStorageViews·ScheduleState·ExpertService·카메라·UI 등의 활성 상태와 참조를 변경하지 않았다. 원본 공간을 가리키는 참조는 새 공간 복제 시 갱신한다.
- 1단계는 Editor 배치 준비 상태다. 새 로비 바닥·스폰 연결 전에는 Play Mode로 실행하지 않는다. 이동·공동 플레이·공연 왕복은 미실행이다.
- 복원: WaitingRoom_LayoutA 비활성 → WaitingRoomGreybox 활성. PlaySetup은 원래 false이므로 그대로 둔다.
- 잘못 선택했던 Levels/waiting_room.unity의 제 변경은 Editor에서 복원했고 Git 원본 대비 차이 0을 확인했다. 이 프로토타입은 복제 원본으로 사용하지 않는다.
- 다음은 2단계 S01_Lobby 및 해당 Mood/Walls 복제·배치와 바닥/스폰 확인이다.
