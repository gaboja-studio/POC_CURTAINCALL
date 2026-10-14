# Task-20261015-041

- **Title:** 인간 대포 2단계 — 봉과 필수 톱관문
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @RagSpirin
- **Domain:** MainStage / HumanCannon
- **Current Skill:** scaffold-task
- **Updated:** 2026-10-15

## Workspace

- **Integration:** integration/main-stage-human-cannon
- **Branch:** feat/human-cannon-pole-saw-gate
- **Previous Task:** Task-20261015-040
- 예약 단계이며 현재 Active Task는 040뿐이다. 기존 feat 브랜치도 실인계가 아니며 scaffolded에서 착수할 수 없다.

## Goal

- 봉 F 잡기·W/S 반동·놓기 도약과 연결 톱 수납/제한시간/예고 재동작을 검증한다.
- 필수 관문 열기→지상 통과→다시 받기와 지상 담당 사망 후 반동 중 F 손잡이 인계를 외팔 조건에서도 검증한다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 준비 사실은 폴더·빈 씬(root 0)뿐이다. 인간 대포 기능 구현·공동 테스트는 미실행이다.
- 앞 단계 integrated·실제 4인 공동 테스트 근거·소유권 반납·실제 JIRA URL을 PM이 확인해야 다음 1개에만 Active Task/Allowed를 부여한다.
- 앞 단계 완료 후 최신 부모 이력을 받고 승인된 준비 브랜치에 배정 문서를 Merge commit으로 sync한다. 실제 전달 PM 확인 이후만 start-work한다.

## Decisions

- 기준: cooperative-route.md — 봉은 잡고 있는 동안 톱 자동 수납, 별도 F hold 유지 불필요; 놓기/시간 끝/사망 뒤 예고 재동작.
- 공용 부상·관전·종료는 읽기/재사용만 허용하며 수정은 별도 소유 Task가 필요하다.
- Decided: CC-127 연결·2단계 제목 확인 완료. 실제 전달·소유권 이관은 별도 대기.
- Open: 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 후속은 미정이며 구현 목표에서 제외한다.
