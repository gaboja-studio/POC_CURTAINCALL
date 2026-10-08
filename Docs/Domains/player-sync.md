# 플레이어 동기화 (온라인 플레이어·호스트 판정)

## 목적

실제 플레이어 프리팹(004)을 온라인으로 만든다: 접속하면 사람마다 캐릭터 생성·출발 자리 배정, 내 캐릭터만 내 입력, 위치·점프·몸 기울기 공유, 호스트가 확정하는 플레이어 상태(추락 등)와 모두 재시작, 카메라·게이지·HUD가 내 캐릭터 따르기. 이후 기능(005·011·006·008)은 이 공개 기능 위에서 기능과 동기화를 같이 만든다([결정](../../Harness/Project/Decisions/feature-with-network.md)).
권한 구조: 이동·균형 계산은 소유자(내 컴퓨터), 상태 판정은 호스트([멀티플레이 구성](../../Harness/Project/Decisions/multiplayer-stack.md)).

## 담당 파일 (2026-10-02 확인, Task-20260930-007)

`Assets/Scripts/Systems/Network/PlayerSync/`
- `NetworkPlayer.cs` — **공개 진입점**. 플레이어 프리팹 루트(NetworkBehaviour). 자리 번호, 소유자 처리, 점프·균형 표시값 공유, 상태·재시작.
- `PlayerState.cs` — 호스트가 확정하는 상태 enum(`Normal`, `Fallen`, `Arrived`(외줄 도착 완료, 005 2026-10-03)). 끝에만 추가한다(숫자로 전송).
- `PlayerSpawnPoints.cs` — 씬의 출발 위치 목록(자리 번호 순). 없으면 줄 간격으로 나란히.
- `LocalPlayerCamera.cs` — Cinemachine 카메라가 내 캐릭터를 따라감.
- `LocalPlayerViews.cs` — 균형 게이지·테스트 HUD 대상을 내 캐릭터로, HUD R → 모두 재시작 요청.
- `TestLaneLanding.cs` — **테스트 전용**: 줄 밖 착지 → 즉시 추락. 011이 코스 조회 기반 착지 판정으로 대체 후 삭제.
- (`TestRoundRestart.cs`는 005 묘기 진행 `TightropeRun`으로 대체되어 2026-10-03 삭제)

에셋
- `Assets/Resources/Prefabs/Characters/Players/Player.prefab` — 루트에 NetworkObject·NetworkTransform(Owner, 위치 xyz·회전 y)·NetworkPlayer.
- `Assets/Resources/Prefabs/Controllers/Network/NetworkManager.prefab` — `PlayerPrefab` = Player. `DefaultNetworkPrefabs.asset`에 등록.
- `Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab` — CinemachineCamera + Follow(뒤 4.5·위 2.6) + RotationComposer(1.2 주시) + LocalPlayerCamera. 메인 카메라에 CinemachineBrain 필요.
- 테스트 씬: `Assets/Scenes/Tests/Systems/NetworkPlayerSync/NetworkPlayerSync.unity` (SessionUI, PlayerSpawnPoints, LaneGuides, 게이지·HUD, 테스트 부품).

## 진입점

```csharp
NetworkPlayer.Local; NetworkPlayer.All;                // 내 캐릭터 / 생성된 모두
NetworkPlayer.LocalSpawned += p => ...; LocalDespawned += ...;
player.Slot; player.IsLocal; player.SlotChanged += ...;
player.CurrentJump; player.IsAirborne; player.JumpChanged += ...;   // 모든 화면에서 같음

player.State; player.StateChanged += (prev, next) => ...;           // 호스트 확정 상태
player.RequestState(PlayerState.Fallen);                            // 소유자 → 호스트 요청
NetworkPlayer.ServerCanChangeState = (p, next) => 허용?;            // 호스트 규칙(조건 추가)
player.ServerSetState(next);                                        // 호스트 직접
NetworkPlayer.ServerRestartAll(); NetworkPlayer.Restarted += p => ...;  // 호스트: 모두 재시작
```

- 추락 흐름: 소유자 `PlayerBalance.Fell`(균형 무너짐·`ForceFall`) → `RequestState(Fallen)` → 호스트 확정 → 소유자 조작 잠금, 다른 화면 래그돌. 소유자 래그돌은 신호 즉시.
- 모두 재시작: 상태 Normal, 소유자 균형 초기화·출발점 순간이동·조작 해제, 다른 화면 래그돌 복구.

## 의존 도메인

- [플레이어 조작](player-control.md) — 동작 부품(`PlayerMover`·`PlayerBalance`·`PlayerRagdoll`)을 끄고 켜고 값을 넣는다.
- [네트워크 세션](network-session.md) — 방 접속·접속 승인 시 플레이어 프리팹 자동 생성.
- 사용하는 쪽: [외줄 코스·진행](tightrope-course.md)(005 — `ServerCanChangeState` 규칙, `ServerRestartAll`, `Arrived`). planned: 외줄 위 캐릭터(011), 톱날(012), 목마(006), 도구(008).

## 수정 주의점

- 남의 캐릭터는 `PlayerInputReader`·`PlayerController`·`PlayerBalance`가 꺼지고 `PlayerMover.Simulated=false`다. 남의 캐릭터에 동작 부품 함수를 불러도 화면에 반영되지 않는다. 상태 변경은 `RequestState`/호스트 함수로만.
- 래그돌은 상태만 공유하고 연출은 각자 화면이다(물리 결과 비동기, 기본값). 맞추려면 결정 요청.
- 호스트가 추락을 거절하면 소유자 화면만 래그돌인 채로 남는다(지금은 거절 규칙 없음). 거절 규칙을 붙일 때 복구를 함께 처리한다.
- 진행 중 참가는 세션이 거절하므로 늦게 들어온 화면의 래그돌 상태 맞춤은 최소 처리(생성 시 `ApplyState`)만 있다.
- 테스트 전용 부품(`Test*`)은 실제 씬에 넣지 않는다. 남은 것은 `TestLaneLanding`(011이 대체).
- 테스트 HUD의 R은 외줄 테스트 씬에서 `TightropeRunDebug`가 "호스트만 묘기 재시작"으로 덮어쓴다(`LocalPlayerViews` 연결은 그대로).
- 저장 검사가 MPPM 가상 플레이어를 에디터로 세어 멈춘다. 저장 전 가상 플레이어를 끈다(Task-007 handoff).
