# Setup
2026-10-20 사용자 직접 지시로 이 checkout의 Task를 UI 씬 통합으로 전환한다. 이전 제작 권한은 History에 보존하며 현재 작업에 적용하지 않는다.
## 작업 구역
- 사용자 후속 요청(2026-10-20): 편집용 UI 미리보기와 두 공연/결과 씬에서 교체 완료된 임시 UI 정리. `Assets/Scripts/MainStage/Tightrope/Tests/PlayMode/TightropeCampaignPlayTests.cs`의 구형 게이지 검증만 새 P1 대상으로 변경한다.
- 이 작업의 외부 인계 원문 보관: `Docs/References/curtaincall_unity_handoff_v1/` (이전 Guides 위치에서 이동, 원문 유지)
- 공통 UI 씬: `Assets/Scenes/UI/`
- 연결 코드·테스트: `Assets/Scripts/UI/SceneIntegration/`
- 별도 검증 씬: `Assets/Scenes/Tests/UiSceneIntegration/`
- 적용 가이드: `Docs/Guides/Ui-scene-integration/`
- Task 문서: 현재 Task 폴더.
## 배정된 공용 파일
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
