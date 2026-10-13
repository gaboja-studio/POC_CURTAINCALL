# Task-20261013-031

- **Title:** 공연 결과 및 보상, 공동 자금 연결
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`)
- **Current Skill:** start-work
- **Updated:** 2026-10-13

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/performance-reward-funds (생성됨, Unity 준비 후 인계)

## Goal

- 공연이 끝나면 결과에 따라 보상이 정해지고, 그 보상이 팀 공동 자금에 쌓이는 흐름을 테스트 씬에서 확인할 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 보상액·호응 산정 방식, 공동 자금 사용처(구매)와 저장 시점은 사람 결정. 기존 `RewardTable`·플레이 기록(Telemetry)을 고쳐야 하면 공용 파일 배정 필요
- Decided: 공연 결과→보상→공동 자금 흐름만 다루고, 상점·구매 UI는 범위 밖. 선행: CC-107, CC-108(런 진행), CC-109(생존 상태)
