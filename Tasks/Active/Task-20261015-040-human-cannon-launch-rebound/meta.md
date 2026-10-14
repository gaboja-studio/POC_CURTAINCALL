# Task-20261015-040

- **Title:** 인간 대포 1단계 — 발사와 트램폴린 받기
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @RagSpirin
- **Domain:** common-game-systems
- **Current Skill:** handoff-to-member
- **Updated:** 2026-10-15

Status 순서: scaffolded → assigned(기술 배정, 실제 전달 별도) → working → submitted → integrated(공동 검증 통과) → done(dev 반영). 막히면 blocked.

## Workspace

- **Integration:** integration/main-stage-human-cannon
- **Branch:** feat/human-cannon-launch-rebound
- **Previous Task:**

## Goal

- 첫 받기 위치에서 시작하는 장애물 없는 4인 기본 묘기로 발사·수평 비행·이동/점프 트램폴린 받기를 검증한다.
- 작업 PR의 대상은 인간 대포 통합이며 Squash한다. 기능 구현은 아직 시작하지 않았다.

## Scope

- `setup.md`의 구역만 수정한다. 같은 테스트 씬은 이 Task만 소유한다.
- AI Setup Allowed: None

## Decisions

- Open: 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 검증은 미정/후속.
- Decided: Active Task는 이 Task 하나. 배정 문서 수신·PM 실제 전달 확인 전 착수 금지.
- Decided: CC-126 URL 연결·1단계 제목 확인 완료. CC-124는 상위 범위/실행 기준.
- Decided: 분리 출발 장치는 3단계. 공용 부상·관전·공연 종료 코드는 별도 소유 Task로 연결한다.
