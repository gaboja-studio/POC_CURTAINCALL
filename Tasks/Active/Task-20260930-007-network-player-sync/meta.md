# Task-20260930-007

- **Title:** 플레이어 동기화
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-02

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/network-player-sync

## Goal

- 4명이 방에 모이면 각자 **실제 플레이어 캐릭터(004)**를 자기 키로만 조작하고, 위치·자세(균형 기울기)·점프·상태(추락 등)가 모두에게 똑같이 보이며, 이후 기능이 쓸 호스트 판정 통로가 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 방 만들기(003)·플레이어 조작(004) 병합·공동 테스트 통과(2026-10-02). 이후 005·011·006·008이 이 작업의 공개 기능을 쓴다.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 담당자 확정(현재 임시 @MoHoDu)
- Decided: 2026-10-02 @MoHoDu — 테스트 캡슐 대신 004 실제 플레이어 프리팹에 동기화를 만든다. 이후 Task는 기능+동기화를 같이 하고 이 작업 공개 기능을 쓴다 (`Harness/Project/Decisions/feature-with-network.md`)
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
