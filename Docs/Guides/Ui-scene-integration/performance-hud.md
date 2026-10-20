# 공연 HUD 씬 적용

최신 추가 구현·검증은 [공통 호응 규칙](audience-rules.md)을 따른다. 아래 초기 검증 기록은 당시 결과이며 현재 1인 진행도와 실제 호응 규칙은 추가 적용됐다.

## 기준과 보호 범위
- Unity 6000.3.25f1, URP17.3.0, uGUI2.0.0/TMP, Input System1.20.0(신 입력만), NGO2.13.3, Multiplayer Services2.3.3, Pipeline0.8.0-exp.1.
- 제작 기준 씬: Assets/Scenes/Tests/CheerGauge/C1CheerGauge.unity, Assets/Scenes/Tests/PerformanceHud/PerformanceHud.unity. 나머지 전달된 UI 검증 씬은 후속 적용 기준으로 보존한다.
- 정본: Assets/Resources/Prefabs/UIs/CheerGauge/C1CheerGauge.prefab 및 PerformanceHud/PerformanceHud.prefab.
- 인계 Packages의 ZIP은 사용하지 않는다. PNG/importer/9-slice/폰트/정본 프리팹은 변경하지 않는다. Preview/Controls 합성 데이터는 공연에 이식하지 않는다.

## 화면 구성
- 적용 씬: Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity 및 HumanCannon/HumanCannon.unity.
- 사용자 추가 지시에 따라 C1/C2/C3는 별도 CommonPerformanceUI.unity, P1은 외줄 공간 UI 루트에 배치했다. 로딩·소유권·Hierarchy는 [shared-ui-scenes.md](shared-ui-scenes.md) 참조.
- PerformanceHud 정본의 Overlay/Scale With Screen Size/1920×1080/Match0.5/PPU100을 사용한다.
- C1: 상단 중앙192×208, (0,-24). C2: 좌상단160×56, (48,-40).
- C3: 우상단408×76, (-48,-40). P1: 하단 중앙544×80, (0,160).
- HUD는 비인터랙티브이며 GraphicRaycaster/EventSystem/InputModule을 추가하지 않는다. 기존 입력 소유권/메뉴/커서 정책을 유지한다.
- 기존 시작/재시작 개발 패널은 기능을 보존한다. 최종 메뉴/UI 교체와는 별도 작업이다.

## 연결 원천
| 표시 | 기존 게임 원천 | 단위/권한 |
|---|---|---|
| C1 | PerformanceRewards.State/Changed, C1GaugeProjection | long 실제 호응/상한, 호스트 권위 스냅샷 |
| C2 | TightropeFire.Settings + Elapsed | 초, chaseDelay + (safeDistance-chaseStart)/chaseSpeed - Elapsed, 0 아래 clamp |
| C3 | TightropeCourse.FinishDistance/GetDistance, NetworkPlayer, TightropeFire | Unity 코스 거리, 기존 동기화 위치/불 상태 |
| P1 | NetworkPlayer.Local의 PlayerBalance, PlayerInputReader.Current.Posture | Value/MaxValue·GreenLimit/MaxValue·WarningLimit/MaxValue, RedTimeRatio |
- UI는 기존 값에서 표시용 남은 시간·정규화 좌표만 계산한다. 점수/실패 규칙이나 RPC를 추가하지 않는다. 입력은 기존 reader의 값을 표시만 한다.
- 하단 P1은 로컬 균형이 활성이고 공연 중일 때만 표시, Fallen/Retired/HasFallen이면 숨긴다.

## 확인된 정본 제약
- C1은 시작 명부를 유지한다. 사용자 추가 지시로 두 공연의 임시 규칙 상한100/절단30을 공통 보상 시스템에서 적용한다.
- C3 정본 SetProgress는2~4인이다. 1인은 동일 씬 인스턴스의 실제 마커를 SoloPerformanceProgress로 표시한다. 정본 API와 그래픽은 유지한다.
- 외줄 최대 도달은 TightropeFire.FurthestDistance, 추락 위치는 로컬에서 마지막으로 관측한 거리다. 관측하지 못한 과거 값을 추정하지 않는다.

