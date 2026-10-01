# 외줄타기 프로토타입 작업 계획

목표: **5일 안에** 4명이 온라인으로 모여 4줄로 시작해 1줄로 끝나는 직선 코스에서 걷기·균형·옆줄 이동·목마를 해 볼 수 있게 만든다.
원문: [브리프](../../../Docs/References/tightrope-prototype-brief.md) · 결정: [멀티플레이 구성](../../../Harness/Project/Decisions/multiplayer-stack.md), [배치 위치](../../../Harness/Project/Decisions/asset-placement.md), [검증 방식](../../../Harness/Project/Decisions/prototype-verification.md)

## 정해진 것 (2026-09-30, PM)

1. 멀티플레이는 **NGO + Unity Multiplayer Services**, 호스트가 방을 여는 방식. 호스트가 나가면 게임 끝.
2. **내 캐릭터 움직임은 내 컴퓨터가**, 죽음·목마·게임 진행 판정은 **호스트가** 정한다.
3. 필요한 패키지는 **PM이 이 브랜치에 먼저 설치**한 뒤 작업을 나눠 준다.
4. 조작 관련 에셋 중 Unity에서 불러와 쓰는 것(입력 에셋, 프리팹 등)은 `Assets/Resources/` 아래에 둔다.
5. AI는 **컴파일 오류만** 검사한다. 직접 해 보는 플레이 테스트는 **작업자가 하고 결과를 알려 준다.**
6. 음성 채팅, 런 기록 저장은 이번에 하지 않는다.
7. **4명 기준**으로만 만든다.
9. **2026-10-02 변경: 007(플레이어 동기화)을 먼저 하고, 이후 기능 작업은 기능과 온라인 동기화를 같이 만든다.** 007은 실제 플레이어 프리팹 기준. 연결 작업(009·010)은 최종 씬·남은 예외로 줄인다([결정](../../../Harness/Project/Decisions/feature-with-network.md)).
8. 외줄 규칙(조작·목마·추락·도착)은 [외줄 규칙 결정](../../../Harness/Project/Decisions/tightrope-rules.md)을 따른다. 코스는 4줄로 시작해 1줄로 끝나는 직선이다(줄 수는 바뀔 수 있어 인스펙터 값).

## 작업 9개

| 순서 | 작업 | 브랜치 | 시작 |
|---|---|---|---|
| 1 | 방 만들기·게임 시작/종료 | `feat/network-session` | 1일차 |
| 1 | 플레이어 조작 | `feat/player-control` | 1일차 |
| 3 | 외줄 코스·진행 (+진행 상태 동기화) | `feat/tightrope-core` | 007 병합 후 |
| 4 | 외줄 위 캐릭터 동작 (+동기화) | `feat/tightrope-rider` | 005 병합 후 |
| 5 | 목마 (+호스트 판정·동기화) | `feat/tightrope-piggyback` | 011 병합 후 |
| 2 | 플레이어 동기화 (실제 플레이어 프리팹) | `feat/network-player-sync` | 지금 (003·004 병합됨) |
| 6 | 오브젝트(도구) 동기화 | `feat/network-prop-sync` | 007 병합 후 (다른 작업과 순서 교환 가능) |
| 7 | 4인 외줄 최종 씬 (남은 연결) | `feat/tightrope-network` | 앞 작업 병합 후 |
| 7 | 목마 온라인 예외 정리 | `feat/piggyback-network` | 006 병합 후 |

- 1번(003·004)은 끝났다(통합·공동 테스트 통과). 2번 007이 동기화 기반(내 캐릭터만 조작, 위치·자세 공유, 호스트 판정 통로, UI가 내 캐릭터 따르기)을 만든다.
- 3번부터는 **실제 플레이어 프리팹과 007 공개 기능을 그대로 쓰고**, 기능과 동기화를 같이 만든다. 임시 입력·더미 캐릭터를 만들지 않는다. 단계마다 여러 명 접속(Multiplayer Play Mode)으로 확인한다.
- 한 사람이 순서대로 진행하므로 번호 순서대로 한다(008은 순서 교환 가능).
- 5일차: 4명이 함께 최종 테스트 → 고칠 것 정리 → dev에 반영.

## 작업별 내용과 작업 공간

PM이 Unity에서 폴더·테스트 씬을 미리 만들고 각 Task `setup.md`에 적는다.

