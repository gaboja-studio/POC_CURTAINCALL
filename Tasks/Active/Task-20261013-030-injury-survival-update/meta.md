# Task-20261013-030

- **Title:** 부상 및 생존 상태 최신 기획 반영
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @MoHoDu
- **Domain:** 공통 게임 시스템 (`Docs/Domains/common-game-systems.md`), 플레이어 동기화 참고
- **Current Skill:** start-work
- **Updated:** 2026-10-13

Status 순서: scaffolded(PM 준비) → assigned(인계) → working(작업 중) → submitted(PR 제출) → integrated(공동 테스트 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/common-game-systems
- **Branch:** feat/injury-survival-update (생성됨, Unity 준비 후 인계)

## Goal

- 플레이어의 부상·생존 상태가 최신 부상 기획대로 바뀌고, 런 진행(다음 Day·다음 세대)에 맞춰 유지·회복되는지 테스트 씬에서 확인할 수 있다.

## Scope

- 작업 구역·공용 파일·수정 금지 목록은 `setup.md`(PM 작성)가 기준이다.
- AI Setup Allowed: None

## Decisions

- Open: 부상 단계·회복 조건·사망 판정 수치는 최신 부상 기획과 CC-107 조사 결과로 확정. 기존 플레이어 동기화 코드를 고쳐야 하면 공용 파일 배정 필요
- Decided: 부상·생존 상태와 전환만 다루며, 추격자 등 부상 원인 이벤트와 공간 연결은 CC-111에서 다룬다. 선행: CC-107(Task-029)
