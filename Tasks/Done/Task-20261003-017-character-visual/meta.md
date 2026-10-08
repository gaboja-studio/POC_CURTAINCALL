# Task-20261003-017

- **Title:** 캐릭터 모델·애니메이션 적용
- **Type:** feat
- **Status:** done
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/player-control.md`, `Docs/Domains/player-sync.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-08

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/character-visual (인계 때 생성)

## Goal

- 디폴트 캡슐 대신 캐릭터 모델이 들어가고, 걷기·점프·옆줄 점프·균형 기울기·목마·추락이 애니메이션으로 모두에게 같게 보인다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: **에셋 대기(2026-10-03 아트 미준비)**. 006·010(목마 자세) 병합 후, 013 이후. 모델 교체 구조(004 `PlayerModelSlot`)는 이미 있다.
- 조정값은 `Assets/Resources/GameSettings/`에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: 모델·애니메이션 출처(아티스트 / 에셋 스토어), 도착 일정
- Open: 캡슐 높이 1.5m와 모델 키 1.73m 맞춤(013에서 넘어옴)
