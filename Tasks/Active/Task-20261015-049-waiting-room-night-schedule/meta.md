# Task-20261015-049

- **Title:** 대기실 4단계 — 밤 정비와 스케줄표
- **Type:** feat
- **Status:** submitted
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** start-work
- **Updated:** 2026-10-17

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-night-schedule
- **Previous Task:**

## Goal

- 둘째 공연 뒤 밤 정비: 문 잠금·타이머 정지, 스케줄표 UI, 1일차 밤 2·3일차 작성, 2일차 밤 3일차 수정(1일차 고정, 4일차 실행 불가), 다음 난이도 단계 표시.
- 호스트 확정과 전원 준비 완료로 밤 정비를 끝내고, 작성한 세트리스트대로 다음 날 공연을 호출한다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 구역: `Assets/Scripts/WaitingRoom/NightSchedule/`, `Assets/Scenes/Tests/WaitingRoom/NightSchedule/`. 1단계 Base 구역은 읽기만 한다.

## Decisions

- 기준: CC-132 전체 개발 순서, `Harness/Project/Decisions/Waiting-room/night-maintenance.md`, `schedule-board.md`. 게임 흐름 기획과 충돌하면 게임 흐름 우선.
- Decided: 2026-10-17 담당자(대기실 기획) — 미작성 처리: 다음 날 세트리스트 저장 전에는 준비 완료 불가(원문 제안). 공용 `RefreshNight`는 저장 여부를 보지 않으므로 이 구역에서 막는다.
- Decided: 2026-10-17 담당자 — 권한·동시 수정: 호스트만 편집·확정, 참가자는 실시간 보기만.
- Decided: 2026-10-17 담당자 — 중복 선택: 같은 공연 하루 2회 허용, 반복 횟수로 난도 1·2·3(게임 흐름 난도 규칙).
- Decided: 2026-10-17 담당자 — 3일차 밤 4일차 작성·확정 필수(실행 안 함, SetSetlist 미호출), 스케줄표 F 카메라 클로즈업 포함. 공동 테스트 2 "4일차 선택 불가" 문구는 PM 갱신 요청.
- Decided: 2026-10-17 담당자 — 작성 범위: Task 목표 기준. 1일차 밤 2·3일차 저장, 2일차 밤 3일차만 다시 열어 수정. schedule-board.md '저장 후 수정 불가'의 예외 — 기획 문서 반영은 PM 확인 필요.
- Decided: 2026-10-15 PM — 실제 전달·인계 완료, 단계 순서 gate 해제. 앞 단계 완료·PM 보고 없이 바로 착수. JIRA는 CC-132.
- Decided: 의존 — 3단계(048), 런 상태·공연 호출 공용 시스템. 공용 수정은 별도 소유 Task.
