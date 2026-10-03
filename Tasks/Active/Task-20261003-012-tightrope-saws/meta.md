# Task-20261003-012

- **Title:** 톱날 장애물(가로·수직)
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 없음 (코드가 생기면 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-saws (인계 때 생성)

## Goal

- 외줄 코스 위에 가로 톱날 22개가 좌우로 왕복하고 수직 톱날이 줄을 따라 내려오며, 톱날에 닿은 플레이어는 즉시 사망한다. 가로는 점프로, 수직은 옆줄 이동(또는 목마 2층이 넘어가 버튼)으로 피하는 모습이 모두에게 같게 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 005(코스 조회·진행·묘기 시작·사망 판정 통로) 병합 후. 수직 톱날 버튼은 목마 앞으로 점프(006)가 있어야 시험할 수 있다 — 버튼 자체는 이 작업, 목마 연결 확인은 006 이후.
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md` · 규칙: `Harness/Project/Decisions/tightrope-course-rules.md`

## Decisions

- Open: 수직 톱날 버튼의 형태(버튼/발판)·위치·눌림 조건, 담당자 확정(현재 임시 @MoHoDu)
- Decided: 2026-10-03 @MoHoDu — 톱날은 이 새 Task가 맡는다(권유서 §5). 규칙·수치는 `tightrope-course-rules.md` #6~#8
