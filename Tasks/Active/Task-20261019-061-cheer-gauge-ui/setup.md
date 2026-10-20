# Setup
2026-10-20 사용자 직접 지시로 이 checkout의 Task를 UI 씬 통합으로 전환한다. 이전 제작 권한은 History에 보존하며 현재 작업에 적용하지 않는다.
## 작업 구역
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
