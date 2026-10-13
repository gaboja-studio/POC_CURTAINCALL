# Task-20261014-036

- **Title:** 에디터가 열린 상태에서 검사 스크립트 테스트 단계 실행
- **Type:** fix
- **Status:** submitted
- **Assignee:** @MoHoDu
- **Domain:** 하네스 검증 도구 (`Harness/Core/Policies/verification-ladder.md`)
- **Current Skill:** start-work
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** fix/check-work-editor-tests

## Goal

- Unity 에디터가 열린 상태에서 "검사해줘"를 하면 컴파일과 테스트 단계가 모두 실행되고, 테스트가 없으면 FAIL이 아니라 `NO_PROJECT_TESTS`로 기록된다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: PlayMode 테스트를 에디터 안에서 돌릴 때 비동기 대기 방식(완료 확인 폴링)은 작업 시작 때 제안
- Decided: 2026-10-14 PM — 검사 스크립트 수정은 별도 Task. 원인: `verify-unity.ps1`이 에디터 연결을 요구한 뒤 테스트는 batch `unity test`로 실행해 "another Unity instance is running"으로 항상 실패(Task-029 확인). 공통 게임 시스템 Task들이 모두 쓰므로 이 통합 안에서 먼저 고친다
