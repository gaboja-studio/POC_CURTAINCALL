# 실내 에셋 교체 기록

대상: WaitingRoomBase의 `WaitingRoomGreybox` 내부만. 아래 경로는 해당 루트 기준.
원본 `대기실`·`WaitingRoom`과 무대 입구 통로는 보존. 기존 대상은 비활성 보관하며 새 묶음은 `Mood/InteriorArt/WR*`로 식별한다.
별도 표시: `Mood/InteriorArt/AssetReview_Replaced_WR01_to_WR07`의 비활성 검토 표식 7개(게임 화면에는 표시하지 않음).

| ID | 기존 경로 | 사유 | 교체·보완 |
|---|---|---|---|
| WR01 | S01_Lobby/CircusTent | 정갈한 대형 천막의 비례가 초라한 테마와 불일치 | 폭 약 60%·높이 48%, 찌그러진 지붕과 기울어진 작은 천막. 새 메시만 변형 |
| WR02 | Mood/ProtoFill/PropRoom/CostumeRack_0·1, Mood/Furnishings/PropRoom/CostumeRack_West, Mood/ProtoFill/Lobby/의상걸이 | 늘어나고 날카로운 조각처럼 보이는 의상 외형 | 정상 폭 금속 랙·처진 천 3벌씩. 기능 없는 장식으로 교체 |
| WR03 | Mood/Furnishings/JunkStorage/DiscardedPropsPile | 풀·야외 쓰레기가 실내 서커스 보관소와 불일치 | 재사용 나무 상자·공연 북·헝클어진 천 묶음 |
| WR04 | Mood/Furnishings/Dormitory/SharedRestTable·RestChair_North·RestChair_East | 현대적 캠핑 가구 인상 | 프로토타입의 낡은 목제 탁자·의자 |
| WR05 | Mood/Rooms/Dormitory/Bench | 지나치게 길고 판자처럼 보이는 벤치 | 2.2m 좌판과 기울어진 수선 다리 |
| WR06 | Mood/Furnishings/PropRoom/MascotBody·CostumeDisplay, Mood/Furnishings/JunkStorage/PreviousPerformer_0~2 | 노출 마네킹으로 의상 보관 의미가 약함 | 원본 마네킹·탈과 주름/찢어진 밑단의 입체 의상 덮개 결합 |
| WR07 | Mood/ProtoFill/PropRoom/Vanity | 길이 약 9.4m의 반복 화장대가 압축한 방에 과도함 | 실제 제작된 서랍장 2개·개별 거울·의자·전구색 조명 |

WR01의 찌그러짐은 승인된 연출이다. 원본 공유 메시·외부 에셋 파일은 수정하지 않는다. WR06은 기존 에셋을 조합한 장식 의상이며 완성된 신규 캐릭터 모델 제작은 포함하지 않는다.
물려줄 선반은 기존 기능 앵커를 옮기고 판자 4개 높이(0.3/0.9/1.5/2.1m)를 유지한다.

## 구조도 공간별 1단계 — 숙소

- WR08: `Mood/Furnishings/Dormitory/Bed_1~4_Bunk`는 원형을 비활성 보관하고 제작된 하단 침상 부품을 재사용해 `Mood/InteriorArt/Stage01_Dormitory/Bed_A~D`로 구성. 구조도의 낮은 침상과 침대 사이 사물함/옷장 배치에 맞춤.
- 숙소만 깊이6.7m의 내부 벽·석재 바닥·러그·개인 짐·머리맡 전구를 구성. 기존 화장실·통로·중앙 천막·원본2개 보존. 비대상13,482개 서명 동일.
- 작업노트 23855106에 숙소 단계·WR08·정면/평면 화면2장 추가. 다음 공간은 숙소 검토 뒤 진행.

## 최신 수정 — WaitingRoom 침대·보관소·소품실

- WR09: 생성한 낮은 침상 Bed_A~D는 비활성 보관하고 `Stage01_WaitingRoomBeds`의 `SM_Prop_Bed_Bunk_01`4개로 교체. WaitingRoom/R02_Dormitory/Props 원본의 메시·재질과 일치, 비율1 유지. 기존 WR08 이후 사용자 정정 반영.
- 보관소: 서쪽 내부 벽으로 폭8.2m, 북쪽 기록 선반3/책상, 이전 세대 의상/기물, 남쪽 정리 선반2/기록 책상 구획. 기존 떠 있는 장식 트렁크는 비활성 보관, 공연 기록 노트 원본은 새 책상 위로 이동. 힌트 낱장 원본 유지.
- 소품실: 서쪽 의상2줄/수선대, 중앙 소품 선반/물려줄 선반, 동쪽 분장3자리/거울/전구/의자. 기능 선반 앵커와 외형은 동일한 수평 이동, Board_0~3 높이는0.3/0.9/1.5/2.1 유지.
- 각 적용은 비대상 전체 오브젝트 서명 검증. 상점·천막·무대 통로·원본2개와 먼저 완료한 다른 공간은 보존. 저장된 생성 그룹 `Stage02_JunkStorage`, `Stage02_ArchiveDetails`, `Stage03_PropRoom`.
