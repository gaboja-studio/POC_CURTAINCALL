# Task-20261014-038

- **Title:** 공용 1인칭·3인칭 카메라 시점과 전환
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 외줄 코스·플레이어 동기화 참고
- **Current Skill:** start-work
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/common-camera-views

## Goal

- 1인칭·3인칭 시점이 공용 카메라 기능으로 있고, 각 공간이 공용 코드를 고치지 않고 씬에 놓는 규칙(구역 진입·상황 등)으로 시점을 바꿀 수 있다. 외줄타기 화면 구도·관전·가림 흐리기는 전과 똑같다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 시점 전환 규칙의 종류(구역 진입, 몸 상태, 시간대 등)와 우선순위·전환 연출(바로/부드럽게)은 작업 시작 때 제안 후 PM 확인
- Decided: 2026-10-14 PM: 카메라는 1인칭/3인칭 시점 로직을 모두 공용으로 만든다. 나중에 일정 규칙에 따라 각 공간의 특정 상황에서 시점을 쉽게 바꿀 수 있어야 한다. 034(개발자 모드)·037(조작 정리)과 병렬
- Decided: 2026-10-14 PM: 외줄 전용 카메라 구도(`TightropeCameraFraming`)는 공연장으로 옮기고, 공용 가림 흐리기는 외줄 코스·세팅을 직접 읽지 않게 한다(Confluence 19398663 6장)
