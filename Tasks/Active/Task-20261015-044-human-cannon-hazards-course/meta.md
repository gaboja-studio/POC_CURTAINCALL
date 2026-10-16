# Task-20261015-044

- **Title:** 인간 대포 5단계 — 함정과 4인 코스 검증
- **Type:** feat
- **Status:** scaffolded
- **Assignee:** @RagSpirin
- **Domain:** MainStage / HumanCannon
- **Current Skill:** scaffold-task
- **Updated:** 2026-10-15

## Workspace

- **Integration:** integration/main-stage-human-cannon
- **Branch:** feat/human-cannon-hazards-course
- **Previous Task:** Task-20261015-043
- 040은 PR #93으로 integrated. 현재 Active Task/Allowed/테스트 씬 단독 소유는 041이며 이 단계는 기술 순번 대기다.

## Goal

- 일반 공중 절단·공중/지상 송풍 수평 밀림·지상 왕복/교차 톱의 둘 이상 대응법과 다리 1/2·외팔 제약을 연결하되 반동 높이·외팔 완수 가능성을 유지한다.
- 실제 4인 약 5분 전체 코스·필수 협동 우회 방지·외팔 완수와 난도 1~3 프리셋 안내를 검증한다. 최종 밸런스값은 미확정이다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 이 단계 구현·기능 검증·공동 테스트는 미실행이다. 선행 최신 구현 수신은 단계 전환 때 확인한다.
- 2026-10-15 PM @MoHoDu가 @RagSpirin에게 040~044 인계·순차 실행을 승인했다. 추가 PM 인계·착수 승인은 불필요하다: [순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md).
- 순서 040→041→042→043→044. 선행 구현 통합 후 최신 부모 수신·Active Task/Allowed/공용 소유권의 한 단계 이관·문서 갱신을 수행한다. 공동 테스트는 전체 통합 후 실행한다.

## Decisions

- 기준: injury-hazards.md — 다리 1은 운반 느림/몸 점프 낮음/공중 반응 둔화, 대포·트램폴린 반동 높이 유지. 다리 2는 기어가기/낮은 손잡이/높은 몸 점프 없는 진입, 다리 1보다 쉬워지지 않음.
- 부상 3 외팔은 두 다리 제약 유지·시간이 필요한 손잡이 잡기/탑승 지연, 반동/정지 정상(반동 자체 지연 없음)·장치 이용 금지 없음·완수 가능. 공통 UI는 부상 무관.
- 공용 부상·관전·종료 수정은 별도 소유 Task다. 043의 별도 공용 종료 연결 없이 전체 종료 검증을 완료로 표시하지 않는다.
- Decided: CC-130 연결·5단계 제목 확인 완료. 2026-10-15 PM @MoHoDu의 @RagSpirin 담당·전체 인계·순차 실행 승인 완료; 소유권은 기술 순번에 따라 이관한다.
- Open: 함정 거리/속도/판정 최종값 대기. 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 후속은 구현 목표 제외.
