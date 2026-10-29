# 실제 진행 UI 누락·임시 표시 점검
2026-10-21. 직전 로비 UI/순간이동 수정은41bce04 commit/push 완료. 이번 정리는 미커밋.

## 대상과 확인 방법
- Start, Lobby, WaitingRoomBase, Tightrope, HumanCannon 및 연결 공통UI5개 씬만 조사. 과거 프리팹 테스트 씬 제외.
- Unity API로 실제 프리팹 연결/누락 스크립트/OnGUI 컴포넌트 조사, Assets 완성 프리팹 목록 및 어댑터 코드 대조. 인계 package 재추출 없음.
- 정적 원자료: .harness/local/ui-audit.json. 조사10개 씬 누락 스크립트0. 이는 전체 플레이 통과를 뜻하지 않는다.

## 적용표
|대상|Assets 정본 및 연결 확인|남은 범위|
|---|---|---|
|Start T1/T2/T3|TitleLogo, BasicPanels, CommonButtons. 실제 생성/참가/LAN/설정/종료 연결|시안 배경 원본 없음: 기존3D 무대 사용. 메인 메뉴 선택 장식은 시안과 차이 있음|
|Lobby L1/L2/L3|LobbyHud, Guides, SystemBanner, Toast. 실제 방 정보/준비/레버 조건|출연 명단 월드 글자/레버 표시는 기존 기능 유지, 전용 명단 프리팹 없음|
|대기실 W1/C4/C5/W6|BackstageHud 및 자금/소지/집결/시계 어댑터|다인 소지·배송 실플레이|
|W2/W3|ShopPanel, SchedulePanel 및 거래/장부/일정/확정/준비|판매 API 없음. 의료/배송 지연·다인 상태 검증|
|W4/W5|WorldLabelCloth/Shelf 인스턴스, Item 프리팹 런타임 생성|겹침/시선/거리의 다인 실제 동선|
|공연 C1/C2/C3/P1/H1|CheerGauge, PerformanceHud, HumanCannonUiBinding|두 공연 전체 완주/다인/화면비|
|C6/C7/C15|InteractionPrompt, Guides, Tooltips 연결|Tooltips는 공연 공통 UI에 있음. 상점/일정의 별도 hover 툴팁 연결은 확인되지 않음. 상세 정보 패널과 별개인 미적용 범위|
|C8/C9/C10/C11|Notifications, 대기실 Captions, Feedback, Spectator|자막은 현재 방송 이벤트 연결 범위. 미구현 대사까지 적용 완료로 간주하지 않음|
|C12/C16|ResultsPanel, TransitionCurtain|원격 전원 차폐ACK·느린 네트워크·복귀 검증|
|C13/C14/음성|BasicPanels/SettingsPanel/CommonButtons, 마이크 상태 아이콘|실제2기기 음성/단원별 설정·Mac/빌드|

## 이번 정리
- WaitingRoomBase에서 비활성 NetworkSessionTestUI/NightScheduleTestUI/ExpertServiceTestUI 컴포넌트3개 제거. 테스트 코드 원본은 보존. Dev 시작 지점 도구는 유지.
- NightDoorLock에 showLegacyNotice 옵션 추가, 실제 대기실 인스턴스만false. 문 잠금/셔터 로직 유지.
- CommonWaitingRoomUI의 기존 새 밤 배너에 ‘탐색 시간 없음’ 포함. 구형 상단 IMGUI 박스 중복 제거.
- 대기실 showLegacyUi/showLegacyLabels/showHud/showStatus가false인 것 확인. 게임 처리 컴포넌트 자체를 삭제하지 않음.

## 보존한 항목
- 공연 DevelopmentTools의 접속·연습 패널: CampaignPerformanceEntry가 실제 캠페인 진입에서 차단. 단독 연습에 사용하므로 보존.
- TightropeControlHints는 아직 모든 조작을 대체한 새 가이드가 아니므로 유지. 실게임에서 보이는 잔여 텍스트이며 정리 완료로 숨기지 않음.
- Dev 배지/시점 선택/음성 진단, 연습 봇, 월드 명단/사물 표지: 기능이 있는 개발/월드 표시로 구분.
- PerformanceEndingPresentation의 검은 화면 fallback: 커튼 참조가 없을 때만 사용, 정상 경로의 중복 UI 아님.
- 외부 작업 폰트2/재질3/ui-workspace.md, runInBackground 설정 차이는 이번 저장/정리에서 제외.

## 검증과 다음
- 컴파일 PASS. 이번 변경은 대기실 알림·중복 로드 회귀1개만 실행하며 결과는 handoff에 기록.
- 미실행:5개 씬 전체 동선 반복, 실제 다인·재접속·네트워크 지연, 다양한 화면비·Mac/빌드, 전체 시각 비교.
- 사람 확인: 밤 진입 시 새 배너 한 번만 표시되고 구형 상단 박스가 없는지, 문은 잠기는지, 상점/스케줄/F 입력 유지, 공연 진입 때 테스트 접속 패널이 보이지 않는지.
- 최종 대기실 알림·공통UI 재등록 회귀:1/1 PASS. 전체 테스트 재실행 없음.

