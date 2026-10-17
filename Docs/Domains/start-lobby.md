# 시작·로비 씬 (게임 첫 화면·로비·런 시작/복귀)

## 목적

게임을 켜면 보는 시작 씬과 방에 모여 출발을 기다리는 로비 씬. 흐름은 시작 씬 → (방 만들기·참가) → 로비 씬 → (게임 시작) 런 → (엔딩·런 실패 도입) 로비 씬. 로비 씬은 장소 전환의 시작 씬(옛 SpaceLink 허브 자리)이다. 화면 구성 결정은 Integration-start-lobby 기록(2026-10-16 PM): 메뉴·설정은 핵심만, 로비는 고정 3D 시점, 준비는 출연 명단 서명 + 방장 커튼 레버.

## 담당 파일 (2026-10-16 확인, Task-20261016-052)

- 씬: `Assets/Scenes/Start_Lobby/Start.unity`(빌드 0번), `Assets/Scenes/Start_Lobby/Lobby.unity`(빌드 1번), 재질 `Assets/Scenes/Start_Lobby/Display/`.
- 씬은 **빌더가 통째로 짓는다**: `Assets/Scripts/Systems/StartLobby/Editor/StartLobbySceneBuilder.cs`(메뉴 `Tools/CurtainCall/Build Start·Lobby Scenes`), 부품 `StartLobbyKit.cs`. 씬을 손으로 고치면 다음 빌드 때 사라진다.
- 코드 `Assets/Scripts/Systems/StartLobby/`:
  - `StartLobbyFlow.cs` — 큰 흐름(씬이 바뀌어도 유지). 호스트가 방을 열면 로비로(NGO 씬 전환), 접속이 끊기면 어디서든 시작 씬으로. 로비 씬 이름 = 공통 세팅 장소 전환의 `HubScene`.
  - `StartMenu.cs` — 시작 씬 메뉴(방 만들기 · 방 코드 참가 · LAN · 설정 · 종료).
  - `LobbyRoom.cs` — 로비 진행: 내 캐릭터 출발 위치·균형 끄기, 가까운 물건 F 안내, 게임 시작 뒤 커튼 열기 → 호스트가 `introSeconds` 뒤 `CampaignRun.EndIntro()`, HUD·ESC 메뉴.
  - `LobbyInteractable.cs`(가까이 가서 F로 쓰는 물건), `LobbyReadyBoard.cs`(출연 명단 = 준비, 호스트 판정·이름 메시지 동기화), `LobbyCurtainLever.cs`(방장 레버: 시작 / 엔딩 뒤 `ReturnToLobby`).
  - `LobbyCamera.cs`(고정 방향, 내 캐릭터 쪽으로 조금만 따라가고 범위 밖으로 안 나감), `ClientSettings.cs`·`SettingsPanel.cs`(이 PC 설정: 전체 음량·화면 모드, PlayerPrefs).

## 진입점

```csharp
StartLobbyFlow.EnsureExists();          // 시작·로비 씬이 자동으로 만든다
StartLobbyFlow.Instance.LeaveToStart(); // 방 나가기 → 시작 씬
StartLobbyFlow.LobbyScene;              // 로비 씬 이름(장소 전환 세팅의 시작 씬)
```

- 로비 물건 추가: `LobbyInteractable`을 상속해 `Prompt`·`Use`를 구현하고, 빌더에서 `LobbyRoom.interactables`에 넣는다.
- 명단 메시지 이름: `CurtainCall.Lobby.ReadyState`·`ReadyToggle`·`ReadyRequest`(`CustomMessagingManager`).

## 현재 동작 (구현 사실)

- 서명은 `Waiting`에서만 바뀌고, 게임 상태가 바뀌면(시작·로비 복귀) 호스트가 명단을 비운다. 나간 사람 서명은 호스트가 지운다.
- 레버: 방장만, 최소 인원(`MinPlayers`)·전원 서명일 때 `StartGame()`. 엔딩 뒤(`Ended`)에는 `ReturnToLobby()` + 전원 몸 복구.
- 런 실패로 새 세대 도입이 오면 로비로 돌아와 커튼이 열린 뒤 다시 첫 공연으로 간다.
- 로비 맵은 바닥 10×8m, 보이지 않는 벽으로 막는다. 출발 위치 4곳(자리 번호 순).

## 의존 도메인

- [네트워크 세션](network-session.md) — 방 열기·참가·나가기, `StartGame`·`ReturnToLobby`.
- [공통 게임 시스템](common-game-systems.md) — `CampaignRun`(도입·엔딩), SpaceLink 장소 전환·`SpaceLinkHub`(추가 로드 때 로비 화면 끄기).
- [플레이어 동기화](player-sync.md) — `NetworkPlayer.Local`·`Slot`·`PlayerSpawnPoints`.

## 수정 주의점

- 로비 환경(출발 위치 포함)은 `SpaceLinkHub`가 전환 중 껐다 켠다. 복귀 위치는 활성화 뒤 LateUpdate에서 적용한다([함정](../../Harness/Project/Pitfalls/hub-spawn-before-environment-enable.md)).
- 균형은 외줄 전용이라 로비에서 끈다(함정 `balance-on-outside-tightrope`).
- 새 한글 문구를 넣으면 빌더가 프로젝트 한글 Static 폰트를 다시 굽는다(함정 `tmp-dynamic-font-churn`).
- 미구현: 도입 컷씬(지금은 커튼 열림 3초), 단원 이름(지금 "단원 N"), 로비 검색, 마우스 감도 설정.
