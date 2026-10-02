# Integration-tightrope-prototype

- **Feature:** 멀티플레이 기반 + 외줄타기 묘기 프로토타입
- **Status:** preparing
- **Branch:** integration/tightrope-prototype
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 순차 병합 — 1단계 작업은 2~3일차, 연결 작업은 4일차, 공동 테스트·dev 반영은 5일차 (착수 후 5일 기한)
- **Updated:** 2026-10-02

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 4명이 온라인 방에 모이면 게임이 시작되고, 4줄로 시작해 1줄로 끝나는 직선 코스 위에서 이동·균형·옆줄 이동·목마·해제를 함께 테스트할 수 있다.
- 쉬운 설명·작업 공간 전체: [plan.md](plan.md)

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20260930-003 | feat | @MoHoDu | feat/network-session | #6 | integrated |
| Task-20260930-004 | feat | @MoHoDu | feat/player-control | #7 | integrated |
| Task-20260930-005 | feat | @MoHoDu(임시) | feat/tightrope-core | - | assigned |
| Task-20260930-006 | feat | @MoHoDu(임시) | feat/tightrope-piggyback (인계 때 생성) | - | scaffolded |
| Task-20260930-007 | feat | @MoHoDu(임시) | feat/network-player-sync | #11 | submitted (병합됨, 병합 후 공동 테스트 대기) |
| Task-20260930-008 | feat | @MoHoDu(임시) | feat/network-prop-sync (인계 때 생성) | - | scaffolded |
| Task-20260930-009 | feat | @MoHoDu(임시) | feat/tightrope-network (인계 때 생성) | - | scaffolded |
| Task-20260930-010 | feat | @MoHoDu(임시) | feat/piggyback-network (인계 때 생성) | - | scaffolded |
| Task-20261001-011 | feat | @MoHoDu | feat/tightrope-rider | - | assigned |

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Resources/Prefabs/Controllers/Network/` | Task-20260930-003 → 007 | NetworkManager 프리팹 (007이 플레이어 프리팹 지정·목록 등록) |
| `Assets/Resources/Input/` | Task-20260930-004 → 007 → 이후 PM 배정 | 조작 입력 에셋 |
| `Assets/Resources/Prefabs/Characters/Players/` | Task-20260930-004 → 007 → 이후 PM 배정 | 플레이어 프리팹 (007이 네트워크 적용, 이후 Task가 차례로 기능 추가) |
| `Assets/Resources/Prefabs/UIs/Player/` | Task-20260930-004 → 007 → 이후 PM 배정 | 플레이어 UI(균형 게이지) 프리팹 |
| `Assets/Resources/Fonts/` | Task-20260930-004 → 이후 PM 배정 | 임시 UI 폰트(TMP) |
| `Assets/TextMesh Pro/` | 없음 (공용, 수정 금지) | TMP 기본 리소스 (2026-10-01 004에서 가져옴) |
| `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | Task-20260930-005 | 외줄 코스 스테이지 (다른 Task는 배치만) |
| `Assets/Resources/Prefabs/Objects/Tools/` | Task-20260930-008 | 도구 프리팹 |
| `Assets/Scripts/Network/PlayerSync/` | Task-20260930-007 | 이후 Task는 공개 기능만 사용, 수정은 PM 배정. 테스트 부품 `TestRoundRestart.cs`는 005, `TestLaneLanding.cs`는 011이 대체·삭제 |

`Packages/`, `ProjectSettings/`는 PM이 인계 전 이 브랜치에서 직접 설치·설정한다(모든 Task 수정 금지).

## Decisions

- Open: 005~010 담당자 GitHub 계정 (미배정, 작업 상황 보고 PM이 결정). 011은 @MoHoDu 확정
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지 PM 선설치, 조작용 로드 에셋은 `Assets/Resources/`, AI는 컴파일만·플레이 테스트는 작업자, 음성·런 데이터 제외, 4인 기준 ([결정](../../../Harness/Project/Decisions/multiplayer-stack.md), [검증](../../../Harness/Project/Decisions/prototype-verification.md))
- Decided: 2026-10-01 @MoHoDu — 패키지 설치·Unity Cloud 연결(gaboja-studio) 완료, Confluence 2건 요약 반입, 외줄 규칙 확정([결정](../../../Harness/Project/Decisions/tightrope-rules.md))
- Decided: 2026-10-01 @MoHoDu — 005(외줄 기본)를 "외줄 코스·진행"(005)과 "외줄 위 캐릭터 동작"(011)으로 나눈다. 004를 먼저 진행하고 006~011은 004 진입점 확정 후 진행, 005는 004와 병렬
- Decided: 2026-10-02 @MoHoDu — 007(플레이어 동기화)을 실제 플레이어 프리팹 기준으로 먼저 하고, 이후 기능 Task(005·011·006·008)는 기능+동기화를 같이 만든다. 순서 007 → 005 → 011 → 006 → 008(교환 가능) → 009·010(최종 씬·예외로 축소) ([결정](../../../Harness/Project/Decisions/feature-with-network.md))