| 작업 | 할 일 | 스크립트 | 프리팹·에셋 | 테스트 씬 |
|---|---|---|---|---|
| 방 만들기 | 방 생성·참가, 4명 모이면 자동 시작, 호스트 나가면 전원 종료, 게임 상태 공유 | `Assets/Scripts/Network/Session/` | `Assets/Resources/Prefabs/Controllers/Network/` | `Assets/Scenes/Tests/NetworkSession/` |
| 플레이어 조작 | W/S·A/D·Space·Q/E·F(짧게/길게) 입력, 기본 이동, 모델 바꿔 끼우기 | `Assets/Scripts/Player/` | `Assets/Resources/Input/`, `Assets/Resources/Prefabs/Characters/Players/` | `Assets/Scenes/Tests/PlayerControl/` |
| 외줄 코스·진행 | 4줄→1줄 직선 코스, 코스 조회(줄 위치·옆줄 착지 가능 여부), 묘기 진행(전원 추락 시 재시작, 1명 도착 성공 — **호스트 판정·공유**), 도착 거리 | `Assets/Scripts/Tightrope/Rope/` | `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | `Assets/Scenes/Tests/Tightrope/` |
| 외줄 위 캐릭터 동작 | 줄 위 앞뒤 이동, 004 점프·옆줄 점프에 줄 판정(`LaneJumpFilter`·착지 보정·동료 근처 감소) 연결, 004 균형 무너짐 → 추락·대기 — **추락은 호스트 판정·공유** | `Assets/Scripts/Tightrope/Rider/` (제안) | 없음 (코스 프리팹은 배치만) | `Assets/Scenes/Tests/TightropeRider/` (제안) |
| 목마 | F 짧게=올라타기, F 길게=내리기, 목마 점프 규칙, 안 되는 상황 막기, 004 목마 배율·위층 전달 연결 — **연결·해제는 호스트 판정·공유** | `Assets/Scripts/Tightrope/Piggyback/` | 없음 (실제 플레이어 프리팹 사용) | `Assets/Scenes/Tests/Piggyback/` |
| 플레이어 동기화 | **실제 플레이어 프리팹**에 온라인 적용: 내 캐릭터만 내 입력, 위치·자세·점프·균형 표시값 공유, 호스트 판정 요청·결과 통로, 상태(사망 등) 공유, 게이지·HUD가 내 캐릭터 따르기, 래그돌 공유 방식 | `Assets/Scripts/Network/PlayerSync/` + 이어받기: Player | `Assets/Resources/Prefabs/Characters/Players/`, `Assets/Resources/Input/`, `Assets/Resources/Prefabs/Controllers/Network/`(플레이어 프리팹 지정·목록 등록) | `Assets/Scenes/Tests/NetworkPlayerSync/` |
| 오브젝트 동기화 | 도구 잡기·놓기(004 상호작용 신호 사용), 1인당 최대 2개, 위치·상태 공유 — 007 판정 통로 사용 | `Assets/Scripts/Network/PropSync/` | `Assets/Resources/Prefabs/Objects/Tools/`, `Assets/Resources/Prefabs/Objects/Interactables/SyncTest/` | `Assets/Scenes/Tests/NetworkPropSync/` |
| 4인 외줄 최종 씬 | 방 만들기 → 4인 외줄 코스 최종 데모 씬, 앞 작업에서 남은 연결·버그 정리 | `Assets/Scripts/Connect/TightropeNetwork/` (필요할 때만) + 이어받기: 필요한 폴더만 PM 배정 | 필요할 때 PM 배정 | `Assets/Scenes/Tests/TightropeNetwork/` |
| 목마 온라인 예외 | 동시에 타려 할 때, 목마 중 추락(연쇄 추락), 호스트 이탈 등 예외 정리 | `Assets/Scripts/Connect/PiggybackNetwork/` + 이어받기: Tightrope/Piggyback | — | `Assets/Scenes/Tests/PiggybackNetwork/` |

- **C# 코드는 모두 `Assets/Scripts/`**, **씬은 모두 `Assets/Scenes/`** 안에 둔다. 테스트 씬은 `Assets/Scenes/Tests/<작업>/`.
- 테스트에서만 쓰는 프리팹(더미·임시 캐릭터·테스트 UI)은 테스트 씬 옆 `Prefabs/` 폴더에 둔다. 임시 입력 같은 코드는 스크립트 폴더에 둔다.
- `Assets/Resources/Input/`은 새 분류다. PM이 만들고 [Assets 폴더 규칙](../../../Docs/Guides/assets-folder-rules.md)에 추가한다.
- 모든 작업 수정 금지: `Assets/Scenes/`(자기 `Tests/<작업>/` 폴더는 제외), `ProjectSettings/`, `Packages/`, `Assets/InputSystem_Actions.inputactions`, `Assets/Settings/`

## 함께 쓰는 파일 (한 파일 = 한 작업만 수정)

| 파일 | 담당 |
|---|---|
| `Assets/Resources/Prefabs/Controllers/Network/` | 방 만들기 → 플레이어 동기화(플레이어 프리팹 지정) |
| `Assets/Resources/Input/`, `Assets/Resources/Prefabs/Characters/Players/` | 플레이어 조작 → 플레이어 동기화 → 이후 순서대로 한 작업씩 이어받기 (PM 배정) |
| `Assets/Resources/Prefabs/UIs/Player/`, `Assets/Resources/Fonts/` | 플레이어 조작 → 플레이어 동기화 → 이후 PM 배정 (`Assets/TextMesh Pro/`는 공용 리소스, 수정 금지) |
| `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | 외줄 코스·진행 (다른 작업은 씬에 놓기만) |
| `Assets/Resources/Prefabs/Objects/Tools/` | 오브젝트 동기화 |
| `Assets/Scripts/Network/PlayerSync/` | 플레이어 동기화. 이후 작업은 공개 기능만 사용, 고칠 때는 PM 배정 |

## 확인 방법

- 작업자: "검사해줘" → 컴파일 확인. 플레이 확인 항목은 AI가 목록으로 주고, **작업자가 직접 해 보고 결과를 답한다.**
- 최종 확인(5일차, 4명): 모두 방에 들어오면 시작되는가 / 외줄 코스에서 걷기·균형·옆줄 이동·목마·내리기가 모두에게 똑같이 보이는가 / 떨어진 사람은 대기로 보이고 모두 떨어지면 재시작되는가 / 한 명이 도착하면 성공인가 / 호스트가 나가면 끝나는가.

## PM이 할 일

1. Confluence 기획·키맵핑 문서를 `Docs/References/`에 올린다 (목마·균형 세부 규칙의 근거).
2. 이 브랜치에 패키지 3개 설치: NGO, Multiplayer Services, Multiplayer Play Mode + Unity Cloud 프로젝트 연결.
3. 위 폴더·테스트 씬을 Unity에서 만들고 저장한다 (빈 폴더에는 `.gitkeep`).
4. 담당자 4명의 GitHub 계정을 정해 Task를 인계한다.