## 후속 구현 / 사용자 검증 위임
- 상점/일정 hover: CommonWaitingRoomUI에 Assets Tooltips 프리팹 추가. PanelHoverTooltip.cs는 실제 포인터 raycast·0.35초 지연·화면 경계 제한·메뉴/패널 닫힘 시 숨김 연결.
- 상점은 ShopRowVisual.EntryId에 해당하는 실제 설명/가격/구매 불가 사유, 일정은 항목 표시와 편집/확정/준비 가능 사유를 사용. WaitingRoomScheduleUI.HoverDescription 공개 API 추가.
- 외줄: TightropeControlHints.CurrentText로 기존 상태별 모든 안내 문구를 보존. 실제 Tightrope 씬의 BasicPanels 인스턴스와 TightropeControlsPanel로 표시, 기존 GUI.Box는 해당 어댑터 활성 시 억제. 단독 기존 테스트 씬 fallback 유지.
- 새 패널은1920×1080 Expand, 원본 prefab/9-slice 수정 없음. 외줄 패널 자동 글자 크기16~23, 하단C6 영역과 분리.
- 사용자 요청으로 Play 테스트/실행 캡처 미실행. 에디터 배치용 컴파일만 PASS. 앞선1/1 결과는 이번 hover/외줄 후속 구현 검증 결과가 아님.
- 확인: 상점 품목/서비스/불가품 hover, 스크롤·탭 전환·닫기/ESC, 일정 항목·확정·준비 버튼 hover, 툴팁 화면 가장자리, 외줄 지상/줄/목마/봉/릴레이/받기/관문/추락에서 안내 내용과 겹침·글자 잘림.
- dev PR 준비: 사용자 직접 지시에 따라 Task Integration을dev로 변경. PR 생성·병합은 수행하지 않음.
## 2026-10-21 상황별 외줄 안내·공통 C5 기물 선택
- 이전 하단 BasicPanels 외줄 안내를 제거하고 Assets Notifications/SystemBanner 인스턴스로 교체했다. 출발 WASD → 줄 탑승 W/S·A/D → 움직이는 가로 톱8m 접근 SPACE 안내를4초씩 표시한다. 톱별1회, 플레이어 재생성 시 초기화. 메뉴·사망·전환 중 숨긴다.
- CommonMenuUI에 Assets BackstageHud/InventorySlotTemplate 전체 프리팹 인스턴스를 추가했다. 시작/로비 제외, 실제 대기실·외줄·인간대포에서5개 슬롯을 상시 표시한다. 대기실의 기존4초 소지 표시 경로는 제거했다.
- 1~5 숫자키는 실제 소지 항목 선택 요청을 호스트에 보낸다. 호스트가 소유권/한도를 검사하고 선택 UID를 기존 carry 스냅샷에 포함한다. 선택 강조·이름·손에 든 도형·F 길게 내려놓기·선반 올리기가 선택 항목을 사용한다. 메뉴/상점 입력 중에는 선택하지 않는다.
- 기존 소지 한도(팔2=2, 팔1=1, 배낭+2)를 유지한다. 따라서 현재 정상 최대4칸이고5번은 잠금 표시다. 소지 한도5개 확대는 하지 않았다. 빈 칸/잠긴 칸 키는 무시한다.
- 개별 기물 아트가 없는 품목은 delivery 공통 아이콘과 실제 이름으로 표시한다. 손의3D 표시는 기존 임시 큐브이며 선택한1개만 보인다. 기물 사용 효과는 이번 범위 아님.
- 수정: CommonInventoryUI.cs(+meta), ContextualHudInstaller.cs(+meta), TightropeControlsPanel.cs, CommonWaitingRoomUI.cs, PropCarry.cs, HeldPropsView.cs, ItemCarryBook.cs, CommonMenuUI.unity, 실제 Tightrope/HumanCannon.unity, Task 문서.
- 확인: Unity 컴파일 PASS, 변경3개 씬 참조 연결 정상/누락스크립트0, diff 공백 검사 PASS. PlayMode·다인 동기화·사용자 입력·시각 검증은 사용자 요청에 따라 미실행.
- 사용자 확인: 출발/탑승/톱 접근 알림과 균형 게이지 중첩, 상점 수령 후1/2 교대와 손의 기물·이름 일치, 선택 기물 내려놓기/선반, 공연 전환 후 소지 유지, ESC·상점에서 숫자키 차단, 두 클라이언트 선택 상태/손 표시 일치.

## 2026-10-21 사용자 수정: 한글 안내·기물 집어넣기
- 실제 Tightrope 씬 ContextualControlsBanner 제목/설명 폰트를 SettingsUi Pretendard-SemiBold로 교체. 새3단계 안내 전체 문자의 폰트 누락0 확인. 정본 프리팹/폰트 에셋 보존, 설치 코드에도 같은 연결 적용.
- 같은 번호 재입력 또는 빈 슬롯 번호 입력은 보유 기물을 유지하며 빈손으로 전환. 기물이 있는 번호를 다시 누르면 꺼낸다. 잠긴 빈 칸도 집어넣기 입력으로 사용할 수 있다. 호스트 선택 UID=-1을 명시적 보관 상태로 동기화하여 기존 자동 마지막 기물 선택과 구분한다.
- 빈손에서 F 길게는 아무 기물도 내려놓지 않는다. 선반에서는 보관 중인 기물을 자동으로 올리지 않고 꺼내기 경로를 사용한다(소지 한도 유지).
- 수정 파일: PropCarry.cs, Logic/ItemCarryBook.cs, Editor/ContextualHudInstaller.cs, 실제 Tightrope.unity. 이전 미커밋 변경 유지.
- 사용자 확인: 같은 번호로 꺼내기/집어넣기 반복, 빈 칸으로 집어넣기, 물품 개수 유지, 빈손 상태의 F 길게, 다른 클라이언트의 손 표시. 실행·다인 검증 미실행.
