# Task-20261015-045

- **Title:** 1인칭 기본 이동의 카메라 수평 방향 적용
- **Type:** fix
- **Status:** working
- **Assignee:** @MoHoDu
- **Domain:** common-game-systems
- **Current Skill:** start-work
- **Updated:** 2026-10-15

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** fix/first-person-movement
- **Previous Task:** Task-20261014-038

## Goal

- 1인칭 기본 이동에서 카메라 수평 시선 기준 W/S 전후 이동을 확인한다. 3인칭·외줄 제약 조작은 기존 동작을 유지한다.

## Scope

- 작업 구역·공용 파일·수정 금지는 `setup.md` 기준. 정식 배정·별도 worktree 준비 후 작업자 세션에서 착수한다.
- AI Setup Allowed: FirstPersonMovement 테스트 씬 구성, FreeThirdPerson.asset 생성 (2026-10-15 PM 범위 확대)

## Decisions

- Open: 없음.
- Decided: 2026-10-15 사용자 — 이동 키를 누른 채 1인칭↔3인칭이 바뀌면 키를 모두 뗄 때까지 직전 이동 기준 유지(추천안 A). 다시 누르면 새 시점 기준. 뒤를 보고 구역을 나갈 때 재입력 시 반전되는 체감 확인 후에도 A 유지(같은 날).
- Decided: 2026-10-15 PM — 045 범위 확대: 3인칭을 고정/비고정으로 나누고, 비고정은 1인칭처럼 마우스로 전방이 바뀌는 같은 이동 로직·카메라만 3인칭 위치. 기존 기본 3인칭·외줄 구도·관전은 고정 유지(비고정은 요청한 구역만).
- Decided: 2026-10-15 PM — 검증된 내용은 씬별로 반복 확인하지 않고 기능 완성 후 사람 확인을 한 번에 진행.
- Decided: 2026-10-15 사용자 — 기능 수정이므로 fix/*, 기본 이동에도 수평 시선 적용·3인칭 유지. 소속 통합으로 Squash 제출, dev 직행하지 않음.
