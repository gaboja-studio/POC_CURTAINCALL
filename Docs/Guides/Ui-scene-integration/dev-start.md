# Dev 시작 지점

Start → Lobby를 반복하지 않고 일차·콘텐츠·시간대를 지정해 실제 게임 흐름으로 진입한다. 새 씬이나 원본 UI 프리팹 수정 없이 기존 Dev 배지에 연결한다.

## Unity 사용법
1. Play를 종료한 상태에서 `Tools > CurtainCall > Dev Mode (1인 테스트)`를 켠다.
2. `WaitingRoomBase`, `Tightrope`, `HumanCannon` 등 원하는 씬을 열고 Play한다. Start 씬에서 시작해도 사용할 수 있다.
3. Game 뷰를 클릭하고 **Mac: ⌘+Shift+U / Windows: Ctrl+Shift+U**를 누르거나 화면 위 **DEV MODE · 시작 지점** 버튼을 누른다. 기존 **F8**도 사용할 수 있다.
4. 일차, 공연/아침/공연 사이/밤 정비/탈출로, 공연 순번과 콘텐츠를 선택한다(탈출로는 콘텐츠 선택 없음). **선택 지점에서 새 테스트 시작**을 누른다.
5. 로딩 후 창이 닫히면 조작한다. 같은 단축키로 다시 열어 다른 지점 또는 같은 지점을 재시작할 수 있다.

예: `2일차 / 공연 / 2번째 / 인간대포`는 해당 공연으로, `3일차 / 밤 정비`는 대기실의 밤으로 바로 진입한다. 아침·공연 사이에서 고르는 콘텐츠는 다음 공연이다. 1일차 공연 사이에는 기존 가이드 쉬는 시간 규칙을 쓴다.

## 범위와 초기화
- 기존 Dev 스위치 사용. 꺼져 있으면 창·진입 기능 비활성. 빌드는 기존 `-devmode` 실행 인자를 사용한다.
- 미접속이면 LAN 호스트를 만들고 실제 NetworkPlayer를 준비한다. 이미 접속 중이면 호스트만 실행할 수 있다. 연결 중/클라이언트/씬 전환 중 요청은 거부한다.
- 매번 새 테스트 세대로 시작한다. 이전 세대의 자금·소지품·부상·정산 결과·준비 완료는 초기화된다. 이전 일차를 실제로 플레이한 보상은 합성하지 않는다.
- 동일 공연도 공간 씬을 다시 로드해 코스·불·대포·공연 어댑터를 초기화한다. 접속한 팀원도 함께 전환한다.
- 선택한 공연 이후는 기존 캠페인 규칙대로 진행한다. 건너뛴 밤의 미작성 세트리스트는 테스트 런에만 기본 편성(외줄/인간대포)을 넣는다.
- 현재 설정은 공연 3일, 하루 2회다. 4일차는 마지막 아침·탈출로만 선택 가능하며 1일차 아침은 없다. 백스테이지 바로 시작은 제공하지 않는다.
- 탈출로(Task-089): 쉬는 시간에서 정상 탈출 개시(`StartEscape`)를 거쳐 `escape_route`를 바로 불러온다. 대기실 합류·로딩 이동(`EscapeSceneTransfer`)은 건너뛰므로 탈출로는 직접 개발 모드로 동작한다. 각 화면이 자기 캐릭터를 탈출로 출발점(`PlayerSpawnPoints`)에 세우고, 모두 쓰러지면 1초 뒤 출발점에서 다시 시작한다(`EscapeDevelopmentSession`). 출구 도착에 따른 탈출 성공·엔딩 판정은 캠페인 합류 경로에서만 동작한다.

## 테스트 치트 창 (F9, Task-089)
Dev 모드에서 **F9** 또는 화면 위 **치트 [F9]** 버튼으로 연다. 열려 있는 동안 게임 입력은 막히고 커서가 풀린다.
- 위치 이동: 씬 출발점, 외줄(출발·구간별 시작·도착), 인간 대포(각 대포 탑승 위치·최종 목적지). 대포가 있는 탈출로에도 대포 지점이 나온다. 앞으로 3m·위로 2m. 재시작 위치는 바꾸지 않는다(`PlayerMover.Relocate`).
- 생존: 추락 무시(균형 붕괴·줄 밖 착지·대포 추락 착지·목마·매달림 낙하. 톱·화염 직접 피해는 그대로), 균형 중앙으로, 모두 되살리기(호스트), 모두 재시작.
- 사망 판정은 호스트가 하므로 추락 무시는 호스트 화면에서 켜야 모두에게 적용된다. 치트 값은 Play마다 꺼진 상태로 시작한다.
- 지점 추가: 콘텐츠 쪽에서 `DevCheats.RegisterTeleportSource`로 공급자를 붙인다(예: `TightropeDevTeleports`, `HumanCannonDevTeleports`).

