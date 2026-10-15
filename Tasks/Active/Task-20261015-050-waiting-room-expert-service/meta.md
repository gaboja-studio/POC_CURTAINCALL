# Task-20261015-050

- **Title:** 대기실 5단계 — 전문가 서비스
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @gyumin9048-ctrl
- **Domain:** common-game-systems
- **Current Skill:** scaffold-task
- **Updated:** 2026-10-15

Status 순서: scaffolded(PM 준비) → assigned(기술 배정, 실전달 별도) → working(작업 중) → submitted(PR 제출) → integrated(검증 통과) → done(dev 병합). 막히면 blocked.

## Workspace

- **Integration:** integration/waiting-room-base
- **Branch:** feat/waiting-room-expert-service (기술 준비 때 생성, 실인계 아님)
- **Previous Task:** Task-20261015-049
- 예약 단계이며 현재 Active Task는 046뿐이다. 준비 브랜치 존재는 실인계가 아니며 scaffolded에서 착수할 수 없다.

## Goal

- 상점에서 서비스 구매 → ⑧ 전문가 대기실 문 열림 → 구매자만 입장 → 신체 복구 → 재등장을 4인 동기화한다.
- 날짜별 담당자와 '담당자 부재중' 표시: 1·3일차 밤 의료 복구, 2일차 의료 비활성.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 준비 사실은 문서·예약 경로뿐이다. 기능 구현·공동 테스트는 미실행이다.
- 앞 단계 integrated·실제 4인 공동 테스트 근거·소유권 반납·이 단계 실제 JIRA 카드 URL을 PM이 확인해야 Active Task/Allowed를 부여한다.

## Decisions

- 기준: CC-132 전체 개발 순서, `Harness/Project/Decisions/Waiting-room/night-maintenance.md`. 게임 흐름 기획과 충돌하면 게임 흐름 우선.
- Open: 전문가 서비스 상세는 별도 기획서(open-items.md 5.1) — 확정 전 임시 규칙만.
- Open: 서비스 가격·이용 횟수는 임시값.
- Decided: 의존 — 4단계(049), 3단계 상점(048), 공용 부상(CC-109). 공용 수정은 별도 소유 Task.
