# Integration-start-lobby

- **Feature:** 시작·로비 씬
- **Status:** done
- **Branch:** integration/start-lobby
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:** 2026-10-16 10:40 UTC — #136 병합, 공동 테스트 대기
- **Updated:** 2026-10-18

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- dev에서 게임을 켜면 시작 씬 → 방 만들기/참가 → 로비 → 호스트 게임 시작으로 런에 들어갈 수 있다. 테스트 씬 IMGUI 대신 실제 시작·로비 화면을 쓴다.
- 구성: 단일 Task(052). 씬은 `Assets/Scenes/Start_Lobby/`, 코드는 `Assets/Scripts/Systems/StartLobby/`.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261016-052 | feat | @MoHoDu | feat/start-lobby-scenes | [#136](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/136) | done |

## 준비 브랜치

선택 사항. 준비 branch를 재사용할 때만 기록한다. 두 SHA는 부모 통합 이력의 전체 40자리 커밋 SHA이며 승인 SHA는 준비 기준 SHA를 포함한다. `Active Task`가 없거나 비어 있으면 기존 일반 통합 흐름이다. 값이 있으면 해당 Task 하나만 배정·착수한다. Task meta의 선택 `Previous Task`에는 실제 Task ID만 적는다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Scenes/Start_Lobby/` (`Start.unity`, `Lobby.unity`) | Task-20261016-052 | 실제 시작·로비 씬. 빌드 씬 목록 추가는 모든 작업자 허용 |
| `Assets/Scripts/Systems/Network/Session/NetworkSessionManager.cs`, `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset`, `Assets/Scripts/Systems/SpaceLink/Tests/PlayMode/SpaceLinkPlayTests.cs` | Task-20261016-052 | 2026-10-16 PM 승인: 로비가 SpaceLink 허브 대신, 런 종료 후 같은 방 복귀 |
| `Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset`, `Assets/Scenes/Tests/Systems/DummySpaces/Display/dummy-korean.asset` | Task-20261016-052 | 한글 Static 재굽기만 승인. 실제 변경은 Pretendard만 |
| `Docs/Domains/network-session.md`, `Docs/Domains/start-lobby.md`, `Docs/Domains/index.md` | Task-20261016-052 | 복귀 API·시작/로비 기능 지도 기록 |

## Decisions

- Open: None
- Decided: 2026-10-18 — 사용자 기록 정리 요청으로 Done 이동. dev 반영 #145(d2d002c). 실제 공연 연결·낙하는 Integration-performance-scene-link(Task 057·058)에서 해결, 10-18 실제 2인 공동 테스트로 시작→로비→공연→엔딩 로비 복귀 흐름 PASS(PM 보고). 온라인 방 코드·설정 저장·빌드 화면 모드·4인은 별도 기록 없음.
- Decided: 2026-10-16 PM — "현재 상태로 먼저 병합" 승인. 미검증·알려진 문제를 명시해 dev에 선반영하되 Task submitted / Integration testing과 Active 위치를 유지한다. 완료 처리·브랜치 삭제 승인이 아니다.
- Decided: 2026-10-16 PM — 브랜치 `integration/start-lobby` + `feat/start-lobby-scenes`, 씬 위치 `Assets/Scenes/Start_Lobby/`, 코드 `Assets/Scripts/Systems/StartLobby/`, 담당 @MoHoDu. Jira CC-133(PM 등록). 로비 준비·시작은 출연 명단 서명 + 방장 커튼 레버, 로비 시점 고정 3D(맵 밖으로 안 나감), 설정은 핵심만
