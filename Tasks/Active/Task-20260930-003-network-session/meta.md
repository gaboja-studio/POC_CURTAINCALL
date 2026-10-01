# Task-20260930-003

- **Title:** 방 만들기·게임 시작/종료
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-01

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/network-session

## Goal

- 방을 만들고 참가해 4명이 모이면 게임이 자동으로 시작되고, 호스트가 나가면 모두 종료되며, 게임 상태(대기/진행/종료)가 모두에게 같게 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 없음 (1일차 시작). 이 작업이 병합되어야 플레이어 동기화(007)·오브젝트 동기화(008)를 인계한다.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: None
- Decided: 2026-10-01 @MoHoDu — 담당자 @MoHoDu 확정. 세부 규칙은 `Docs/References/tightrope-keymap-summary.md`, `Harness/Project/Decisions/tightrope-rules.md` 기준
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
