# Task-20261001-011

- **Title:** 외줄 위 캐릭터 동작
- **Type:** feat
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-05

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-rider

## Goal

- 옆줄 점프가 코스 규칙(이동 가능 구간·착지할 줄 있음·불타지 않은 줄)을 따르고, 착지 자리가 겹치면 상대 뒤쪽에 착지하며, 동료 가까이(1m) 착지하면 손잡기로 흔들림이 줄어든다. 걷기·점프·균형 추락·재시작은 004·005·007에 이미 있다(2026-10-03 범위 축소).

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 플레이어 조작(004)의 명령·`PlayerBalance` 진입점, 외줄 코스·진행(005)의 코스 조회·진행 진입점. 004(병합 완료)·007·005 병합 후 진행한다(2026-10-02 변경). 임시 코스를 만들지 않는다.
- 2026-10-01 005(외줄 기본)에서 "줄 위 캐릭터 동작"을 나눠 만든 Task다. 코스·진행은 005에 남는다.
- 2026-10-03 범위 축소: 남은 것은 옆줄 판정 연결·뒤쪽 착지·손잡기·테스트 부품 정리 4개. 끝나면 같은 갈래(B)에서 006 목마로 이어간다. 013(세팅) 병합 후 시작.
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(013, `Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Decided: 2026-10-03 @MoHoDu — 범위 축소(남은 4개), 공동 테스트 항목을 남은 범위에 맞춰 바꾼다
- Decided: 2026-10-01 @MoHoDu — 005를 "코스 + 진행"(005)과 "줄 위 캐릭터 동작"(011)으로 나눈다
- Decided: 2026-10-01 @MoHoDu — 담당자 @MoHoDu(PM 직접) 확정, 공동 테스트 3개 확정, JIRA CC-53
- Decided: 2026-10-02 @MoHoDu — 004의 6·8·10단계(추락 신호·점프·옆줄 명령과 충격)가 integration에 병합된 뒤 착수한다. 임시 입력·자체 접점은 만들지 않는다
- Superseded: 2026-10-02 @MoHoDu — 줄 위 이동은 011이 직접 맡고 004 `PlayerMover`를 끈다 → 2026-10-03 범위 축소로 대체(줄 위 이동은 004 `PlayerMover`·005 전환이 이미 담당)
- Decided: 2026-10-02 @MoHoDu — 옆줄 착지 자리가 뒤쪽까지 전혀 없으면 착지 실패 = 추락
- Decided: 2026-10-01 @MoHoDu — 균형 기능의 베이스는 004(player-control)에서 구현 중이다. 011은 균형을 새로 만들지 않고 004 진입점에 충격을 넣고 신호를 받기만 한다
- Decided: 2026-10-01 @MoHoDu — 세부 규칙은 `Docs/References/tightrope-keymap-summary.md`, `Harness/Project/Decisions/tightrope-rules.md`, 균형은 `Harness/Project/Decisions/tightrope-balance.md`(004에서 구현) 기준
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
- Decided: 2026-10-02 @MoHoDu — 기능과 온라인 동기화를 같이 만든다. 실제 플레이어 프리팹과 007 공개 기능(`Assets/Scripts/Network/PlayerSync/`)·004 공개 함수를 쓴다. 단계마다 여러 명 접속으로 확인 (`Harness/Project/Decisions/feature-with-network.md`)
