# Integration-tightrope-prototype

- **Feature:** 멀티플레이 기반 + 외줄타기 묘기 프로토타입
- **Status:** preparing
- **Branch:** integration/tightrope-prototype
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 순차 병합 — 1단계 작업은 2~3일차, 연결 작업은 4일차, 공동 테스트·dev 반영은 5일차 (착수 후 5일 기한)
- **Updated:** 2026-10-01

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 4명이 온라인 방에 모이면 게임이 시작되고, 4줄로 시작해 1줄로 끝나는 직선 코스 위에서 이동·균형·옆줄 이동·목마·해제를 함께 테스트할 수 있다.
- 쉬운 설명·작업 공간 전체: [plan.md](plan.md)

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20260930-003 | feat | @MoHoDu(임시) | feat/network-session (인계 때 생성) | - | scaffolded |
| Task-20260930-004 | feat | @MoHoDu(임시) | feat/player-control (인계 때 생성) | - | scaffolded |
| Task-20260930-005 | feat | @MoHoDu(임시) | feat/tightrope-core (인계 때 생성) | - | scaffolded |
| Task-20260930-006 | feat | @MoHoDu(임시) | feat/tightrope-piggyback (인계 때 생성) | - | scaffolded |
| Task-20260930-007 | feat | @MoHoDu(임시) | feat/network-player-sync (인계 때 생성) | - | scaffolded |
| Task-20260930-008 | feat | @MoHoDu(임시) | feat/network-prop-sync (인계 때 생성) | - | scaffolded |
| Task-20260930-009 | feat | @MoHoDu(임시) | feat/tightrope-network (인계 때 생성) | - | scaffolded |
| Task-20260930-010 | feat | @MoHoDu(임시) | feat/piggyback-network (인계 때 생성) | - | scaffolded |

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Resources/Prefabs/Controllers/Network/` | Task-20260930-003 | NetworkManager 프리팹 |
| `Assets/Resources/Input/` | Task-20260930-004 → 병합 후 009 | 조작 입력 에셋 |
| `Assets/Resources/Prefabs/Characters/Players/` | Task-20260930-004 → 병합 후 009 | 플레이어 프리팹 (009가 네트워크 적용) |
| `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/` | Task-20260930-005 | 외줄 코스 스테이지 (다른 Task는 배치만) |
| `Assets/Resources/Prefabs/Objects/Tools/` | Task-20260930-008 | 도구 프리팹 |
| `Assets/Scripts/Network/PlayerSync/` (연결 단계) | Task-20260930-007 → 병합 후 009 | 010과 동시 수정 방지 |

`Packages/`, `ProjectSettings/`는 PM이 인계 전 이 브랜치에서 직접 설치·설정한다(모든 Task 수정 금지).

## Decisions

- Open: 담당자 GitHub 계정 (미배정, 작업 상황 보고 PM이 결정)
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, 패키지 PM 선설치, 조작용 로드 에셋은 `Assets/Resources/`, AI는 컴파일만·플레이 테스트는 작업자, 음성·런 데이터 제외, 4인 기준 ([결정](../../../Harness/Project/Decisions/multiplayer-stack.md), [검증](../../../Harness/Project/Decisions/prototype-verification.md))
- Decided: 2026-10-01 @MoHoDu — 패키지 설치·Unity Cloud 연결(gaboja-studio) 완료, Confluence 2건 요약 반입, 외줄 규칙 확정([결정](../../../Harness/Project/Decisions/tightrope-rules.md))
