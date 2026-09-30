# 외줄타기 프로토타입 작업 계획

목표: **5일 안에** 4명이 온라인으로 모여 줄 9개 위에서 걷기·균형·옆줄 이동·목마를 해 볼 수 있게 만든다.
원문: [브리프](../../../Docs/References/tightrope-prototype-brief.md) · 결정: [멀티플레이 구성](../../../Harness/Project/Decisions/multiplayer-stack.md), [검증 방식](../../../Harness/Project/Decisions/prototype-verification.md)

## 정해진 것 (2026-09-30, PM)

1. 멀티플레이는 **NGO + Unity Multiplayer Services**, 호스트가 방을 여는 방식. 호스트가 나가면 게임 끝.
2. **내 캐릭터 움직임은 내 컴퓨터가**, 죽음·목마·게임 진행 판정은 **호스트가** 정한다.
3. 필요한 패키지는 **PM이 이 브랜치에 먼저 설치**한 뒤 작업을 나눠 준다.
4. 조작 관련 에셋 중 Unity에서 불러와 쓰는 것(입력 에셋, 프리팹 등)은 `Assets/Resources/` 아래에 둔다.
5. AI는 **컴파일 오류만** 검사한다. 직접 해 보는 플레이 테스트는 **작업자가 하고 결과를 알려 준다.**
6. 음성 채팅, 런 기록 저장은 이번에 하지 않는다.
7. **4명 기준**으로만 만든다.

## 작업 8개

| 순서 | 작업 | 브랜치 | 시작 |
|---|---|---|---|
| 1 | 방 만들기·게임 시작/종료 | `feat/network-session` | 1일차 |
| 1 | 플레이어 조작 | `feat/player-control` | 1일차 |
| 1 | 외줄 기본 | `feat/tightrope-core` | 1일차 |
| 1 | 목마 | `feat/tightrope-piggyback` | 1일차 |
| 2 | 플레이어 동기화 | `feat/network-player-sync` | 방 만들기 병합 후 |
| 2 | 오브젝트(도구) 동기화 | `feat/network-prop-sync` | 방 만들기 병합 후 |
| 3 | 연결: 외줄 + 온라인 | `feat/tightrope-network` | 4일차, 앞 작업 병합 후 |
| 3 | 연결: 목마 + 온라인 | `feat/piggyback-network` | 4일차, 앞 작업 병합 후 |

- 1번 작업들은 **동시에** 시작한다. 외줄·목마는 임시 입력·더미 캐릭터로 먼저 만들고, 3번 연결 작업에서 진짜 조작·온라인과 합친다.
- 5일차: 4명이 함께 최종 테스트 → 고칠 것 정리 → dev에 반영.

## 작업별 내용과 작업 공간

PM이 Unity에서 폴더·테스트 씬을 미리 만들고 각 Task `setup.md`에 적는다.

