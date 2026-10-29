# C16 커튼 전환
2026-10-21

- Assets/Resources/Prefabs/UIs/TransitionCurtain/TransitionCurtain.prefab 인스턴스를 PerformanceTransitionUI/UI/C16TransitionCurtainCanvas에 배치. 정본·PNG·9-slice 유지.
- SceneCurtainTransition은 공통 전환 씬을 필요시 로드하고 화면 차폐/입력 차단/열림을 관리한다.
- SpaceLinkDirector: 로드/언로드 전에 FullyCovered를 기다린다. NGO 로드 완료 후 공통 UI가 준비되면 커튼을 연다. 기존 성공/실패·씬 선택 규칙 유지.
- StartLobbyFlow: 호스트 Start→Lobby, 방 이탈→Start에 동일 연출. DevStartLauncher는 커튼 열림까지 기다린 뒤 진입 완료.
- 공연 결과 대기 후 커튼 닫기, 기존 자동 이동, 목적지 준비 뒤 열기. 기존 검은 Canvas와 IMGUI 가림은 일반 경로에서 사용하지 않는다.
- 참가자 NGO Load/LoadEventCompleted에 로컬 커튼 연결. 호스트가 모든 참가자의 Covered 응답을 수집하는 네트워크 장벽은 아직 없음. 빠른 원격 로딩에서 차폐 이전 씬 변화 가능성은 다인 검증 필요.
- 렌더링 불가 시 4초 차폐 대기 상한, 45초 입력 복구. 서버/화면 없는 실행에서 렌더링 보장을 주장하지 않는다.

## 확인
Dev로 두 공연을 각각 시작 → 1인 C12 한 행/시간/거리 → 커튼 완전 닫힘 → 대기실 → 열림/입력 복구.
Start→Lobby/이탈, 실제2~4인/재접속/느린 로딩/다른 화면비는 사용자 확인 필요.

## 변경 파일
- SceneIntegration/ResultsData.cs·ResultsPanelView.cs: 정본 소스 기반 1~4인 표시 어댑터.
- SceneIntegration/SceneCurtainTransition.cs, PerformanceEndingPresentation.cs, Editor/PerformanceResultsInstaller.cs, Tests/PlayMode/PerformanceResultsPlayTests.cs 및 신규 meta.
- Systems/SpaceLink/SpaceLinkDirector.cs, Systems/StartLobby/StartLobbyFlow.cs, Systems/DevMode/DevStartLauncher.cs.
- Assets/Scenes/UI/PerformanceTransitionUI.unity와 현재 Task/관련 가이드.

## 검증 결과
- 컴파일 PASS. 실제 Tightrope/HumanCannon을 한 테스트에서1인 C12 한 행·커튼 FullyCovered·대기실 복귀·열림 검증1/1 PASS(c16-play.json). 합성 API 종료이며 사람 완주 검증 아님.
- 구역/문서 PASS. Start/Lobby 동선·실제 다인·Mac·빌드 미실행.

## dev 배경 통합
- 2026-10-21 origin/dev 8fa9ba3 → 로컬 병합0eb9140. 원본 배경 자동 로더/배치 규칙 유지.
- 커튼 열림은 Common UI와 PerformanceBackgroundLoader.IsTransitioning 완료를 모두 기다린다. 외줄/인간대포 진입 배경 로드 및 대기실 복귀 배경 해제 테스트 추가.
- 기존 UI와 사용자 폰트/재질 변경은 stash에서 복원. 보관 stash는 삭제하지 않았다. 원격 push 없음.
- 통합 후 컴파일 PASS. 실제 두 공연의 배경 로드, 1인 C12, 커튼 차폐, 대기실 복귀 시 배경 해제·열림을 PlayMode 1/1 PASS(34.02초)로 확인. 사람 완주·다인·Start/Lobby 동선은 미실행.

## Start → Lobby 카메라 소실 수정
- 후속 F/안내: LobbyRoom에도 남아 있던 sceneCount==1 조건을 SpaceLinkHub.HasLoadedSpace 공유 검사로 교체. CommonInteractionUI는 LobbyRoom.FocusedInteraction을 C6 대상으로 사용하며 원본 [F] 접두사는 키 배지와 중복되지 않게 제거한다. 차단된 레버는 기존 조건 안내를 표시하고, 메뉴/전환 입력 차단 중에는 대상과 안내를 해제한다.
- F 회귀 확장: Input System에 F 누름/뗌을 주입해 서명/취소/재서명과 레버 Intro 시작, C6 표시, Start 복귀 1/1 PASS(9.89초). LobbyReadyBoard는 종료 중 수신자 없는 송신을 생략한다. 물리 키보드/Relay/다인 미실행.
- 재현: Lobby와 CommonInteractionUI가 로드된 상태에서 Main Camera의 enabled=true, activeInHierarchy=false, 렌더 카메라0. 커튼은 이미 Open인데 마지막 프레임이 화면에 남았다.
- SpaceLinkHub가 sceneCount>1을 게임 공간 진입으로 판단하던 것을 SpaceRoute에 등록된 실제 공간 씬 검사로 교체. 공통 UI·커튼·공연장 배경만으로 로비 카메라와 UI를 끄지 않는다.
- 사용자 확인: Start에서 방 열기 → 커튼 열림 뒤 로비 배경/단원/버튼 표시 → 방 나가기 → Start 복귀. LAN 호스트 자동 검증과 별개로 Relay 접속·다중 클라이언트는 미실행.
- 검증: 컴파일·구역·문서 PASS. StartLobbyCurtain_KeepsLobbyCameraAndUi_WithAdditiveUi 1/1 PASS(10.1초), 실제 Start→LAN 호스트 Lobby→Start. 로비 카메라·Canvas 활성, 커튼 종료·입력 복구 및 캡처 확인.
