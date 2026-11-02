# UI 고치는 법 (상점·소지 칸)

UI는 **에디터에서 보이는 상태로 저장**되어 있다. 씬 뷰나 프리팹 화면에서 보면서 바로 고치면 되고, 게임은 시작할 때 알아서 숨겼다가 필요할 때 띄운다. 규칙: [ui-authoring](/Harness/Project/Decisions/ui-authoring.md).

## 한눈에 보기

| 바꾸고 싶은 것 | 어디서 |
|---|---|
| 상점 창의 위치·크기·색·제목('상점') | 프리팹 `Assets/Resources/Prefabs/UIs/ShopUi/ShopPanel.prefab` |
| 가격 줄 형식·버튼 글·장부 문구·마우스 올림 설명 | 설정 에셋 `Assets/Resources/Settings/WaitingRoom/ShopUiTexts.asset` |
| 기물 가격·상점 설명·장비 발동 문구 | 기물 목록 `Assets/Resources/Settings/GameSettings/Items/ItemCatalog.asset` |
| 기물 이름 | 문구 파일 `Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt`의 `item.<id>.name` |
| 기물 아이콘 | `Assets/Resources/ItemArt/Icons/<id>-shop.png`(상점), `<id>-slot.png`(소지 칸) 파일 교체 |
| 소지 칸 모양·선택 테두리·손 아이콘·손 상태 문구 | 씬 `Assets/Scenes/UI/CommonMenuUI.unity`의 `C5_InventoryCanvas` |

- 고친 뒤 **Ctrl+S**로 저장한다. Play 중에 고친 값은 Play를 멈추면 사라진다.
- `Assets/_ThirdParty` 안의 원본 키트는 고치지 않는다.

## 1. 상점 창 보기

Project 창에서 `ShopPanel.prefab`을 더블클릭하면 프리팹 화면이 열린다. 예시 내용(장갑·신발, 175 토큰 등)이 들어 있어 실제 화면과 같은 모양으로 보인다. 게임에서는 열 때 실제 내용으로 바뀐다.

![상점 프리팹](Images/shop-prefab-overview.png)

1. `00_Visuals`: 상점 창 전체. 이 아래 오브젝트를 골라 고친다.
2. 씬 뷰: Rect Tool(**T**)로 끌어서 옮기거나 크기를 바꾼다.
3. Canvas Group **Alpha 1**: 보이는 상태로 저장되어 있다. 0으로 바꾸지 않는다.

## 2. 바뀌지 않는 문구 (제목 등)

`00_Visuals/…/40_Content/01_TitleTMP`를 골라 Inspector의 **Text Input**을 고친다.

![상점 제목](Images/shop-title.png)

1. 창 제목 오브젝트 `01_TitleTMP`
2. 여기 적힌 글이 게임 제목이 된다.
3. 폰트는 한글 전체가 든 `Pretendard-SemiBold SDF`다. 글자가 □로 보이면 폰트에 그 글자가 없는 것이므로 이 폰트로 바꾼다.

## 3. 상점 설명 칸과 스크롤 느낌

설명은 칸을 넘치면 휠로 스크롤된다. 스크롤 바는 없고, 대신 가장자리 흐림·아래 화살표·처음 살짝 내려 보이기로 알린다.

![설명 칸](Images/shop-description.png)

1. `02_DescriptionViewport`: 설명이 보이는 칸. 높이를 "줄 높이 × 4.5"처럼 반 줄이 남게 두면 넘친 줄이 반만 보인다.
2. 설명 칸 크기·위치(씬 뷰나 Rect Transform에서)
3. 가격 줄 `03_PriceTMP`. 설명 칸 아래에 겹치지 않게 둔다.
4. **Shop Description Scroll**: 흐림 높이(Fade Height), 화살표 움직임(Indicator Bob), 살짝 내려 보이기 거리·시간(Nudge…)을 숫자로 고친다. 화살표 그림은 `02_MoreIndicator`의 Image에서 바꾼다.

## 4. 바뀌는 문구: ShopUiTexts

`ShopUiTexts.asset`을 클릭하면 Inspector에 상점 문구가 모두 나온다. `{price}`, `{n}` 같은 **이름표**는 게임이 실제 값으로 바꾼다. 쓸 수 있는 이름표는 칸에 마우스를 올리면 나온다. **빈 칸이면 그 줄을 숨긴다.**

![상점 문구 에셋](Images/shop-texts-asset.png)