## 검증 상태
- 2026-10-20 최종 분리 구조 컴파일 PASS, 신규 PlayMode 테스트 9/9 PASS. 무데이터 숨김·기존 Canvas 복원·raycast 비차단·타이머 경계/잘못된 속도·공통 씬 수명을 검사했다.
- 실제 외줄 씬: LAN 1인 호스트, Running, C1 실제 상한100, 불 경과에 따라 C2 8:19→8:18, 로컬 균형 활성 P1 alpha1 확인. 테스트 API로 Fallen 전이 후 기존 Run Failed와 P1 alpha0 확인. 실제 키보드/불 접촉 검증과는 구분한다.
- 분리 전 인간대포 씬: 호스트 세션과 기존 캠페인 API로 공연 진입, AddCheer 수락 후 C1 10/100 확인. 기존 벽 Arm API 호출 후 C2 2:28, 미가동/종료 후 —, P1 alpha0 확인. 최종 분리 구조에서는 오프라인 씬 전환·참조 연결을 확인했고 인간대포 호스트 점수/벽 실행은 재실행하지 않았다.
- 외줄 1920×1080 Game 캡처 확인: C1/C2/P1 표시, C3는 1인 제약으로 —. 촬영 중에만 개발 패널을 비활성화하고 timeScale을 0.001로 낮췄다. 저장된 씬에는 이 테스트 변경을 적용하지 않았다.
- 최종 분리 후 외줄 호스트 재검증: 공통 씬 C1 0/100·C2 8:19, 공간 씬 P1 alpha1 확인. .harness/local/shared-hud-tightrope.png. 공통 씬 C1 첫 활성화 초기화 문제를 수정하고 재확인했다.
- 실행 종료 후 timeScale1, runInBackground 원래값 false 복구. 현재 Console 오류0 확인; 최초 import의 기존 SVG 정점 한도/서드파티 데모 씬 오류 이력은 별도다.
- check-work: 구역 PASS, 문서/규칙 FAIL(전달된 인계 문서의 이름·크기 규칙). 이 경로에서 컴파일/테스트는 SKIPPED. 위 Unity 검증은 개별 실행 결과다.
- 미실행: 2~4 실제 클라이언트, 재접속·늦은 참가, 실제 A/D 입력, 불 접촉 실패, 전 코스 완주, 다른 해상도, 전체 EditMode/PlayMode 회귀. 이전 제작 Task PASS를 이번 결과로 사용하지 않는다.

## 사용자 확정 타이머 (2026-10-20)
- C2는 불이 안전 지대에 도달할 때까지의 표시다. 기존 공연 제한시간이나 성공/실패 판정은 변경하지 않는다.
- 외줄: 회차 고정 FireSettings의 경계·속도·출발 지연과 동기화된 Elapsed를 읽는다. 속도0/불 비활성/미수신은 —.
- 인간대포: Available/Armed/미종료 FireWall의 EndTime-RouteSystem.Clock. 동시에 여러 벽이 활성일 때 가장 먼저 경계에 도착하는 벽의 남은 시간. 현재 씬에서 벽이 사용 중지 상태면 —.
- 인간대포 C3는 첫 대포 포구→도착 판의 수평 축 투영 거리다. 경로 누적 이동거리나 높이를 포함하지 않는다. 외줄 전용 불/최대거리 값은 인간대포에 주입하지 않는다.
- C3 추락 표시는 이 클라이언트가 관측한 마지막 유효 위치다. 서버의 과거 추락 위치 스냅샷이 없으므로 늦은 접속 때 과거 위치는 복원하지 않는다.
- 가까운 불 경고 임계값은 기존 공개 API에 없어 임의 거리 기준을 추가하지 않았다(추격 위치 자체는 표시).

## 수정 파일과 재적용
- CommonPerformanceUI.unity: 정본 PerformanceHud의 C2/C3와 C1 정본을 UI 루트 하위에 연결. C1 기본 binder만 해당 인스턴스에서 꺼 중복 구독을 방지한다. 두 공연 .unity에는 UI 루트와 공간 binding, 외줄에는 P1 정본 인스턴스를 둔다.
- Assets/Scripts/UI/SceneIntegration/PerformanceSceneBinding.cs: 스냅샷/불/위치/균형/입력을 읽는다. 기존 BalanceGaugeView와 Canvas는 동작 중 숨기고 binding 해제 시 복원한다.
- 같은 폴더 CommonPerformanceUI.cs·CommonUiSceneLoader.cs·Editor/PerformanceSceneInstaller.cs: 공통 표시 참조, 로컬 additive 수명 관리, 최초 설치 도구. 중복 설치와 미저장 씬을 거부한다. EditorBuildSettings.asset에 공통 씬 등록.
- Tests/PlayMode/PerformanceSceneBindingPlayTests.cs 및 asmdef, Unity 생성 .meta 추가. Task-061의 범위/계획/인계 갱신, 이전 Task 문서는 History/Before-scene-integration에 보존.
- 제작 정본과 PNG/importer/9-slice는 변경 없음. 검증 중 에디터가 생성한 폰트 atlas 및 코스 프리팹 자동 변경도 원래 내용으로 되돌렸다.

## Unity에서 확인할 항목
1. 두 공연 씬의 UI 루트와 연결 컴포넌트를 확인한다. Play 시 CommonPerformanceUI 씬이 하나만 추가되고 외줄 P1은 공간 씬에 남아야 한다. 기존 개발 패널은 HUD 외형 확인 때만 Play 중 해당 표시 컴포넌트를 끈다.
2. 2~4인 공연에서 C1 개인 점수/사망 강조/상한 유지, C3 단원 위치·내 위치·추락 마커를 확인한다. 사망 단원의 원형 마커는 정본 API상 마지막 관측 위치에 남는다.
3. 외줄에서 실제 A/D 키와 위험 누적 표시, 관전 전환 시 P1 숨김을 확인한다. C2가 0이어도 UI 자체가 실패를 발생시키지 않아야 한다.
4. 인간대포에서 벽 활성/종료에 따른 C2와 P1 숨김, 수평 투영 C3가 코스 진행을 적절히 설명하는지 확인한다.
5. 1280×720/1920×1080/울트라와이드, 실제 입력 차단 여부, 재시작·재접속을 확인한다. 시작/로비/대기실 UI 적용은 다음 단계다.
