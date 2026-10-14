# Task-20261014-033

- **Title:** 공용 부상·공연 결과 코드 공통 폴더 이전
- **Type:** refactor
- **Status:** integrated
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 외줄 코스 참고
- **Current Skill:** check-work
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** refactor/common-code-move

## Goal

- 공연장 폴더에 있던 공용 부상 효과·패널티 틀·절단 연출·공연 결과 명부가 `Assets/Scripts/Systems/`에 있고, 외줄 테스트 씬(부상·결과)이 옮기기 전과 똑같이 동작한다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Decided: 2026-10-14 PM: 결과 명부 분리는 031로 넘긴다. 033은 Damage 이전만 끝낸다(경계안은 handoff). 분리하려면 구역 밖 `Rope/`·`Telemetry/`·`UI/` 수정과 구조 변경이 필요하기 때문
- Decided: 2026-10-14: 외줄 기본 정책은 공통 코드가 직접 만들지 않고 `TightropeBodyPenalty`가 게임 시작 전 `BodyDamageEffects.DefaultPolicyFactory`에 등록한다(Player 프리팹·씬 수정 없이 모든 씬에서 결과 동일)
- Decided: 2026-10-14 PM: 공간 작업자가 작업 전이므로 PM이 1단계에서 먼저 옮긴다. 동작은 바꾸지 않는다(위치·참조 정리만). 통합 계획 1B
