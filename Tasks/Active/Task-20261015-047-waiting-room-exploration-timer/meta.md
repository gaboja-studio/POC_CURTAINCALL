# Task-20261015-047

- **Title:** 대기실 2단계 — 탐색 제한 시간(Jira 표기: 쉬는 시간 타이머)
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** start-work
- **Updated:** 2026-10-15

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-exploration-timer (기술 준비 때 생성, 실인계 아님)
- **Previous Task:**

## Goal

- 누군가 대기실에서 나온 순간 팀 공용 300초가 흐르고(B안, A안 세팅 전환), 우측 하단 표시·종료 60초 전 소집 경보(종소리·방송)와 30·10초 방송, 전원 ⑥ 진입 시 조기 시작을 4인 동기화한다.
- 0초 뒤 늦은 인원에게만 추격자(더미)가 나오고 안전지대 재진입 불가, 늦은 인원이 도착하거나 잡혀 정리된 뒤 공연 시작(잡힌 인원은 외팔 합류).

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 구역: `Assets/Scripts/WaitingRoom/ExplorationTimer/`, `Assets/Scenes/Tests/WaitingRoom/ExplorationTimer/`. 1단계 Base 구역은 읽기만 한다.

## Decisions

- 기준: CC-132 전체 개발 순서, `Harness/Project/Decisions/Waiting-room/exploration-time-limit.md`. 게임 흐름 기획과 충돌하면 게임 흐름 우선.
- Decided: 2026-10-15 PM — 60초 소집 경보(종소리·방송)·30·10초 방송 적용(기획서 4.1 개정본, dev PR #94로 하네스 결정 갱신). 11초 알림 등 나머지 4.1 미적용 목록은 유지.
- Open: 용어는 '탐색 제한 시간'(옛 쉬는 시간 타이머). 추격 세부 수치는 시스템·백스테이지 협의.
- Decided: 2026-10-15 PM — 실제 전달·인계 완료, 단계 순서 gate 해제. 앞 단계 완료·PM 보고 없이 바로 착수. JIRA는 CC-132.
- Decided: 의존 — 1단계 루프(046), 런 규칙 #1·#6, 공용 부상(CC-109). 공용 수정은 별도 소유 Task.
