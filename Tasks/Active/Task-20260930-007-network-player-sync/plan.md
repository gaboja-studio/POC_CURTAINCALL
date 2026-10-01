# Plan

## Context

004 실제 플레이어 프리팹을 온라인으로 만든다. 이후 기능 작업(005·011·006·008)이 이 결과 위에서 기능과 동기화를 같이 만든다.
결정: `Harness/Project/Decisions/feature-with-network.md`, `multiplayer-stack.md`(이동은 각자, 판정은 호스트), `player-control-architecture.md`(입력 → 조작 규칙 → 동작 부품)
도메인: `Docs/Domains/network-session.md`(방 접속), `Docs/Domains/player-control.md`(플레이어)

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 테스트 어셈블리는 만들지 않는다. AI는 컴파일만 검사한다.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

세부 순서(단계마다 테스트 씬에서 여러 명 접속으로 확인):
- 플레이어 프리팹을 네트워크 플레이어로: NetworkObject, 접속하면 각자 생성(NetworkManager `PlayerPrefab` 지정), 4명 출발 위치 나눠 서기.
- 내 캐릭터만 내 입력: 소유자가 아니면 입력·조작 규칙을 끈다(004 `PlayerInputReader`/`PlayerController`). 카메라·게이지·HUD는 내 캐릭터를 따른다(지금은 "씬에서 처음 찾은 플레이어").
- 위치·방향·점프 공유: 이동은 소유자가 하고 결과 위치를 공유(소유자 권한 이동). 보간 값은 인스펙터.
- 자세 공유: 균형 값(몸 기울기 표시용)을 소유자가 보내고 다른 사람은 기울기만 보여 준다. 흔들림 계산은 소유자만.
- 호스트 판정 통로: "요청 → 호스트가 판정 → 결과 공유" 공용 장치. 첫 사용처는 추락(균형 무너짐 신호 → 호스트 확정 → 모두에게 추락 상태). 이후 목마·도구·묘기 진행이 같은 통로를 쓴다. 상태는 늘어날 수 있게 확장 가능하게 둔다.
- 추락 래그돌: 상태가 공유되면 각자 화면에서 래그돌 연출(물리 결과까지 같게 맞추지는 않음, 필요하면 결정 요청).
- 공개 기능은 `Assets/Scripts/Network/PlayerSync/` 한 곳에 모으고 도메인 지도를 만든다.

## Files

- 수정 예정: `Assets/Scripts/Network/PlayerSync/`(신규), `Assets/Scripts/Player/`(이어받기: 소유자 처리에 필요한 최소 변경), `Assets/Resources/Prefabs/Characters/Players/Player.prefab`, `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab`, NetworkManager 프리팹(플레이어 프리팹 지정)
- 읽기 전용 참고: `Assets/Scripts/Network/Session/`(공개 진입점 `NetworkSessionManager`만 사용)

## Risks

- 지연 시 위치가 튀는 현상 — 보간 설정을 인스펙터 값으로 둔다.

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
