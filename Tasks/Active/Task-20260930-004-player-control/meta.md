# Task-20260930-004

- **Title:** 플레이어 조작
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-01

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/player-control (인계 때 생성)

## Goal

- 공통 입력(W/S, A/D, Space, Q/E 홀드, F 짧게/길게)으로 캐릭터가 움직이고, 디폴트 모델을 다른 모델로 바꿔 끼울 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 없음 (1일차 시작). 외줄(005)·목마(006)는 임시 입력으로 병행하고, 연결 작업(009)에서 이 입력으로 교체한다.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: None
- Decided: 2026-10-01 @MoHoDu — 담당자 @MoHoDu 확정. 세부 규칙은 `Docs/References/tightrope-keymap-summary.md`, `Harness/Project/Decisions/tightrope-rules.md` 기준
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
