# Task-20261014-035

- **Title:** 공통 게임 시스템 검증용 더미 공간
- **Type:** feat
- **Status:** assigned
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`)
- **Current Skill:** start-work
- **Updated:** 2026-10-14

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/dummy-spaces

## Goal

- 더미 묘기·대기실·탐색(백스테이지)·탈출로 씬에서 버튼과 구역만으로 공연 결과·쉬는 시간·밤 정비·추격·탈출 상황을 만들 수 있고, 대기실·탐색·탈출로 세팅 값을 바꾸면 더미 동작이 그대로 바뀐다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 더미 씬을 4개로 나눌지 한 씬 안 구역으로 둘지(장소 전환 032와 함께 확인)
- Decided: 2026-10-14 PM: 공통 게임 시스템은 더미 공간으로 먼저 검증하고 실제 공간 로직·씬은 나중에 교체한다. 통합 계획 2C(선행: 028 런 진행·세팅 틀)
- Decided: 2026-10-14 PM: 더미 묘기는 종료를 031에 알리고 시간대 전환은 031이 부른다. 백스테이지 잡힘은 030의 외팔 설정 함수를 호출. 탐색 중 부상 패널티 값은 030(공통 세팅) 소유. 2단계 마지막 병합
