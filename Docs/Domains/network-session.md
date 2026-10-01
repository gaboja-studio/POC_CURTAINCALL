# 네트워크 세션 (방 만들기·게임 상태)

## 목적

방을 열고 참가하는 접속과, 모두가 같게 보는 게임 진행 상태(대기/진행/종료)를 담당한다. 플레이어·오브젝트 동기화(007·008)와 외줄·목마 연결(009·010)은 이 도메인의 공개 진입점 위에서 만든다. 스택·권한 결정은 [멀티플레이 구성](../../Harness/Project/Decisions/multiplayer-stack.md).

## 담당 파일 (2026-10-01 확인, Task-20260930-003)

- `Assets/Scripts/Network/Session/NetworkSessionManager.cs` — **유일한 공개 진입점**. NetworkManager 프리팹에 붙는다.
- `Assets/Scripts/Network/Session/ISessionConnector.cs` — 접속 방식 인터페이스(방 열기·참가·나가기, `JoinKey`).
- `Assets/Scripts/Network/Session/ServicesSessionConnector.cs` — Multiplayer Services 세션(Relay). 비공개 세션, 방 코드로 참가.
- `Assets/Scripts/Network/Session/LanSessionConnector.cs` — 직접 IP(LAN) 테스트 경로. 기본 포트 7777.
- `Assets/Scripts/Network/Session/GameSessionState.cs`, `SessionConnectionState.cs` — 게임 상태 / 내 접속 상태 enum.
- `Assets/Scripts/Network/Session/GameSessionStateSync.cs` — 게임 상태·인원 복제용 NetworkBehaviour(내부용).
- `Assets/Scripts/Network/Session/NetworkSessionTestUI.cs` — 테스트 씬 전용 IMGUI. 진짜 로비 UI가 생기면 삭제.
- `Assets/Resources/Prefabs/Controllers/Network/NetworkManager.prefab` — NetworkManager + UnityTransport + NetworkSessionManager(Required Players 기본 4).
- `Assets/Resources/Prefabs/Controllers/Network/GameSession.prefab` — NetworkObject + GameSessionStateSync. 호스트가 방을 열 때 스폰.
- `Assets/Resources/Prefabs/Controllers/Network/DefaultNetworkPrefabs.asset` — 네트워크 프리팹 목록.
- 테스트 씬: `Assets/Scenes/Tests/NetworkSession/NetworkSession.unity` (`SessionUI` 오브젝트).

## 진입점

```csharp
var session = NetworkSessionManager.EnsureExists();   // 씬에 없으면 Resources 프리팹 생성

await session.HostSessionAsync();          // 방 만들기 → session.JoinKey = 방 코드
await session.JoinSessionAsync(code);      // 방 코드로 참가
await session.HostLanAsync() / JoinLanAsync("IP:포트");  // LAN 테스트
await session.LeaveAsync();                // 나가기(호스트면 방 닫기)
session.EndGame();                         // 호스트 전용: 진행 → 종료

session.GameStateChanged += state => { ... };   // Waiting / Playing / Ended
session.PlayerCountChanged += count => { ... };
session.ConnectionStateChanged += state => { ... };
session.Disconnected += reason => { ... };      // 종료 이유 문구
```

- 읽기 값: `GameState`, `PlayerCount`, `RequiredPlayers`, `ConnectionState`, `IsHost`, `JoinKey`, `LastDisconnectReason`, `NetworkManager`, `ActiveConnector`.
- 이벤트는 **값이 바뀔 때만** 온다. 구독할 때 현재 값(`GameState` 등)을 먼저 한 번 읽는다.

## 현재 동작 (구현 사실)

- 4명(`RequiredPlayers`)이 모이면 호스트가 즉시 `Playing`으로 바꾸고 세션을 잠근다.
- 진행 중 클라이언트가 나가면 남은 인원으로 계속하고, 진행 중 참가는 접속 승인에서 거절한다. 인원이 차면 "방이 가득 찼습니다."로 거절한다.
- 호스트가 나가면 클라이언트는 연결이 끊기고, 진행 중이었으면 로컬 `GameState`가 `Ended`가 된다.
- 익명 로그인은 실행마다 다른 프로필을 쓴다(같은 PC의 MPPM 플레이어 구분).

## 의존 도메인

- [Unity Assets 구조](assets-structure.md) — 폴더·Resources 배치.
- [플레이어 조작](player-control.md) — 007이 실제 플레이어 프리팹에 온라인을 붙인다.
- 사용하는 쪽(planned): 플레이어 동기화(007), 이후 기능 작업 005·011·006·008(기능+동기화, [결정](../../Harness/Project/Decisions/feature-with-network.md)), 최종 씬(009)·목마 예외(010).

## 수정 주의점

- `NetworkConfig.ConnectionApproval`과 `ConnectionApprovalCallback`은 이 매니저가 소유한다. 다른 코드가 콜백을 등록하면 NGO가 예외를 던진다. 승인 조건 추가는 이 도메인에 요청한다.
- 게임 상태는 **호스트만** 바꾼다. 클라이언트는 이벤트로만 받는다. `GameSessionStateSync`를 직접 쓰지 않는다.
- 플레이어 프리팹(`NetworkConfig.PlayerPrefab`) 지정은 NetworkManager 프리팹 수정이다. 공용 파일 담당 배정을 받은 뒤 한다(2026-10-02 007에 배정). 지정되면 접속 승인 시 자동 생성된다.
- 새 네트워크 프리팹은 `DefaultNetworkPrefabs`에 등록한다(보통 Unity가 자동 등록). 프리팹을 Resources 경로에서 옮기면 `NetworkSessionManager`의 경로 상수도 고친다.
- 미구현: 씬 전환(NGO SceneManager), 재시작 후 대기 복귀, 로비 검색(`QuerySessionsAsync`는 별도 인터페이스로 추가 예정), 호스트 이전.