1. 탭 위 안내줄: 지금은 비워서 숨김
2. 창 아래 줄: 평소·처리 중·이용 불가·장부 안내, 오류 문구
3. 기물 가격 줄: `개당 {price} 토큰`
4. 신체 복구 설명. 여러 줄은 Enter로 줄을 바꾼다.
5. 신체 복구 가격 줄: `잘린 팔·다리 한 개당 {price} 토큰`
6. 팔·다리 수 고르기 버튼: `팔·다리 {n}/{max}`
7. 목록에 마우스를 올렸을 때 설명 형식

## 5. 기물 가격·설명·발동 문구: 기물 목록

`ItemCatalog.asset` → Items에서 기물을 펼친다.

![기물 목록](Images/item-catalog.png)

1. **Price**: 상점 가격(토큰)
2. **Shop Description**: 상점 설명 칸·마우스 올림 설명
3. **Use Cue**: 장비가 발동할 때 화면 오른쪽 알림(예: 다시 한 번!). 비우면 알리지 않는다.
4. Display Name·Worn Name은 기본값이다. 게임 화면 이름은 문구 파일의 `item.<id>.name`·`.worn`이 우선한다.

## 6. 소지 칸 (화면 오른쪽 아래)

`CommonMenuUI` 씬을 열고 `UI/C5_InventoryCanvas`를 펼친다.

![소지 칸](Images/inventory.png)

1. `00_SlotTemplate`: 칸 하나의 모양. 게임에서는 이 칸을 복제해 두 칸을 만든다.
2. `SelectionBorder`: 고른 칸 테두리(그림·두께는 Image와 Rect Transform)
3. `SelectedProp`: 고른 기물 이름. 아래의 `HeldIndicator`가 도구를 손에 들었을 때의 손 아이콘이다.
4. 씬 뷰 예시(머리핀, 1번 칸, 5회)
5. **Common Inventory UI**: 손 상태 문구(빈칸이면 표시 안 함, `{item}` = 기물 이름), 잠긴 칸 문구, 손 아이콘과 이름 사이 거리

2026-11-02 추가(Task-094, 위 그림 뒤에 바뀜):

- 칸 크기: `C5_InventorySlots`의 Scale(현재 1.5 = 96px). 키트가 칸을 64 단위로 배치하므로 칸 안 배치는 64 기준으로 고치고, 화면 크기는 Scale로 바꾼다. Scale을 바꾸면 `SelectedProp` 높이도 맞춘다.
- `70_Durability`: 칸 아래 내구도 바. 칸 하나 = 내구도 1, 칸 수 = 새 제품 내구도. 모양은 자식 `Segment`(복제 원본), 간격은 Horizontal Layout Group, 색(채움·낡음·빈 칸·사용 번쩍임)은 **Inventory Slot Durability Bar**. 숫자(`60_QuantityTMP`)는 비워 둔다.
- `80_UseFlash`·`85_BreakGhost`와 슬롯 템플릿의 **Inventory Slot Use Effect**: 내구도를 쓰면 칸 번쩍임·아이콘 흰빛, 다 써서 사라지면 붉은 번쩍임·아이콘이 타 없어지는 연출. 세기·시간·색은 컴포넌트에서 고친다.
- 셰이더 효과 머티리얼: `Assets/_ThirdParty/curtaincall/InventorySlotFx/InventorySlotFx.mat`(All In 1 Sprite Shader UI, Hit Effect·Fade). 외부 에셋으로 만든 것이라 private 저장소에 있다.

## 7. 여러 UI가 한 화면에 겹쳐 보일 때

대기실 공용 UI(`CommonWaitingRoomUI`)와 메뉴(`CommonMenuUI`)를 같이 열면 상점·메뉴·HUD가 한꺼번에 보여 겹친다.

![겹쳐 보이는 씬](Images/waiting-room-ui-scene.png)

1. `ShopPanelCanvas/60_Panels/W2_Shop/00_Visuals`: 씬 속 상점
2. 상점 창
3. 같은 자리에 겹친 메뉴 창

- Hierarchy 왼쪽의 **눈 아이콘**(Scene visibility)으로 안 보고 싶은 Canvas를 가린다. 씬 뷰에서만 가려지고 저장·게임에는 영향이 없다.
- 상점만 볼 때는 1번처럼 프리팹 화면이 편하다. 씬 인스턴스에서 값을 바꾸면 그 씬에만 적용(굵은 글씨)되니, 모두에 적용할 값은 프리팹에서 고친다.

## 아직 이 방식이 아닌 UI

스케줄표·설정·자막·배너 패널은 아직 숨긴 상태로 저장되어 있다. 패널을 고른 뒤 `Tools → Curtain Call UI → Preview selected UI in Scene View`로 잠시 보이게 해서 고친다([layout-editing](layout-editing.md)).

## 확인

Play → 개발 배지 또는 Ctrl+Shift+U로 **2일차 밤** 대기실 → 배송구 위 수화기에서 F.
