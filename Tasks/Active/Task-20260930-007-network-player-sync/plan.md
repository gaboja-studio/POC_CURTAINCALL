# Plan

## Context

004 실제 플레이어 프리팹을 온라인으로 만든다. 이후 기능 작업(005·011·006·008)이 이 결과 위에서 기능과 동기화를 같이 만든다.
결정: `Harness/Project/Decisions/feature-with-network.md`, `multiplayer-stack.md`(이동은 각자, 판정은 호스트), `player-control-architecture.md`(입력 → 조작 규칙 → 동작 부품)
도메인: `Docs/Domains/network-session.md`(방 접속), `Docs/Domains/player-control.md`(플레이어)

## 시작 시점 상태 (2026-10-02 확인)

- 007 코드 없음: `Assets/Scripts/Network/PlayerSync/` 비어 있음, 테스트 씬 `NetworkPlayerSync.unity`는 카메라·조명만.
- `Player.prefab`: NetworkObject·NetworkTransform 없음.
- `NetworkManager.prefab`: `PlayerPrefab` 비어 있음(자동 생성 옵션은 켜짐), `DefaultNetworkPrefabs.asset`에는 `GameSession`만 등록.
- 004의 혼자 기준 부분:
  - `PlayerInputReader`: 모든 플레이어가 키보드를 읽음.
  - `BalanceGaugeView`·`PlayerCommandDebugHud`: 대상이 비면 `FindAnyObjectByType`로 첫 플레이어를 잡음.
  - `PlayerBalance`: 흔들림 방향(`Random`)·추락 판정을 각자 계산, `Value` 외부 설정 불가.
  - `BalanceTiltView`·`PlayerRagdoll`: 자기 `PlayerBalance`의 값·`Fell` 신호만 따름.
  - 카메라: 따라가는 스크립트 없음(씬 고정 카메라).
- 방 접속(003): `NetworkSessionManager`(`IsHost`, 연결·게임 상태 이벤트), 4명 모이면 자동 시작, 진행 중 참가 거절.

## Approach

1. `setup.md` 작업 구역 안에서만 구현한다. 새 코드는 `Assets/Scripts/Network/PlayerSync/`에 모으고, `Assets/Scripts/Player/`는 외부에서 값·상태를 넣을 수 있게 하는 최소 변경만 한다.
2. 테스트 어셈블리는 만들지 않는다. AI는 컴파일만 검사하고, 단계마다 작업자가 MPPM 여러 명으로 직접 확인한다.
3. 단계마다 작업자와 합의 → 구현 → 컴파일 → 작업자 확인 → 다음 단계.

## 단계 (각 단계 완료 기준 = 작업자 MPPM 확인)

1. **네트워크 플레이어 생성·출발 위치**: `Player.prefab`에 NetworkObject, `NetworkManager.prefab`의 `PlayerPrefab` 지정·네트워크 프리팹 목록 등록, 접속 순서로 4개 출발 위치에 배치(위치 목록은 인스펙터 값).
   - 완료: 4명이 들어오면 캐릭터 4개가 겹치지 않고 각자 출발 위치에 선다.
2. **내 캐릭터만 내 입력 + UI·카메라**: 소유자가 아니면 입력·조작 규칙·이동 계산을 끈다. 게이지·HUD·카메라는 내 캐릭터(로컬 플레이어)를 대상으로 잡는다.
   - 카메라(2026-10-02 결정): **Cinemachine 3**(`com.unity.cinemachine` 3.1.7, 2026-10-02 PM이 통합 브랜치에 설치 d58e08e)으로 내 캐릭터 3인칭 시점. 메인 카메라에 `CinemachineBrain`, 가상 카메라는 `CinemachineCamera` + `CinemachineFollow`(뒤·위 거리) + `CinemachineRotationComposer`(가슴 높이 주시). 화면에 내 줄과 양쪽 옆줄(줄 간격 2.0m)이 보이는 정도. 거리·높이·FOV는 인스펙터 값. `PlayerSync/`의 스크립트가 접속 후 로컬 플레이어를 Follow/LookAt에 넣는다. 리그는 프리팹 `Assets/Resources/Prefabs/Controllers/Camera/`(2026-10-02 PM 배정)로 만들어 이후 Task가 씬에 놓기만 하게 한다. 고도화(흔들림·목마 시 줌 등)는 이후 Cinemachine 설정으로.
   - 완료: 각 창에서 자기 캐릭터만 움직이고, 게이지가 자기 캐릭터 값을 보이며, 카메라가 자기 캐릭터를 뒤에서 따라가고 양쪽 옆줄이 보인다.
