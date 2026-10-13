# Task-20261013-032

- **Title:** 공간 연결과 통합 확인
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 연결 Task
- **Current Skill:** start-work
- **Updated:** 2026-10-13

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/space-integration-check (생성됨, Unity 준비 후 인계)

## Goal

- 런 진행·부상/생존·보상/공동 자금 상태가 공간(공연장·대기실·백스테이지·탈출로)을 오가도 이어지고, 하루 흐름 전체가 한 번에 돌아가는지 확인할 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 공간 담당자 소유 파일에 연결 코드가 필요하면 어느 파일을 이 Task에 배정할지, 공간 전환 방식(씬 전환 등)은 PM·공간 담당자와 결정
- Decided: 연결 Task다. 각 공간 내부 기능은 구현하지 않고 연결·통합 확인만 한다. 선행: CC-107~110(Task-029·028·030·031) 병합 후 인계
