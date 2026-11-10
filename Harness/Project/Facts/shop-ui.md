---
summary: W2 수화기 구매·판매 표시 패널·요청 토큰 계약과 독립 확인 씬
status: active
updated: 2026-11-01
source: Task-20261019-061, Task-20261101-092
---

# W2 수화기 구매·판매 표시 UI

## 언제 읽나
- 수화기 패널 조립·표시 API 호출·외부 거래 어댑터 연결 전에 읽는다.

## 사실
- 제공 Runtime은 private shop 키트의 `CurtainCall.UI.ShopV1`, 조립 API는 `CurtainCall.UI.ShopV1.Editor.ShopPanelBuilder.BuildProject`다. 공개 코드는 `Assets/Scripts/Systems/ShopUi/` 전용이다.
- 제공 배치 계약: Canvas1920×1080/Match.5, 패널1056×552/(432,272), 상단240 HUD 보호·전면 딤 없음. 수화기224×80은 Simple 고정, 목록 viewport592×296/행592×80/간격4·동적 스크롤(2026-10-20 사용자 높이 변경 승인), 꼬리 없는 우측 설명376×296.
- 거래 불가도 행 선택/설명을 허용한다. 선택/포커스는 독립 레이어, disabled와 unavailable을 구분하고 글자 alpha1을 유지한다. busy는 행동 버튼만 차단하며 탭/행/닫기는 유지한다.
- `ShopPresentation`/`ShopEntryPresentation`/`ShopActionPresentation`은 복사·읽기 전용 표시 자료다. 품목ID 전역 고유·행동ID 항목 내 고유·최대2행동·장부 행동0. 가격/시간/금액은 전달된 문자열이며 UI가 계산하지 않는다.
- `Open`/`SetPresentation`/`SelectTab`/`SelectEntry`/`RequestAction`/`AcceptAction`/`RejectAction`/`Close`/`Hide`를 Unity 메인 스레드에서 호출한다. revision의 선후 판단·서버 권한/거래 멱등성은 외부 책임이다.
- 요청에는 ViewId/Epoch/RequestToken/SessionId/Revision/EntryId/ActionId가 포함된다. 같은 세션 갱신은 pending을 유지하며 원래 요청으로 완료한다. close/reopen/disable 뒤 응답·중복/다른 View 응답은 거절한다.
- Close는 닫기 의도와 Closed를 통지하고 Hide는 Closed만 통지한다. Closed는 퇴장 연출 완료가 아닌 닫기 처리 시점이다. 이미 전송된 실제 거래를 취소하거나 롤백하지 않는다.
- 데모의 구매·판매/서비스·장부 수치는 검토용이다. 실제 경제/재고/배송/저장/네트워크·커서/입력 소유·HUD 연결은 구현하지 않는다.
- 생산49PNG=신규9+승인40, 전체 키트276파일/PNG58, 누적174고유 PNG 이력과 구분한다. 전체 원본·자료·GUID 보존, 신규 importer만 보정·기존 정본/Static 폰트 읽기 전용 재사용한다.

## 대기실 연결·편집 위치 (Task-092, 2026-11-01 사용자)
- UI 배치·고정 문구는 프리팹/씬에서, 바뀌는 문구는 설정 에셋에서 고친다. 코드에 화면 문구를 적지 않는다.
- 창 제목: `ShopPanel.prefab`의 `01_TitleTMP` Text('상점'). `WaitingRoomShopUI.titleLabel`이 그 글을 제목으로 쓴다. 폰트는 한글 전체를 가진 정적 `Fonts/SettingsUi/Pretendard-SemiBold SDF`(전용 Bold는 옛 제목 110글자뿐이라 □로 보였음).
- 프리팹은 편집할 때 보이도록 `W2_Shop/00_Visuals`를 켜고 alpha 1로, 줄·설명·버튼에 기물 탭 예시 내용을 넣어 저장한다. 게임에서는 `ShopPanelView.Awake`의 ForceHidden이 숨기고 열 때 실제 내용으로 바꾼다([ui-authoring](../Decisions/ui-authoring.md)).
- 가격 줄·버튼·설명 꼬리말·장부·마우스 올림 설명·탭 위 안내줄·아이콘 키: `Resources/Settings/WaitingRoom/ShopUiTexts.asset`. 빈 칸이면 그 줄을 숨긴다.
- 기물 이름은 대기실 문구 파일 `item.<id>.name`, 상점 설명과 장비 발동 문구는 `ItemCatalog` 항목의 `shopDescription`·`useCue`.
- 키트 `ShopPanelView`가 코드에 적은 창 아래 기본 문구(플레이는 계속됩니다·요청 처리 중…·이용 불가/장부 안내)는 씬 인스턴스의 `ShopFooterTexts`가 `ShopUiTexts` 값으로 바꾼다. 키트 문구가 바뀌면 이 컴포넌트의 상수도 맞춘다.
- 상세 설명은 `ShopPanel.prefab`의 `40_Content/02_DescriptionViewport`(RectMask2D·ScrollRect·투명 Image, 스크롤 바 없음) 안 `02_DescriptionTMP`(Overflow·ContentSizeFitter)다. 높이 4.5줄(약 129px), 글 폭 300px, 오른쪽 28px는 `02_MoreIndicator`(설정 키트 꺾쇠 −90°) 자리. 가격·사유·상태 줄은 y 200/230/258.
- `ShopDescriptionScroll`(뷰포트): 더 읽을 쪽 가장자리만 흐림(RectMask2D softness 40, 끝에 닿은 쪽은 음수 여백으로 밖에 밀어냄), 아래 글이 남으면 꺾쇠 표시·까딱임, 넘치는 설명을 고르면 0.45초 뒤 22px 내려갔다 돌아옴(휠·드래그 시 중단), 항목이 바뀌면 맨 위. 수치는 인스펙터에서 고친다.
- 키트 테스트 `InitialHiddenCanvasAndTypography`는 스크롤 내용 TMP만 Overflow를 허용한다.

## 확인 방법
- 산출물: `Assets/Resources/Prefabs/UIs/Roles/Retained/ShopUi/ShopPanel.prefab`, `Assets/Scenes/Tests/ShopUi/ShopUi.unity`. 씬 Play 후 `데모 열기`로 확인한다. 검증 정본은 Task의 `shop-ui-verification.md`다.
- ShopUi Medium은 기존 atlas/material을 공유하는 Static 변형이며 전용 Static512 보조 폰트로 U+2212를 보완한다. Bold는 고정 제목/ASCII/기호110글자의 전용 Static2048, SemiBold는 기존 정본이다. 기존 폰트는 수정하지 않는다.
- Pretendard 행 제목24pt/부제18pt는 각각 높이32/24를 확보한다. 높이28/20에서는 Ellipsis가 세로 초과로 글자를 전부 숨길 수 있다. 후속 사용자 요청으로 행 높이80·상하 여백12/글자 크기 유지로 변경했다. 배치/스크롤 보폭은84다.
- `Scripts/check-work.ps1` 및 W2 선택 PlayMode 검사를 사용한다. 실행 캡처/합성 입력·물리 입력·사람 외형 승인을 구분하고 실패/생략/환경 제약을 그대로 기록한다.
