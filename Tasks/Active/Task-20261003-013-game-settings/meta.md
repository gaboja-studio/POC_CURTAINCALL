# Task-20261003-013

- **Title:** 조정값 세팅 파일 정리
- **Type:** refactor
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md` (조정값 표), `Docs/Domains/player-control.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** refactor/game-settings

## Goal

- 기획자가 `Assets/Resources/GameSettings/` 한 폴더(게임 기본 1개 + 묘기별 1개)에서 수치를 바꾸면 게임에 반영된다. 플레이 중 수정은 바로, 코스 모양은 재시작 때. 기존 값 그대로면 플레이 감각이 바뀌지 않는다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 005 병합(완료). **1단계 두 갈래(012·011)보다 먼저 병합한다** — Player·코스·세션 스크립트와 프리팹을 같이 고치므로, 갈래가 시작된 뒤 하면 충돌한다.
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 캡슐 높이 1.5m와 모델 맞춤 키 1.73m 불일치 — 세팅에 둘 다 두고 모델 작업(017)에서 확정
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접), 공동 테스트 3개 확정, JIRA CC-63
- Decided: 2026-10-03 @MoHoDu — 세팅 구성(게임 기본 1 + 묘기별 1 + 교체형 보상), `Harness/Project/Decisions/game-settings.md`
