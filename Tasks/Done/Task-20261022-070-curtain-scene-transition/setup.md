# Setup

PM 직접 긴급 수정. 기존 세션 대신 D:/github/POC_CURTAINCALL-curtain-fix worktree·전용 Unity 7801 사용.

## JIRA

- 관련 JIRA: 없음 (PM 직접 긴급 요청)

## 작업 구역

- `Assets/Scripts/UI/SceneIntegration/SceneCurtainTransition.cs`
- `Assets/Scripts/UI/SceneIntegration/Tests/PlayMode/PerformanceResultsPlayTests.cs`
- `Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs`
- `Assets/Scripts/Systems/StartLobby/StartLobbyFlow.cs`
- `Assets/Scripts/UI/SceneIntegration/CommonWaitingRoomUI.cs`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음

## 배정된 공용 파일

- 없음 (기존 씬·프리팹 저장본 변경 없음)

## 수정 금지

- `Assets/Scenes/`
- `Assets/Resources/`
- `Assets/_ThirdParty/`
- `ProjectSettings/`
- `Packages/`

## PM 승인·구역 중복

- 2026-10-22 사용자 채팅: 기존 네 파일의 커튼 동기화·검증 변경 승인.
- 추가 긴급 예외 승인: 같은 PR에서 CommonWaitingRoomUI.cs의 탐색 경고를 기존 상단 배너·최상위 표시로 수정. Task-061 중복 경로이며 알림 시점·씬·정본 에셋은 변경하지 않는다. 검증 최소화 요청.
- UI 두 파일은 Task-061, 시스템 두 파일은 Task-062와 겹침. 기존 Task의 다른 변경을 포함하지 않는다.
- 기존 Task 담당자는 dev 병합 후 본 패치와 겹치는 수정의 충돌·동작을 재확인한다.
- 기본 new-task.ps1은 integration 및 신규 branch만 지원하므로, 이미 생성한 긴급 fix의 기록은 동일 공통 함수·전체 ref 번호 검사·공유 잠금으로 070을 발급했다.

## 테스트 씬 처리

- 결정: 새 씬 없음. 기존 Start/Lobby 및 실제 묘기·대기실을 활용.
- 결정자·날짜: PM 요청 범위, 2026-10-22
