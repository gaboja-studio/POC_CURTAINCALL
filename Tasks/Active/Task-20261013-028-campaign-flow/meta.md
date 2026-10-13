# Task-20261013-028

- **Title:** 캠페인 진행 관리
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 캠페인 진행 관리 (신규 영역; 코드 확정 후 Domain Map 추가)
- **Current Skill:** start-work
- **Updated:** 2026-10-13

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/campaign-flow (생성됨, Unity 준비 후 인계)

## Goal

- 새 런을 시작하면 세대 1·Day 1·첫 공연 단계로 시작하고, 확정된 하루 흐름에 따라 공연·자유시간·밤·다음 Day 상태로 이동한다.
- 설정된 하루 공연 수와 마지막 Day 조건에 따른 진행 종료/다음 세대 시작을 상태 전환으로 확인할 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 밤 정산 및 저장, 공간 이동·타이머·게임오버 원인과의 연결은 별도 결정/연결 Task로 분리
- Decided: 이번 Task는 캠페인 세대·Day·단계 상태와 전환 규칙만 담당하며, 공간 내부 기능·보상·기물·세이브 구현은 포함하지 않음