| 작업 | 할 일 | 스크립트 | 프리팹·에셋 | 테스트 씬 |
|---|---|---|---|---|
| 방 만들기 | 방 생성·참가, 4명 모이면 자동 시작, 호스트 나가면 전원 종료, 게임 상태 공유 | `Assets/Scripts/Network/Session/` | `Assets/Resources/Prefabs/Controllers/Network/` | `Assets/_Sandbox/NetworkSession/` |
| 플레이어 조작 | W/S·A/D·Space·Q/E·F(짧게/길게) 입력, 기본 이동, 모델 바꿔 끼우기 | `Assets/Scripts/Player/` | `Assets/Resources/Input/`, `Assets/Resources/Prefabs/Characters/Players/` | `Assets/_Sandbox/PlayerControl/` |
| 외줄 기본 | 줄 9개, 앞뒤 이동, 균형, 점프, 옆줄 이동, 떨어지면 사망 | `Assets/Scripts/Tightrope/Rope/` | `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | `Assets/_Sandbox/Tightrope/` |
| 목마 | F 짧게=올라타기, F 길게=내리기, 안 되는 상황 막기 | `Assets/Scripts/Tightrope/Piggyback/` | 더미 캐릭터: `Assets/_Sandbox/Piggyback/Prefabs/` | `Assets/_Sandbox/Piggyback/` |
| 플레이어 동기화 | 위치·상태(사망 등)·애니메이션 값 공유 (테스트 캡슐 사용) | `Assets/Scripts/Network/PlayerSync/` | 테스트 캡슐: `Assets/_Sandbox/NetworkPlayerSync/Prefabs/` | `Assets/_Sandbox/NetworkPlayerSync/` |
| 오브젝트 동기화 | 도구 잡기·놓기, 1인당 최대 2개, 위치·상태 공유 | `Assets/Scripts/Network/PropSync/` | `Assets/Resources/Prefabs/Objects/Tools/`, `Assets/Resources/Prefabs/Objects/Interactables/SyncTest/` | `Assets/_Sandbox/NetworkPropSync/` |
| 연결: 외줄+온라인 | 진짜 플레이어에 온라인 적용, 줄 위 움직임·사망 공유, **4인 9줄 최종 씬** | `Assets/Scripts/Connect/TightropeNetwork/` + 이어받기: Player, Tightrope/Rope, Network/PlayerSync | 플레이어 프리팹 이어받기 | `Assets/_Sandbox/TightropeNetwork/` |
| 연결: 목마+온라인 | 줄 위 목마, 호스트 판정, 동시에 타려 할 때 등 예외 | `Assets/Scripts/Connect/PiggybackNetwork/` + 이어받기: Tightrope/Piggyback | — | `Assets/_Sandbox/PiggybackNetwork/` |

- 테스트용 더미·캡슐은 불러올 일이 없으므로 각자 테스트 씬 폴더 안에 둔다.
- `Assets/Resources/Input/`은 새 분류다. PM이 만들고 [Assets 폴더 규칙](../../../Docs/Guides/assets-folder-rules.md)에 추가한다.
- 모든 작업 수정 금지: `Assets/Scenes/`, `ProjectSettings/`, `Packages/`, `Assets/InputSystem_Actions.inputactions`, `Assets/Settings/`

## 함께 쓰는 파일 (한 파일 = 한 작업만 수정)

| 파일 | 담당 |
|---|---|
| `Assets/Resources/Prefabs/Controllers/Network/` | 방 만들기 |
| `Assets/Resources/Input/`, `Assets/Resources/Prefabs/Characters/Players/` | 플레이어 조작 → 병합 후 연결: 외줄+온라인 |
| `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | 외줄 기본 (다른 작업은 씬에 놓기만) |
| `Assets/Resources/Prefabs/Objects/Tools/` | 오브젝트 동기화 |
| `Assets/Scripts/Network/PlayerSync/` (연결 단계) | 연결: 외줄+온라인만 |

## 확인 방법

- 작업자: "검사해줘" → 컴파일 확인. 플레이 확인 항목은 AI가 목록으로 주고, **작업자가 직접 해 보고 결과를 답한다.**
- 최종 확인(5일차, 4명): 모두 방에 들어오면 시작되는가 / 9줄에서 걷기·균형·옆줄 이동·목마·내리기가 모두에게 똑같이 보이는가 / 떨어지면 사망으로 보이는가 / 호스트가 나가면 끝나는가.

## PM이 할 일

1. Confluence 기획·키맵핑 문서를 `Docs/References/`에 올린다 (목마·균형 세부 규칙의 근거).
2. 이 브랜치에 패키지 3개 설치: NGO, Multiplayer Services, Multiplayer Play Mode + Unity Cloud 프로젝트 연결.
3. 위 폴더·테스트 씬을 Unity에서 만들고 저장한다 (빈 폴더에는 `.gitkeep`).
4. 담당자 4명의 GitHub 계정을 정해 Task를 인계한다.
