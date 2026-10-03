# Task-20260930-005

- **Title:** 외줄 코스·진행
- **Type:** feat
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** submit-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-core

## Goal

- 4줄로 시작해 1줄로 끝나는 직선 코스가 인스펙터 값대로 만들어지고, 다른 작업이 줄 위치·옆줄 착지 가능 여부를 물어볼 수 있으며, 모두 떨어지면 재시작·한 명이 도착하면 성공으로 묘기가 진행된다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 플레이어 동기화(007) 병합 후. 줄 위 캐릭터 동작은 011이 이 작업의 진입점을 받아 쓴다. 묘기 진행 상태 동기화는 이 작업에서 같이 한다(2026-10-02 변경).
- 2026-10-01 "줄 위 캐릭터 동작"(전진·후진·점프·옆줄 이동·낙하)을 011로 나눴다.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 담당자 확정(현재 임시 @MoHoDu)
- Decided: 2026-10-01 @MoHoDu — 005를 "코스 + 진행"(005)과 "줄 위 캐릭터 동작"(011)으로 나눈다
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
- Decided: 2026-10-02 @MoHoDu — 줄 간격 2.0m(007), 재시작은 전원 추락 시 모두 같이(007 `ServerRestartAll`), 007 `TestRoundRestart`는 이 작업이 대체·삭제, `TestLaneLanding`은 011
- Decided: 2026-10-02 @MoHoDu — 기능과 온라인 동기화를 같이 만든다. 실제 플레이어 프리팹과 007 공개 기능(`Assets/Scripts/Network/PlayerSync/`)·004 공개 함수를 쓴다. 단계마다 여러 명 접속으로 확인 (`Harness/Project/Decisions/feature-with-network.md`)
