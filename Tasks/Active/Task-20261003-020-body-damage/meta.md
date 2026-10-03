# Task-20261003-020

- **Title:** 신체 손상(부위 절단·패널티·동기화)
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** `Docs/Domains/player-control.md`, `Docs/Domains/player-sync.md`, `Docs/Domains/tightrope-course.md`
- **Current Skill:** start-work
- **Updated:** 2026-10-03

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/tightrope-prototype
- **Branch:** feat/body-damage (인계 때 생성)

## Goal

- 톱날(012) 등이 부위 절단을 요청하면 호스트가 확정해 모두에게 같은 잘린 부위가 보이고, 팔을 잃으면 상호작용 패널티·다리를 잃으면 이동이 느려지며 어느 쪽이든 균형이 더 흔들린다. 팔 2개 또는 다리 2개를 모두 잃으면 탈락(사망)·래그돌. 묘기 재시작 때 복구된다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None
- Dependencies: 013(세팅) 병합 후 시작 — Player 스크립트·프리팹을 고치므로. 갈래 A에서 012와 함께, **012보다 먼저 병합**(012가 이 작업의 절단 요청을 부른다).
- 규칙: `Harness/Project/Decisions/tightrope-course-rules.md` #9. 바탕 코드: 004 `PlayerCondition`·`BodyPart`(준비만 됨, 프리팹 미부착·공유 없음).
- 조정값은 `Assets/Resources/GameSettings/`의 세팅 파일에 둔다(`Harness/Project/Decisions/game-settings.md`).
- 계획 전체: `Integrations/Active/Integration-tightrope-prototype/plan.md`

## Decisions

- Open: "상호작용 패널티"의 구체 내용(예: F 목마 올라타기 판정 거리·시간 증가 / 실패 확률) — 임시 규칙 + 세팅 값으로 두고 PM 확인
- Open: 이미 잃은 부위에 또 닿을 때(무시 / 같은 종류 남은 쪽 절단), 팔 1개 + 다리 1개는 생존(규칙상 같은 종류 2개만 탈락)으로 임시 구현
- Open: 잘린 부위 겉모습 — 임시로 해당 부위 숨김·표시, 실제 모델은 017
- Decided: 2026-10-03 @MoHoDu — 톱날은 즉시 사망 대신 부위 절단(규칙 #9). 패널티 크기는 세팅 값(기획 미정)
- Decided: 2026-10-03 @MoHoDu — 담당자 @MoHoDu(PM 직접) 기본 배정