3. **위치·방향·점프 공유**: 소유자 권한 이동(NetworkTransform 소유자 모드). 점프 종류·공중 여부도 공유. 보간 값은 인스펙터.
   - 완료: 다른 사람의 이동·점프·옆줄 점프가 끊김 없이 보인다.
4. **자세(기울기) 공유**: 소유자가 균형 값을 보내고, 다른 사람은 기울기 표시만 한다(흔들림 계산은 소유자만).
   - 완료: 한 사람의 몸 기울기가 모두에게 같은 방향으로 보인다.
5. **호스트 판정 통로 + 추락 상태 공유**: "요청 → 호스트 판정 → 결과 공유" 공용 장치와 확장 가능한 플레이어 상태(첫 값: 보통·추락). 첫 사용처는 추락(소유자 균형 무너짐·줄이 아닌 곳 착지 → 호스트 확정 → 모두에게 추락, 추락자 조작 잠금). 줄 밖 착지 판정은 지금 테스트용 `TestLaneLanding`(로컬 즉시 추락)이고, 실제 줄 정보는 005 코스가 대체한다.
   - 모두 재시작(2026-10-02 @MoHoDu): 재시작은 개인이 아니라 전원 추락 시 모두 같이 출발점에서(`tightrope-rules.md`). 007은 호스트가 부르는 "모두 재시작" 공유(출발점 순간이동·균형 초기화·래그돌 복구·조작 잠금 해제)를 제공하고, 언제 부를지는 005가 정한다. 테스트 씬은 임시로 호스트가 전원 추락 3초(인스펙터) 후 재시작, R은 "모두 재시작 요청"(004 테스트 씬은 그대로).
   - 완료: 추락한 사람이 모두에게 추락으로 보이고 조작이 잠긴다. 전원 추락 후 모두 같이 출발점에서 다시 시작한다. 이후 목마·도구·묘기 진행이 같은 통로를 쓸 수 있다.
6. **추락 래그돌 연출**: 추락 상태를 받으면 각자 화면에서 래그돌(물리 결과는 맞추지 않음, 기본값).
   - 완료: 추락 래그돌이 모두에게 보인다.
7. 마무리: 공개 기능 정리, `Docs/Domains/` 플레이어 동기화 지도 추가, `player-control.md` 주의점 갱신.

## Files

- 신규: `Assets/Scripts/Network/PlayerSync/`(소유자 처리·출발 위치·상태 공유·판정 통로), 테스트 씬 `Assets/Scenes/Tests/NetworkPlayerSync/` 배치(작업자 확인 후)
- 이어받기 최소 변경 후보: `PlayerBalance`(외부 값·추락 상태 적용), `BalanceGaugeView`·`PlayerCommandDebugHud`(대상 지정), `PlayerRagdoll`(외부 추락 신호), `PlayerMover`(출발 위치 지정)
- 공용 파일: `Player.prefab`, `BalanceGauge.prefab`(필요 시), `NetworkManager.prefab`, `DefaultNetworkPrefabs.asset`, 카메라 리그 프리팹(`Controllers/Camera/`, 신규)
- 읽기 전용: `Assets/Scripts/Network/Session/`(공개 진입점 `NetworkSessionManager`만 사용)

## Risks

- 지연 시 위치가 튀는 현상 — 보간 설정을 인스펙터 값으로 둔다.
- 비소유자에서 CharacterController가 NetworkTransform과 충돌할 수 있음 — 비소유자는 이동 계산을 끈다.
- 프리팹 수정은 에디터 작업(`integrate-editor`) — 저장 확인 필요(`Pitfalls/scene-not-saved.md`).

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정)
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
**작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** (2026-10-02 실제 플레이어 기준, PM·담당자 @MoHoDu 합의)

1. 4명이 방에 들어오면 각자 자기 캐릭터만 자기 키로 움직이고, 다른 사람의 이동·점프·옆줄 점프가 끊김 없이 보인다.
2. 한 사람의 균형 기울기와 추락(래그돌)이 모두에게 보이고, 추락한 사람은 조작이 잠긴다.
3. 게이지·HUD가 각자 자기 캐릭터의 균형을 보여 준다.
