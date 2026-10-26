# 공연 종료 UI 적용

2026-10-20, Task061. 외줄/인간대포의 판정은 기존 게임 로직을 유지한다.

## 표시와 시간
- 로컬 플레이어 Fallen/Retired 또는 보상 스냅샷 Dead: «사망했습니다» 알림을 공연당 한 번, 2초 표시한다. 동료 사망만으로 내 알림을 띄우지 않는다.
- 결과 확정 즉시 정산·공유하고 조작을 잠근다. 1.5초 후 결과창, 4초 동안 결과 표시, 0.3초 검은 화면을 먼저 표시한 뒤 기존 Campaign/SpaceLinkDirector가 전환한다.
- 새 공간 로드와 SpaceLinkDirector 전환 완료 후 최소0.5초를 두고 검은 화면/입력 잠금을 해제한다. 기존 실패 대기 설정이 더 길면 그 값을 보존한다.
- 전환이 45초를 넘기면 화면/입력 잠금을 풀고 연결 상태 확인 알림을 표시한다. 씬 로드 취소나 네트워크 재접속을 대신 수행하지 않는다.
- 시간 설정: `Assets/Resources/Settings/GameSettings/Tricks/Rewards/performance-ending.asset`. 로딩 비용을 낮추는 최적화가 아니라 임시 화면 가림이다.

## 구성과 데이터
- `Assets/Scenes/UI/PerformanceTransitionUI.unity`: 공연 공간 진입 시 로컬 additive 로드. 루트는 Single 교체에도 유지하며 UI/DeathNotificationCanvas, ResultCanvas, BlackTransitionCanvas 아래 정리했다. 새 EventSystem은 만들지 않는다.
- Assets의 Notifications/SystemNotifications와 BasicPanels/BasicPanels 정본 프리팹 인스턴스를 사용한다. 결과 전용 정본은 없으므로 메뉴 패널의 씬 인스턴스에서 버튼을 숨기고 제목·정산 텍스트를 배치했다. 원본 파일명/9-slice/PNG는 변경하지 않는다.
- 결과 제목은 PerformanceResult.Succeeded, 팀 호응은 Participants.Segment 합계, 수입은 Income, 단원별 표시는 Slot/Dead/Connected/Segment를 사용한다. 성공/실패 판단이나 수입을 UI에서 재계산하지 않는다.
- 사망 알림은 화면 상단, 결과는 중앙640×600, 검은 화면은 전체 화면이다. 검은 Canvas와 IMGUI 모달 덮개를 사용한다. 에디터 캡처에서는 기존 개발 IMGUI 일부 글자가 검은 화면 위에 남는 제약이 있으며 개발 패널 정리는 후속 대상이다.
- 나중 전환 연출은 이 씬의 BlackTransitionCanvas와 PerformanceEndingPresentation.Cover/SceneLoaded 완료 처리를 확장한다. 공간 씬에 연출을 복제하지 않는다.

## 실제 수정 파일
- 신규: PerformanceTransitionUI.unity 및 meta, performance-ending.asset 및 meta.
- 신규: Systems/Settings/PerformanceEndingSettings.cs, UI/SceneIntegration/PerformanceEndingPresentation.cs, Editor/PerformanceEndingInstaller.cs, Tests/PlayMode/PerformanceEndingPlayTests.cs 및 각 meta.
- 수정: Systems/Reward/PerformanceRewards.cs(정산 후 전환 지연), UI/SceneIntegration/CommonUiSceneLoader.cs(로컬 전환 씬 수명/NGO 제외), UiRuntimeFontFallback.cs(결과 글자 지원), Tests/PlayMode/PerformanceSceneBindingPlayTests.cs(지속 UI 정리), ProjectSettings/EditorBuildSettings.asset(전환 씬 등록).
- 가이드/Task 문서 갱신. 앞선 호응 구현과 기존 사용자 변경은 별도로 보존하며 이번 작업으로 commit/push하지 않았다.

## Unity 사용자 확인
1. Start에서 호스트로 시작하여 외줄에 입장한다. 추락/불 접촉으로 자신이 죽으면 사망 알림은 한 번만, 개인 호응은0으로 표시돼야 한다.
2. 두 명 이상이면 동료만 죽었을 때 살아 있는 내 화면에 «사망했습니다»가 뜨지 않는지 확인한다. 내 사망 후 팀이 계속 진행하는 경우 알림은 사라지고 기존 관전 흐름이 유지돼야 한다.
3. 전체 실패/성공 후 약1.5초 대기, 결과4초, 검은 화면, 대기실 순서를 확인한다. 결과의 팀/개인 호응·수입·사망 표시를 기존 정산과 대조한다.
4. 대기실에서는 검은 화면과 입력 잠금이 남지 않아야 한다. 인간대포에서도 같은 순서를 확인한다.
5. 같은 세션에서 다음 공연까지 진행하여 이전 결과가 재표시되거나 공통 UI가 중복되지 않는지 확인한다.
6. 2~4클라이언트, 느린 로딩, 연결 해제/재접속, 다른 해상도와 최종 빌드 검증은 미실행이다. 현재 자동 테스트/호스트 합성 실행은 실제 키보드 완주를 대체하지 않는다.

검증 상세: [Task 기록](../../../Tasks/Done/Task-20261019-061-cheer-gauge-ui/ending-verification.md).
