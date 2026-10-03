# Task-20261003-014

- **Title:** 불(추격 불·불타는 구간)
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-fire (인계 때 생성)

## Goal

- 묘기가 시작되면 출발점 뒤에서 불이 쫓아와 닿은 사람은 사망하고, 줄 일부 구간이 시간에 맞춰 불타 쓸 수 없게 되어 후반으로 갈수록 쓸 수 있는 줄이 줄어드는 모습이 모두에게 같게 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 005(완료)·013(세팅) 병합 후. 갈래 A에서 012 톱날 다음(사망 처리 경로를 012와 같게).
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 불 추격 속도·출발 지연·불타는 구간(줄·거리·시각) — 기획 값 없음. 임시값으로 넣고 `TightropeSettings` 불 칸에서 에디터로 바꾼다(2026-10-03 PM)
- Open: 불타기 시작한 구간 위에 서 있던 사람 처리(사망 / 착지만 불가)
- Decided: 2026-10-03 @MoHoDu — 화재는 새 Task. 수치는 나중에 에디터에서 변경 가능하게
