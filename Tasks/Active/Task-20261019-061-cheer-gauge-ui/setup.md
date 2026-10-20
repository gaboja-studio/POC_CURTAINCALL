# Setup
2026-10-20 사용자 직접 지시로 이 checkout의 Task를 UI 씬 통합으로 전환한다. 이전 제작 권한은 History에 보존하며 현재 작업에 적용하지 않는다.
## 작업 구역
- 2026-10-21 C13/C14 사용자 직접 지시: CommonMenuUI 공통 씬/SceneIntegration·테스트, Start/Lobby 씬 및 Systems/StartLobby/StartMenu.cs·LobbyRoom.cs 메뉴 진입/구형 UI 정리. 기존 ClientSettings·LeaveToStart 연결, 원본 프리팹 보존.
- 2026-10-21 음성 추가 지시: Systems/Voice/VoiceChat.cs 및 VoiceChatPreferences.cs에 마이크/수신 음량·음소거·개별 단원 로컬 설정과 저장/재연결 적용. SceneIntegration 공통 마이크 상태 HUD/설정 연결. 기존 거리·생사 채널 규칙 유지.
- 2026-10-21 사용자 로비 F/안내 수정: StartLobby/LobbyRoom.cs 입력 대상·공통 C6 표시 연동, SpaceLinkHub 실제 공간 검사 공유. LobbyReadyBoard.cs는 방 종료 후 수신자 없는 송신 방지. 입력·준비·레버 회귀 확인.
- 2026-10-21 사용자 Start→Lobby 카메라 소실 수정: Systems/SpaceLink/SpaceLinkHub.cs의 공간 감지에 공통 UI 씬 제외. 실제 Start/Lobby 전환 회귀 검사 포함.
- C12 사용자 직접 지시(2026-10-21): Systems/Results/PerformanceResult.cs, Systems/Reward/PerformanceRewards.cs·RewardMessages.cs·Logic/PerformanceRewardModel.cs 및 기록 테스트에 호스트 확정 시간/거리 스냅샷 추가. SceneIntegration과 PerformanceTransitionUI에 Assets ResultsPanel 연결. 정본·판정·보상·전환 시간 유지.
- W3 사용자 직접 지시(2026-10-21): ScheduleBoardView.cs 표시 전환, ScheduleBoard.cs 준비 조건, NightSchedule/Logic/ScheduleBook.cs 4일 편성 제거 및 해당 규칙 테스트. CommonWaitingRoomUI·SceneIntegration에 Assets SchedulePanel/CommonButtons 인스턴스와 실제 편집/확정/준비 연결. 실제 Start/Lobby/Tightrope/HumanCannon/WaitingRoomBase와 이들이 사용하는 공통 UI만 대상, 임시 과거 씬 적용/검증 제외. 최종 최소 검증만 실행.
- W2 사용자 직접 지시(2026-10-21): CommonWaitingRoomUI 씬에 ShopPanel 정본 인스턴스·SceneIntegration 어댑터, ShopPhone 표시 상태/API 및 임시 표시 억제, ExpertServicePhonePanel 대체 표시 제거. WaitingRoomShop.cs·ExpertService.cs의 기존 거래에 요청 ID 왕복/결과 이벤트만 추가(가격·권한·판정 유지). 관련 테스트·가이드 포함.
- 사용자 후속 요청: 셔터/통로 정렬 및 전화기 발견성 수정, 모든 공간의 가까운 상호작용 사물 최대2개 공통 외곽선, 대기실 C6 정본 안내. `Assets/Scripts/WaitingRoom/ShopStorage/ShopReach.cs`, `ShopPhone.cs`, `ShopDeliverySlot.cs`, `FloorPropsView.cs`, `HandoverShelfView.cs`, `Assets/Scripts/WaitingRoom/NightSchedule/ScheduleBoardView.cs`, `Assets/Scripts/WaitingRoom/ExpertService/ExpertRoomView.cs`에 표시용 공개 정보와 구형 안내 숨김을 연결한다. 기존 소지/서비스/네트워크 판정은 유지한다. 공통 UI 씬/SceneIntegration 및 기존 대상4개 공간 씬에서 상호작용 표시를 연결한다.
- 사용자 후속 요청(2026-10-20): 최신 대기실 입력·전환 확인, W1/C4/C5/W6 기본 HUD와 대체된 임시 표시 정리. WaitingRoomBase와 공통 WaitingRoom UI 씬, SceneIntegration 코드를 사용한다. 기존 `ExplorationTimerView.cs`에는 임시 화면 생성만 끄는 옵션을 추가하여 종소리는 보존한다. `PropCarry.cs`에는 기존 소지 IMGUI 표시 속성과 요청 결과 알림 이벤트를 추가한다. 게임 판정·입력 규칙은 유지한다.
- 사용자 후속 요청(2026-10-20): 편집용 UI 미리보기와 두 공연/결과 씬에서 교체 완료된 임시 UI 정리. `Assets/Scripts/MainStage/Tightrope/Tests/PlayMode/TightropeCampaignPlayTests.cs`의 구형 게이지 검증만 새 P1 대상으로 변경한다.
- 이 작업의 외부 인계 원문 보관: `Docs/References/curtaincall_unity_handoff_v1/` (이전 Guides 위치에서 이동, 원문 유지)
- 공통 UI 씬: `Assets/Scenes/UI/`
- 연결 코드·테스트: `Assets/Scripts/UI/SceneIntegration/`
- 별도 검증 씬: `Assets/Scenes/Tests/UiSceneIntegration/`
- 적용 가이드: `Docs/Guides/Ui-scene-integration/`
- Task 문서: 현재 Task 폴더.
## 배정된 공용 파일
- `Assets/Scripts/Systems/SpaceLink/Tests/PlayMode/SpaceLinkPlayTests.cs` — 실제 통로 Z=70에서 셔터 열림/닫힘을 검증하도록 기존 좌표 수정.
- `ProjectSettings/EditorBuildSettings.asset` — 공통 UI 씬 등록만(사용자 추가 지시).
- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 사용자 지정 공연 HUD 씬 인스턴스 적용만. 다른 Task 게임 코드는 변경하지 않는다.
- `Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity` — 기존 공연 내용 보존, 정본 HUD 인스턴스와 연결 컴포넌트 추가.
## 읽기 전용 의존성
- C1CheerGauge/PerformanceHud 정본 프리팹, 기존 게임 공개 API, 기존 프리팹·폰트·PNG.
## 수정 금지
- `Assets/_ThirdParty/`
- `Assets/Resources/`
- `Packages/`
- `ProjectSettings/` (위 배정된 EditorBuildSettings.asset 제외)
- 다른 Task 씬·코드. 기존 제작 환경 파일은 수정하지 않는다.
## 검증
- 현재 에디터 경로 확인 후 조회/배치/저장. 컴파일·EditMode·PlayMode·실제 공연/입력/화면을 구분한다.
- 네트워크 다중 클라이언트를 실행하지 못하면 미실행으로 기록한다.
종료 연출 작업 구역은 [기록](History/ending-scope-20261020.md)에 유지한다.
## 2026-10-20 공통 호응 규칙 구현 (PM 사용자 직접 추가 지시)
- HTML 4-1 임시 호응 규칙의 공통 구현을 이번 연결 Task에 추가한다. 아래 항목은 기존 읽기 전용/다른 게임 코드 수정 금지의 명시적 예외다.
- `Assets/Scripts/Systems/Reward/` — 호스트 점수 증감 API, 정산 전 도착 관찰, 공통 공연 호응 관찰 및 테스트.
- `Assets/Scripts/Systems/Settings/AudienceReactionSettings.cs` 및 meta — 임시 호응 설정.
- `Assets/Resources/Settings/GameSettings/Tricks/Rewards/temporary-audience.asset` 및 meta — 신규 설정 에셋.
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonTrampoline.cs`, `HumanCannonTrampoline.GroundBounce.cs` — 호스트 승인 반동 이벤트 공개만.
- `Assets/Scripts/MainStage/Tightrope/Saws/TightropeSaws.cs` — 수직 톱 출현 식별자 읽기 API만.
- 기존 프리팹/그래픽/9-slice 및 공연 성공·실패 판정은 변경하지 않는다.
기존5개 대상 씬 범위는 [원문](History/scene-targets-20261020.md)에 유지한다. 최신 C10/보상 범위는 아래 참조.
## 공통 UI 씬 분리 (사용자 추가 지시)
- Assets/Scenes/UI/ — 공통 UI 씬 추가. C1/C2/C3 공통, P1 공간 소유.
- ProjectSettings/EditorBuildSettings.asset — 공통 UI additive 로딩을 위한 씬 등록만 수정 허용. 위 ProjectSettings 금지의 이 항목만 예외.
- 두 공연 씬의 UI 루트 정리와 공통 씬 연결·수명 관리 포함.
## 2026-10-20 최신 dev 이후 공연 UI 고도화
- 사용자 지시로 기존 SceneIntegration/두 공연 씬/공통 UI 씬에서 C11·H1·C8·C12를 연결한다. 정본 프리팹·그래픽은 수정하지 않는다.
- Assets/Scripts/Systems/Camera/CameraSpectator.cs: 카메라/입력 로직을 유지하고 임시 IMGUI 표시를 끄는 공개 속성만 추가한다. 관전 상태·입력·판정 변경은 없다.
## Dev 시작 지점 (사용자 직접 추가 지시)
- 기존 Dev 모드에 원하는 일차/공연/대기실 시점부터 테스트하는 런 시작 도구를 추가한다.
- Assets/Scripts/Systems/DevMode/: 런 시작 도구·기존 배지 연결.
- Assets/Scripts/Systems/Campaign/RunState.cs, CampaignRun.cs 및 새 RunStartPoint.cs: 검증된 Dev 시작 상태를 한 번에 적용하는 공개 API.
- Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs: Dev 전용 동일 공간 재로딩. 일반 전환 규칙/설정 에셋은 유지.
- SceneIntegration의 Editor/Tests 및 Tests/PlayMode: 진입·재진입·권한·상태 검사. 가이드와 Task 문서 갱신.
- 기존 SceneIntegration/CommonUiSceneLoader.cs: Dev 진입 때 UI 로드 완료를 기다리고 공간 전환 중 새 UI 작업 보류. 원본 UI 프리팹/씬 변경 없음.
## W4·W5 및 수화기 (2026-10-21 사용자 직접 지시)
- WaitingRoomBase 씬: 수화기/배송구 위치와 기존 Assets 전화기 모델 연결, W4·W5 배치, 대체 임시 이름표 제거.
- ShopReach.cs, FloorPropsView.cs, HeldPropsView.cs, HandoverShelfView.cs, ShopDeliverySlot.cs, ExpertRoomView.cs: 표시용 데이터 API와 기존 이름표 표시 제어만. 게임 판정/거래 규칙은 유지.
- SceneIntegration/ 및 해당 테스트, Docs/Guides/Ui-scene-integration/: 연결 컴포넌트/에디터 설치/검증/가이드.
- 완성 WorldLabels 프리팹과 제공 코드·폰트·PNG·9-slice 원본은 읽기 전용.
## 상호작용 창 커서 (2026-10-21 사용자 직접 지시)
- Assets/Scripts/Systems/Camera/CameraViewDirector.cs: 공통 InputGate와 커서 해제/복귀 연결, 창 종료 ESC와 수동 해제 충돌 방지. SceneIntegration 테스트/가이드 갱신. 카메라 구도/게임 판정/정본 에셋 유지.
## C12 1인·C16 (2026-10-21 사용자 직접 지시)
- SceneIntegration에 정본 C12 표시 소스를 기반으로 1~4인 지원 로컬 어댑터 추가. 정본/그래픽 유지, 씬 컴포넌트만 교체. PerformanceTransitionUI에 커튼 인스턴스, 기존 요약/검은 오브젝트 정리.
- Systems/SpaceLink/SpaceLinkDirector.cs와 Systems/StartLobby/StartLobbyFlow.cs에 커튼 차폐 대기/완료 훅 연결. 로드 판정/권한 유지. 실제5개 씬만 대상.
## C10·기본 자금 (2026-10-21 사용자 직접 지시)
- Systems/Results/PerformanceResult.cs와 Systems/Reward/ 모델·테스트: 시작 참가 인원 ×10을 성공/실패 모두 기존 정산에 가산. 호스트 장부·중복 방지 유지.
- SceneIntegration 코드·Editor·테스트, CommonPerformanceUI: C10 키트 조립·실제 호응 변화·HUD 중첩/종료 수명 연결. 완성 C10 프리팹 부재 확인 후 신규 Assets/Resources/Prefabs/UIs/Feedback/와 Images/UIs/Feedback/ 추가. 원본 PNG 바이트·파일명 유지, 복사본만 Sprite import.
- MainStage/Tightrope/Piggyback/PiggybackSystem.cs: 손잡기 표시 이벤트 및 교체된 임시 표시 억제만. 보상/착지 판정 변경 없음.
- Systems/Voice/VoiceChatDevOverlay.cs: 기존 Dev 음성 패널의 표시 오프셋/억제 API만. 공연 C3 아래 배치, 메뉴·관전·결과 중 숨김, 공통 UI 해제 시 원복.
- 2026-10-21 사용자 Start 타이틀 지시: Start.unity 중앙 로고/메뉴·페이지 재배치, UI 루트 정리. Assets TitleLogo 인스턴스 사용, 원본 변경 없음.
- 2026-10-21 사용자 팝업/화면비 지시: Start 참가/LAN을 BasicPanels/CommonButtons 인스턴스로 교체, 실제5개 진행 씬 및 공통 UI의 화면 Canvas를1920×1080/Expand로 통일. 월드 UI 크기 보존.
- 2026-10-21 로비 UI/시작 순간이동 사용자 지시: Lobby.unity·SceneIntegration에 Assets LobbyHud/Guides/알림 연결, 대체 HUD 제거. Systems/StartLobby/LobbyRoom.cs는 실제 공간 복귀에만 재배치하도록 수정, 실제 LAN 회귀 검사 포함.
- 2026-10-21 사용자 전체 점검: 실제5개 공간/공통UI만. WaitingRoomBase의 대체된 비활성 테스트UI 제거, NightDoorLock.cs 표시 옵션 및 CommonWaitingRoomUI 밤 배너 문구만 수정. 게임 문 잠금 판정 유지.
- 2026-10-21 사용자: 상점/일정 hover·외줄 안내 교체, TightropeControlHints.cs의 문구 API/구형표시 전환 허용. 실행 검증은 사용자에게 위임. commit/push 및 dev 대상 PR 준비 승인(생성/병합은 미요청).
