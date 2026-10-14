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
- 예약 단계이며 현재 Active Task는 040뿐이다. 기존 feat 브랜치도 실인계가 아니며 scaffolded에서 착수할 수 없다.

## Goal

- 일반 공중 절단·공중/지상 송풍 수평 밀림·지상 왕복/교차 톱의 둘 이상 대응법과 다리 1/2·외팔 제약을 연결하되 반동 높이·외팔 완수 가능성을 유지한다.
- 실제 4인 약 5분 전체 코스·필수 협동 우회 방지·외팔 완수와 난도 1~3 프리셋 안내를 검증한다. 최종 밸런스값은 미확정이다.

## Scope

- 작업 구역·공용 파일·수정 금지는 setup.md가 기준이다. AI Setup Allowed: None.
- 준비 사실은 폴더·빈 씬(root 0)뿐이다. 인간 대포 기능 구현·공동 테스트는 미실행이다.
- 앞 단계 integrated·실제 4인 공동 테스트 근거·소유권 반납·실제 JIRA URL을 PM이 확인해야 다음 1개에만 Active Task/Allowed를 부여한다.
- 앞 단계 완료 후 최신 부모 이력을 받고 승인된 준비 브랜치에 배정 문서를 Merge commit으로 sync한다. 실제 전달 PM 확인 이후만 start-work한다.

## Decisions

- 기준: injury-hazards.md — 다리 1은 운반 느림/몸 점프 낮음/공중 반응 둔화, 대포·트램폴린 반동 높이 유지. 다리 2는 기어가기/낮은 손잡이/높은 몸 점프 없는 진입, 다리 1보다 쉬워지지 않음.
- 부상 3 외팔은 두 다리 제약 유지·시간이 필요한 손잡이 잡기/탑승 지연, 반동/정지 정상(반동 자체 지연 없음)·장치 이용 금지 없음·완수 가능. 공통 UI는 부상 무관.
- 공용 부상·관전·종료 수정은 별도 소유 Task다. 043의 별도 공용 종료 연결 없이 전체 종료 검증을 완료로 표시하지 않는다.
- Decided: CC-130 연결·5단계 제목 확인 완료. 실제 전달·소유권 이관은 별도 대기.
- Open: 함정 거리/속도/판정 최종값 대기. 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 후속은 구현 목표 제외.
