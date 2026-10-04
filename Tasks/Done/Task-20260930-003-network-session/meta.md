# Task-20260930-003

- **Title:** 방 만들기·게임 시작/종료
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/network-session.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-01

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(2026-10-05 PM 요청으로 완료 보관, dev 반영은 별도). 막히면 blocked.

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
- Decided: 2026-10-01 @MoHoDu — 4명이 모이면 즉시 시작(브리프). 진행 중 클라이언트가 나가면 남은 인원으로 계속하고 진행 중 참가는 거절. 종료(Ended)는 호스트 `EndGame()` 판정 API만 제공(도착 판정 호출은 009)
- Decided: 2026-10-01 @MoHoDu — 방 참가는 방 고유 코드(세션 Code)로 한다. 비공개 세션(코드로만 참가), 최대 4명. 로비 검색은 이후 별도 인터페이스로 확장할 수 있게 접속 방식을 `ISessionConnector`로 분리
- Decided: 2026-10-01 @MoHoDu — 담당자 @MoHoDu 확정. 세부 규칙은 `Docs/References/tightrope-keymap-summary.md`, `Harness/Project/Decisions/tightrope-rules.md` 기준
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
