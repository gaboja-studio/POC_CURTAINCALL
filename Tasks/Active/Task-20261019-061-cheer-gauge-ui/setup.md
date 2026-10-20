# Setup
2026-10-20 사용자 직접 지시로 이 checkout의 Task를 UI 씬 통합으로 전환한다. 이전 제작 권한은 History에 보존하며 현재 작업에 적용하지 않는다.
## 작업 구역
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

## 2026-10-20 종료 연출 (사용자 직접 추가 지시)
- 두 공연 로컬 사망 알림, 종료 대기/결과 표시 시간, 임시 검은 전환 씬 추가.
- 기존 SceneIntegration/Scenes/UI 구역과 PerformanceRewards 전환 지연을 사용한다.
- `Assets/Scripts/Systems/Settings/PerformanceEndingSettings.cs` 및 meta, `Assets/Resources/Settings/GameSettings/Tricks/Rewards/performance-ending.asset` 및 meta를 추가한다.
- EditorBuildSettings에는 전환 UI 씬을 추가한다. 게임 성공/실패 판정·원본 UI 프리팹·그래픽은 유지한다.

## 2026-10-20 공통 호응 규칙 구현 (PM 사용자 직접 추가 지시)
- HTML 4-1 임시 호응 규칙의 공통 구현을 이번 연결 Task에 추가한다. 아래 항목은 기존 읽기 전용/다른 게임 코드 수정 금지의 명시적 예외다.
- `Assets/Scripts/Systems/Reward/` — 호스트 점수 증감 API, 정산 전 도착 관찰, 공통 공연 호응 관찰 및 테스트.
- `Assets/Scripts/Systems/Settings/AudienceReactionSettings.cs` 및 meta — 임시 호응 설정.
- `Assets/Resources/Settings/GameSettings/Tricks/Rewards/temporary-audience.asset` 및 meta — 신규 설정 에셋.
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonTrampoline.cs`, `HumanCannonTrampoline.GroundBounce.cs` — 호스트 승인 반동 이벤트 공개만.
- `Assets/Scripts/MainStage/Tightrope/Saws/TightropeSaws.cs` — 수직 톱 출현 식별자 읽기 API만.
- 기존 프리팹/그래픽/9-slice 및 공연 성공·실패 판정은 변경하지 않는다.

## 후속 사용자 지정 대상 (2026-10-20)
- `Assets/Scenes/Start_Lobby/Start.unity`
- `Assets/Scenes/Start_Lobby/Lobby.unity`
- `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`
- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity`
- 위5개 씬만 적용. 첫 단계는 두 공연 씬의 HUD, 나머지는 후속 화면 단계에서 정본 존재 확인 후 적용한다. 게임 규칙/다른 Task 코드의 수정 권한으로 확대하지 않는다.

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
