# Task-20261003-015

- **Title:** 결과·보상(임시, 교체 가능)
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/tightrope-course.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/tightrope-result (인계 때 생성)

## Goal

- 묘기가 끝나면(클리어/실패) 결과(성공 여부·도착 인원·걸린 시간·개인 상태)와 임시 보상이 모두에게 같게 표시되고, 보상 규칙은 세팅의 보상 파일만 갈아 끼우면 바뀐다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 005·013 병합 후. 합치기 ① 이후 갈래 A 2단계. UI 고도화(016)가 이 결과 화면을 다듬는다.
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 실패(재시작) 때도 결과를 보여줄지, 보여주면 재시작 전 몇 초
- Open: 클리어 때 `EndGame`(게임 종료)과 결과 표시 순서 — 결과를 본 뒤 종료되게
- Decided: 2026-10-03 @MoHoDu — 보상은 나중에 바뀌므로 교체형(`RewardTable` 파일 갈아 끼우기). 런 기록 저장은 하지 않는다(브리프)
