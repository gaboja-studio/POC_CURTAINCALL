# Task-20261015-041

- **Title:** 인간 대포 2단계 — 봉과 필수 톱관문
- **Type:** feat
- **Status:** working
- **Assignee:** @RagSpirin
- **Domain:** MainStage / HumanCannon
- **Current Skill:** implement-code
- **Updated:** 2026-10-15

## Workspace

- **Integration:** integration/main-stage-human-cannon
- **Branch:** feat/human-cannon-pole-saw-gate
- **Previous Task:** Task-20261015-040
- 현재 Active Task는 041. 2026-10-15 PM이 040~044 모두 @RagSpirin에게 인계 완료·순차 실행 가능함을 확인했다. 추가 착수 승인 불필요.

## Goal

- 봉 F 잡기·W/S 반동·놓기 도약과 연결 톱 수납/제한시간/예고 재동작을 검증한다.
- 필수 관문 열기→지상 통과→다시 받기와 지상 담당 사망 후 반동 중 F 손잡이 인계를 외팔 조건에서도 검증한다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 040 구현은 #93/8293bb9로 통합됐고 현재 브랜치가 da4747c로 수신했다. 040이 반환한 HumanCannon 코드·테스트 구역/씬을 이 Task에 단독 배정한다.
- [PM 순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)에 따라 진행한다. 2026-10-15 PM이 현재 041 브랜치의 다른 PC 재개용 중간 저장을 요청했다. 다른 브랜치로의 승인 문서 sync·PR은 별도다.
- AI가 전체 코드·씬 초안을 선행 작성했으나 내부 단계별 사용자 확인은 미실행. 현재 봉 조작 확인 단계이며 전체 완료가 아니다. 자동 검사 결과는 handoff 기준이다.

## Decisions

- 기준: cooperative-route.md — 봉은 잡고 있는 동안 톱 자동 수납, 별도 F hold 유지 불필요; 놓기/시간 끝/사망 뒤 예고 재동작.
- 공용 부상·관전·종료는 읽기/재사용만 허용하며 수정은 별도 소유 Task가 필요하다.
- Decided: CC-127 연결·2단계 제목 확인 완료. 2026-10-15 현재 사용자 PM @MoHoDu가 인계 완료·순차 실행을 승인하고 040→041 소유권을 이관했다.
- Decided: 2026-10-15 사용자 — 기존 테스트 씬에 기본 도형 봉·톱 관문을 추가하고 Inspector 조절 임시값으로 진행(봉 유지 8초·톱 재동작 예고 2초부터). 최종 밸런스 확정 아님.
- Decided: 2026-10-15 PM 정정 — Task 내부도 한 기능 구현→사용자 확인→다음 기능 구현. 현재 봉 조작부터, 후속은 톱 연동→필수 관문→사망 인계 순서. 전체 통합 후 4인 검증과 별개다.
- Open: 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 후속은 미정이며 구현 목표에서 제외한다.
