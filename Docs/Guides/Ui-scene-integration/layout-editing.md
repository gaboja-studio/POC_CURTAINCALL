# 씬 UI 위치 편집과 구형 UI 정리

## 안 보였던 이유
- 공연 UI는 데이터 수신 전 노출되지 않도록 CanvasGroup alpha=0 또는 비활성 상태로 저장되어 있다. 편집기 Scene View도 이 값을 사용하므로 그대로는 보이지 않는다.
- 바늘·경계선은 데이터로 매번 위치를 계산한다. 게이지 전체 위치는 P1_Balance RectTransform에서 조절한다.

## 보면서 수정하기
1. Play를 정지하고 실제 적용 씬을 연다.
2. 외줄: UI/TightropeBalanceCanvas/P1_Balance를 선택한다. 현재 사용자 지정 Pos Y=80을 유지했다.
3. Tools → Curtain Call UI → Preview selected UI in Scene View를 실행한다. Scene 탭의 2D와 Rect Tool(T)로 조절하거나 Inspector의 Pos Y를 수정한다. 값이 작을수록 아래로 내려간다.
4. Ctrl+S로 씬을 저장한다. 위치/크기는 저장되고 임시 Alpha/활성 상태는 저장 전에 복원된다. 저장 후 미리보기는 다시 표시된다.
5. End layout preview로 끝낸다. Play 진입·씬 닫기·스크립트 재컴파일 시에도 표시 상태를 복원한다.
- 공통 UI는 CommonPerformanceUI 씬의 C2/C3/관전 등 개별 패널을 선택해 같은 메뉴를 사용한다. 결과는 PerformanceTransitionUI의 ResultCanvas/60_Panels/Menu_C13을 선택한다.
- 다른 화면인 비활성 자식은 자동으로 전부 켜지 않는다. 원하는 패널 자체를 선택한다. 검은 전환 Canvas까지 함께 켜면 화면을 덮으므로 개별 패널로 편집한다.
- 이 도구는 씬 인스턴스 편집용이며 Prefab Mode에서는 비활성이다. Prefab Apply All 없이 씬만 저장한다. 합성 게임 데이터/한글 폰트 생성은 수행하지 않는 레이아웃 미리보기다.

## 제거/유지
- Tightrope에서 구형 BalanceGauge 오브젝트, PlayerCommandDebugHud와 TightropeRunDebug 오브젝트, 이들만 연결하던 LocalPlayerViews 컴포넌트를 제거했다.
- HumanCannon에서 새 어댑터로 대체한 HumanCannonCourseSections 컴포넌트를 제거했다. 구간 경계와 새 안내는 HumanCannonUiBinding에 유지한다.
- 기존 표지의 TMP는 문구/위치 데이터 원천이므로 보존하고 중복 MeshRenderer 표시만 껐다. 새로운 WorldLabels 정본이 렌더링한다.
- 결과 Canvas의 사용하지 않는 CurtainCallMenuPanels, 확인창 트리, ActionsAndFooter를 제거했다. 결과 패널/사망 알림/검은 전환은 유지한다.
- 독립 씬 접속·공연 시작·R 재시작 등 아직 사용하는 도구는 DevelopmentTools 아래로 분리했다. 인간대포의 HumanCannonSystem/물리 장치/바닥 표시 로직은 제거하지 않는다.
- 원본 리소스 프리팹·제작 테스트 씬·Start/Lobby/WaitingRoom의 아직 대체되지 않은 UI는 유지한다.

## 수정 파일·검증
- Editor/SceneUiLayoutPreview.cs와 SceneUiCleanup.cs 추가, PerformanceUpgradeInstaller.cs는 정리 후 구간 경계를 새 어댑터에서 읽도록 보완.
- 씬: Tightrope, HumanCannon, PerformanceTransitionUI. 외줄 WarningBandAndTunedWalkAreAppliedByAutomaticBoarding 검사의 구형 게이지 의존을 새 P1로 변경.
- 미리보기 임시 씬 실행 검증: 표시, 저장 전 숨김 복원, 디스크에 숨김 유지, Y160→120 위치 저장 모두 확인. 임시 씬 제거 완료.
- 상세 최종 검증은 Task의 upgrade-verification.md에 기록한다.
