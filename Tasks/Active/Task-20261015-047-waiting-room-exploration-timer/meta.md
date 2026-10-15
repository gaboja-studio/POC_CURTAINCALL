# Task-20261015-047

- **Title:** 대기실 2단계 — 탐색 제한 시간(Jira 표기: 쉬는 시간 타이머)
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** scaffold-task
- **Updated:** 2026-10-15

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-exploration-timer (기술 준비 때 생성, 실인계 아님)
- **Previous Task:** Task-20261015-046
- 예약 단계이며 현재 Active Task는 046뿐이다. 준비 브랜치 존재는 실인계가 아니며 scaffolded에서 착수할 수 없다.

## Goal

- 누군가 대기실에서 나온 순간 팀 공용 300초가 흐르고(B안, A안 세팅 전환), 우측 하단 표시·1분/30초/10초 경보, 전원 ⑥ 진입 시 조기 시작을 4인 동기화한다.
- 0초 뒤 늦은 인원에게만 추격자(더미)가 나오고 안전지대 재진입 불가, 늦은 인원이 도착하거나 잡혀 정리된 뒤 공연 시작(잡힌 인원은 외팔 합류).

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 준비 사실은 문서·예약 경로뿐이다. 기능 구현·공동 테스트는 미실행이다.
- 앞 단계 integrated·실제 4인 공동 테스트 근거·소유권 반납·이 단계 실제 JIRA 카드 URL을 PM이 확인해야 Active Task/Allowed를 부여한다.

## Decisions

- 기준: CC-132 전체 개발 순서, `Harness/Project/Decisions/Waiting-room/exploration-time-limit.md`. 게임 흐름 기획과 충돌하면 게임 흐름 우선.
- Open: CC-132 2단계의 '60초 소집 경보'는 exploration-time-limit.md의 미적용 목록과 충돌 — 착수 전 PM·담당 대조.
- Open: 용어는 '탐색 제한 시간'(옛 쉬는 시간 타이머). 추격 세부 수치는 시스템·백스테이지 협의.
- Decided: 의존 — 1단계 루프(046), 런 규칙 #1·#6, 공용 부상(CC-109). 공용 수정은 별도 소유 Task.
