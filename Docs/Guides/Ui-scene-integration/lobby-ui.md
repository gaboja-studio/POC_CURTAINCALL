# 로비 UI
- 이전 Start·팝업·화면 Canvas 변경은2be6f00 commit/push 완료. 이번 로비 변경은 미커밋.
- Lobby.unity/UI에 Assets 완성 LobbyHud, Guides, SystemBanner, Toast 프리팹 인스턴스. 원본 그래픽/9-slice 유지,1920×1080 Expand.
- LobbyHudBinding: NetworkSessionManager의 JoinKey/PlayerCount/RequiredPlayers, NetworkPlayer의 호스트 슬롯, LobbyReadyBoard의 서명 수/로컬 준비, LobbyCurtainLever의 권한/시작 조건, CampaignRun의 도입/엔딩 연결.
- 방 코드/LAN 주소는 기본 가림, 눈 버튼으로 보기, 복사 요청은 실제 클립보드와 성공 토스트 연결. 호스트 이름 API가 없으므로 기존 단원 슬롯명 사용.
- 준비 배너는 실제 서명 인원·레버 조건 표시. F 안내는 가까운 물체가 있을 때 공통 C6에 양보. ESC/입력 차단 시 안내와 코드 버튼 차단, 공간 전환 시 UI 루트 숨김.
- 구형 Lobby Canvas의 Room Info/Copy/Prompt/Banner/Hint/Goal을 제거. 실제 월드 출연 명단·서명 행·레버 표시는 아직 대체되지 않은 기능이므로 유지. F 서명/레버 판정 및 네트워크 권한은 그대로.
- 수정: Lobby.unity, Systems/StartLobby/LobbyRoom.cs. 신규: SceneIntegration/LobbyHudBinding.cs, Editor/LobbyHudInstaller.cs, Tests/PlayMode/LobbyHudPlayTests.cs 및 meta.

## 시작 순간이동
- 원인: Intro가 현재 Hub 전환을 요청하면서 IsTransitioning이 잠깐 켜짐. LobbyRoom이 화면 복구를 실제 로비 복귀로 오인해 Place를 재호출.
- 수정: 실제 다른 공간이 로드된 것을 관찰한 뒤 로비에 돌아온 경우에만 재배치. 최초 입장 배치·공연 출발 위치는 유지.

## 검증
- 컴파일 PASS: recompile_status completed/failed=false.
- 실제 LAN PlayMode1/1 PASS(9.49초): Start→LAN host→Lobby, 코드 기본 가림/보기/복사, 서명, 레버 시작, 시작 후 수평 위치 차이0.25m 미만, 구형 Room Info 제거.
- 게임 시작 순간이동은 실제 호스트1인 Dev에서 회귀 확인. 테스트는 기존 API 호출 기반으로 키·마우스 실제 입력 검증과 구분.
- 미실행: 원격2~4인, 실제 F/눈/복사/ESC 포인터 입력, 다중 화면비 시각 검증, 게임 전체 완주 후 로비 복귀, Mac/빌드.
- 사용자 확인: Start에서 방 생성→로비 눈/복사, 명단 근처 F 서명/취소, 전원 준비 후 방장 레버 F. 현재 위치에서 도입이 시작되는지, 공연 진입 후 로비 HUD가 남지 않는지, ESC 메뉴 클릭 확인.
