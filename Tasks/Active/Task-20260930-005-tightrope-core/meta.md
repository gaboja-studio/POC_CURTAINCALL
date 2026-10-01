# Task-20260930-005

- **Title:** 외줄 기본
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-09-30

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-core (인계 때 생성)

## Goal

- 4줄로 시작해 1줄로 끝나는 직선 코스에서 앞뒤로 걷고, A/D로 균형을 잡고, 점프하고, Q/E 홀드 + Space로 옆줄로 넘어가며, 떨어진 사람은 재시작까지 대기하고, 모두 떨어지면 재시작, 한 명이 도착하면 성공한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 없음 (1일차 시작, 임시 입력 사용). 진짜 입력·온라인은 연결 작업(009)에서 붙인다.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 담당자 확정(현재 임시 @MoHoDu) / 세부 규칙은 Confluence 문서 반입 후 확인
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
