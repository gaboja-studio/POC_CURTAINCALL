# Plan

## Context

지금은 테스트 씬(SpaceLink 허브, `NetworkSessionTestUI`)에서만 방을 열고 참가한다. 게임을 켰을 때 처음 보는 **시작 씬**과, 방에 모여 출발을 기다리는 **로비 씬**을 실제 게임 씬으로 만든다.

- 시작 씬: 게임 실행 첫 화면. 방 만들기·방 코드로 참가·종료.
- 로비 씬: 방 코드·참가자 목록(인원/정원)·나가기, 호스트만 게임 시작(최소 인원 이상). 시작하면 런의 첫 장소로 전원 이동.
- 규칙 근거: `run-rules.md` #3(로비 재입장 허용, 시작 후 참가 불가), 게임 흐름 기획 v1(Confluence 15564804) 2-2·9-1(런 시작 → 도입 컷씬 → 1일차 줄타기). 기획 문서에는 시작 화면·로비 화면 구성이 아직 없다.
- 화면 구성·문구·연출 같은 플레이어 체감 선택은 `human-decision.md`로 사람에게 확인한다.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

## Files

- 수정 예정: `Assets/Scenes/Start_Lobby/Start.unity`, `Assets/Scenes/Start_Lobby/Lobby.unity`, `Assets/Scripts/Systems/StartLobby/` 새 코드, 빌드 씬 목록
- 읽기 전용 참고: `Docs/Domains/network-session.md`, `Docs/Domains/common-game-systems.md`, `Assets/Scripts/Systems/SpaceLink/SpaceLinkHubCanvas.cs`(기존 Canvas 허브 방식), `Assets/Scripts/Systems/Network/Session/NetworkSessionManager.cs`

## Risks

- 로비 → 첫 장소 이동은 SpaceLink·Campaign 공용 코드와 연결된다. 공용 코드 수정이 필요하면 직접 고치지 않고 PM 구역 확대 후 진행한다.
- 씬 전환 시 NetworkManager·세션 유지(DontDestroyOnLoad, NGO 씬 전환) 방식에 따라 기존 SpaceLink 흐름과 충돌할 수 있다.
- 시작 화면·로비 화면 구성이 기획 문서에 없다. 임의로 확정하지 않고 사람 결정으로 남긴다.

## Auto Verification

- 구역 검사: `Scripts/verify-scope.ps1` (기준 `integration/start-lobby`)
- 1 문서/규칙: `Scripts/verify-fast.ps1`
- 2 컴파일: `Scripts/verify-unity.ps1 -ProjectPath . -Compile`
- 3 테스트: 로비 상태(인원·시작 가능 여부) 판단 로직이 생기면 EditMode 테스트, 없으면 `NO_PROJECT_TESTS`
- 4 실행 로그: 시작 → 방 만들기 → 로비 → 게임 시작 1회 실행 로그(Console 오류 0)

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
불필요하면 `불필요 — 이유`.

1. **방 만들기와 참가**: 2명(호스트 1·참가자 1, MPPM 또는 2 PC). 호스트가 시작 씬에서 방 만들기 → 로비에 방 코드 표시 → 참가자가 코드로 참가 → 두 화면 로비에 인원 2/정원과 참가자가 같게 보인다.
2. **로비 나가기·재입장**: 참가자가 로비에서 나가면 시작 씬으로 돌아가고 호스트 로비 인원이 줄어든다. 같은 코드로 다시 참가할 수 있다. 호스트가 나가면 참가자는 이유 문구와 함께 시작 씬으로 돌아간다.
3. **게임 시작**: 최소 인원 미만이면 시작 버튼이 막히고 참가자에게는 시작 버튼이 없다. 호스트가 시작하면 두 화면이 동시에 첫 장소로 이동하고, 시작 뒤 새 참가는 거절된다.

- 기록: 일시·참여자·호스트/참가 역할·실행 씬(`Start.unity`)·재현 순서·예상·실제를 handoff `## 공동 테스트 결과`에 적는다.
