# Plan

## Context

외줄타기 묘기의 무대와 진행. 입력·균형과 무관하게 코스와 묘기 진행(재시작·성공)을 먼저 만든다. 줄 위 캐릭터 동작은 011.
원문: `Docs/References/tightrope-prototype-brief.md`
요약: `Docs/References/tightrope-keymap-summary.md`, `Docs/References/tightrope-plan-summary.md` · 규칙 결정: `Harness/Project/Decisions/tightrope-rules.md`

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 테스트 어셈블리는 만들지 않는다. AI는 컴파일만 검사한다.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

세부 순서:

- 코스: 4줄로 시작해 1줄로 끝나는 직선 코스 스테이지를 프리팹으로 만든다(다른 작업은 배치만 한다). 줄 수·줄 간격(기획 1.5m → 2.0m, 007 결정. `PlayerMover.LaneSpacing`과 같은 값)·줄이 줄어드는 위치·옆줄 이동 가능 구간·시작 플랫폼·도착 거리는 인스펙터 값으로 둔다.
- 코스 조회 진입점(011·장애물·온라인이 사용): 위치를 가장 가까운 줄과 진행 거리로 바꾸기, 그 위치에서 왼쪽/오른쪽에 착지 가능한 줄 찾기, 옆줄 이동 가능 구간인지, 줄 사용 가능 여부(이후 화재용 자리), 출발 위치.
- 묘기 진행: 참가자 등록, 추락·도착 알림을 받는다. 4명 모두 추락하면 실패 → 재시작 신호(모든 요소를 초기 상태로), 1명이라도 도착 거리에 닿으면 성공. 상태(진행/실패/성공)를 공개한다.
- 시험은 실제 플레이어 프리팹(007 적용)으로 MPPM 여러 명 접속해서 한다. 디버그 조작(추락·도착 알리기)은 이 작업 스크립트 폴더 안에만 둔다. 줄 위 걷기·점프·옆줄 이동은 만들지 않는다(011).
동기화(2026-10-02, `Harness/Project/Decisions/feature-with-network.md`):
- 코스는 모두가 같은 프리팹·같은 인스펙터 값으로 만들므로 따로 공유하지 않는다.
- 묘기 진행(참가자·추락·도착·실패/성공·재시작)은 **호스트가 판정**하고 007 판정 통로로 모두에게 공유한다. 재시작 신호는 모든 플레이어에게 간다.

007에서 이어받는 것(2026-10-02, `Docs/Domains/player-sync.md`):
- 쓸 공개 기능: `NetworkPlayer.All`·`State`·`StateChanged`(추락 = `PlayerState.Fallen`), `NetworkPlayer.ServerRestartAll()`(모두 출발점·균형 초기화·래그돌 복구·조작 해제), `NetworkPlayer.ServerCanChangeState`(예: 진행 중일 때만 추락), 세션 `NetworkSessionManager.EndGame()`(성공).
- 재시작은 개인이 아니라 전원 추락 시 모두 같이(`tightrope-rules.md`). 테스트 부품 `PlayerSync/TestRoundRestart.cs`(전원 추락 3초 후 재시작)를 이 작업의 묘기 진행으로 대체하고 삭제한다(setup 배정).
- 줄 밖 착지 추락(`PlayerSync/TestLaneLanding.cs`)은 옆줄 착지 판정과 함께 011이 대체한다.
- 출발 위치: 코스 시작 플랫폼이 `PlayerSpawnPoints`(자리 번호 순 Transform) 역할을 하게 둔다. 줄 간격은 `PlayerMover.LaneSpacing`(2.0m)과 맞춘다.
- 균형은 시작하자마자 켜져 있다(`PlayerBalance` `activeOnStart`). 줄에 오를 때 켜고 내릴 때 끄는 처리는 011.
- 테스트 씬에는 007 테스트 씬(`Assets/Scenes/Tests/NetworkPlayerSync/`)처럼 SessionUI(`NetworkSessionTestUI`)·카메라 리그(`Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab` + 메인 카메라 CinemachineBrain)·`BalanceGauge`·`PlayerCommandDebugHud`·`LocalPlayerViews`를 둔다(배치만).
- 기획과 다른 수치는 항상 인스펙터 값으로 두고, 작업이 끝나면 "- 어떤 기획: 어떤 변경"으로 보고한다(2026-10-02 PM).
- 저장 검사가 MPPM 가상 플레이어를 에디터로 세어 멈춘다(하네스 수정 전까지 저장 전에 가상 플레이어를 끈다).

## Files

- 수정 예정:
- 읽기 전용 참고:

## Risks

- 코스 조회 진입점은 011·009가 그대로 받아 쓴다. 이름·의미를 착수 초기에 정해 011 plan에 알리고, 이후 바꾸면 011과 함께 고친다.
- 재시작 범위("모든 요소를 초기 상태로")는 지금 코스·참가자만 해당한다. 장애물 등이 생기면 재시작 신호를 받아 각자 초기화한다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정)
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
**작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** (초안 — 인계 전 PM·담당자 합의)

1. 인스펙터에서 줄 수·줄 간격·줄이 줄어드는 위치·도착 거리를 바꾸면 코스가 그대로 다시 만들어진다(4줄 → 1줄).
2. 코스 위 아무 위치에서 왼쪽/오른쪽 착지 가능한 줄과 옆줄 이동 가능 구간이 디버그 표시로 맞게 보인다.
3. 여러 명(MPPM) 중 일부만 추락하면 진행이 계속되고, 모두 추락하면 모두 같이 출발점에서 재시작되며, 한 명이 도착 거리에 닿으면 모든 화면에 성공으로 표시된다. (2026-10-02 실제 플레이어 기준으로 수정, PM @MoHoDu)