## 연결 파일
- `Assets/Scripts/Systems/DevMode/DevStartLauncher.cs`, `DevModeBadge.cs`: 창, 입력 잠금, 호스트 준비, 진입 요청.
- `Assets/Scripts/Systems/DevMode/DevCheats.cs`, `DevCheatPanel.cs`: 치트 상태·순간이동, F9 창. 지점 공급자 `MainStage/Tightrope/Rope/TightropeDevTeleports.cs`, `MainStage/HumanCannon/HumanCannonDevTeleports.cs`.
- `Assets/Scripts/EscapeRoute/EscapeDevelopmentSession.cs`: Dev 탈출로 진입 시 출발점 배치·복구, 추락 무시 치트 연결.
- `Assets/Scripts/Systems/Campaign/RunStartPoint.cs`, `RunState.cs`, `CampaignRun.cs`: 유효한 시작 상태 생성, 세대 초기화, 호스트 권한과 기존 상태 전송.
- `Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs`: Dev 요청의 같은 공간 재로딩.
- `Assets/Scripts/UI/SceneIntegration/CommonUiSceneLoader.cs`: 기존 UI 로드를 마치고 Dev 공간 전환 동안 새 UI 로드를 보류하는 수명 토큰.
- `Assets/Scripts/UI/SceneIntegration/Editor/DevStartPointTests.cs`, `Tests/PlayMode/DevStartLauncherPlayTests.cs`: 상태 및 실제 씬 진입 회귀.

## 검증
- 2026-10-21 Mac 단축키 보완: 컴파일 PASS, Windows 에디터에서 Ctrl+Shift+U 열림/Meta(Command)+Shift+U 닫힘 및 입력 잠금 해제 합성 검사 PASS. 실제 Mac 키보드·에디터 검증은 미실행.
- Unity 6000.3.25f1 컴파일 PASS. EditMode 14/14, UI PlayMode 27/27 PASS.
- SpaceLink 기본6/6 PASS. 넓은 namespace 필터로 함께 실행된 검사 전체는 25개 중19 PASS/6 FAIL. 구형 `PerformanceRouteTests`가 현재 `Tightrope` 대신 `proto_0.0.2`를 기대한 실패1개, `PerformanceLinkPlayTests`의 정산/도착/다음 시간대 timeout5개다. 이 확장 검사는 통과로 간주하지 않으며 이번 Dev 경로 밖의 별도 조사 대상으로 남긴다.
- 실제 1인 LAN 호스트: WaitingRoomBase 직접 Play → 2일차 두 번째 인간대포 → 같은 공연 재시작(씬 handle 변경·부상 제거·자금0) → 3일차 외줄 → 3일차 밤 → 4일차 마지막 아침. 각 진입에 공통 HUD/상호작용 UI가 각각1개, 조작 잠금 해제 확인.
- Dev 꺼짐/잘못된 지점 거부, 창 열림/닫힘 입력 잠금 확인. Client/Connecting 거부는 상태 주입 검사이며 실제 다중 클라이언트 검증은 아니다.
- 직접 Play에서 2일차 아침(다음 HumanCannon), 출구 전 대기 타이머와 입력 복귀 확인. 1920×1080 창/대기실 화면, F8 합성 입력으로 창 열림/닫힘 확인. Dev 배지는 시계 위 여백으로 옮겼으며 창은 불투명 배경이다. 최종 배치도 캡처 확인했다.
- 최초 PlayMode는 로딩 중 CommonInteractionUI를 NGO가 초기 씬 목록으로 잡아 Single 전환에서 예외 발생. 진행 중 UI 작업 완료 대기 + 전환 중 새 작업 보류 후 위 재검사 통과.
- 종합 check-work는 기존 병합/Task 중첩 구역 FAIL431에서 후속 단계 SKIPPED. 개별 fast OK(경고10/hard0), diff-check PASS. 컴파일/테스트는 위 개별 실행 결과다.
- 미실행: 실제 2~4인·재접속, 빌드 실행 인자, 실제 키보드 장시간 플레이, 여러 해상도·성능 프로파일. 원본 프리팹·PNG·9-slice·씬 파일은 이번 Dev 작업에서 변경하지 않았다. 기존 폰트1/재질3 변경은 별도 보존 중이다.
