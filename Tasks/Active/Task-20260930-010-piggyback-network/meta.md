# Task-20260930-010

- **Title:** 연결: 목마 + 온라인
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-01

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/piggyback-network (인계 때 생성)

## Goal

- 온라인 상태에서 줄 위 목마 연결·해제가 모두에게 같게 보이고, 동시에 타려 하는 등 예외가 올바르게 처리된다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 플레이어 조작(004)·목마(006)·외줄 코스·진행(005)·외줄 위 캐릭터 동작(011)·플레이어 동기화(007) 병합 후 4일차 인계. 009와 동시 진행하는 연결 Task.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 담당자 확정(현재 임시 @MoHoDu) / 세부 규칙은 Confluence 문서 반입 후 확인
- Decided: 2026-09-30 @MoHoDu — NGO + Multiplayer Services(Host), 이동은 클라·판정은 호스트, AI는 컴파일만 검사·플레이 테스트는 작업자가 직접 하고 결과를 알려 줌 (`Harness/Project/Decisions/multiplayer-stack.md`, `prototype-verification.md`)
