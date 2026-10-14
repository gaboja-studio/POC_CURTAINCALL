# Integration-main-stage-human-cannon

- **Feature:** 인간 대포 5단계 순차 개발
- **Status:** preparing
- **Branch:** integration/main-stage-human-cannon
- **Base:** integration/main-stage
- **Active Task:** Task-20261015-040
- **PM:** @MoHoDu
- **Merge Time:** 단계별 공동 검증 뒤 PM 결정 (현재 미정)
- **Updated:** 2026-10-15

## 목표

- @RagSpirin이 4인 인간 대포를 한 단계씩 구현·검증할 작업공간을 준비한다. 현재는 문서·브랜치·빈 테스트 씬 준비이며 기능 구현이 아니다.
- 작업 → 이 통합은 Squash, 검증한 묶음 → 부모 integration/main-stage는 Merge commit. dev 직행·빈 통합 병합·기존 close-integration 실행 금지.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261015-040 | feat | @RagSpirin | feat/human-cannon-launch-rebound | - | scaffolded |
| Task-20261015-041 | feat | @RagSpirin | feat/human-cannon-pole-saw-gate | - | scaffolded |
| Task-20261015-042 | feat | @RagSpirin | feat/human-cannon-route-shelter | - | scaffolded |
| Task-20261015-043 | feat | @RagSpirin | feat/human-cannon-finale-result | - | scaffolded |
| Task-20261015-044 | feat | @RagSpirin | feat/human-cannon-hazards-course | - | scaffolded |

## 준비 브랜치

준비 커밋 push 후 같은 기준에서 5개 브랜치를 생성한다. 그다음 별도 준비 기록 커밋에 전체 40자리 준비 기준/승인 SHA를 적는다. 브랜치 존재는 배정·실제 인계가 아니다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity | Task-20261015-040 | 기본 묘기 테스트 씬 단독 소유. 후속은 예약만 |

## 소유권 이관 이력

- 2026-10-15: Task-040에 HumanCannon 스크립트/테스트 구역·빈 씬 단독 배정. Task-041~044 Allowed/공용 소유는 없음.
- 다음 이관 전: 앞 Task integrated·handoff 실제 공동 테스트 PASS/일시·참여자·재현·예상·실제 확인 → Allowed/소유 반환 → 다음 Task 한 개만 개방.
- main_stage.unity는 Task-025 소유 유지. 공용 Systems·Resources·외줄 원본은 이 통합 구역 밖이다.

## Decisions

- Decided: Task-040~044는 CC-126~130 URL 연결·각 단계 제목 확인 완료. CC-124는 상위 범위/실행 기준이다.
- Open: 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 검증은 미정/후속.
- Open: 4단계의 실제 공용 공연 종료 연결은 별도 공용 소유 Task 의존. 미연결이면 해당 통합 검증 BLOCKED, 공용 최소 생존 2명 전역 삭제/임시 우회 금지.
- Decided: Active Task 하나만 assigned/착수. 후속은 이전 실제 공동 검증·통합·소유 반환·실제 Jira URL 뒤 최신 구현/배정 문서를 Merge commit sync로 수신한다.
- Decided: 기술 배정·URL 연결·브랜치 생성과 실제 전달은 별개. 사용자 실제 전달 확인 전 모든 실전달 todo 미완료.
- Decided: 부모 dev 동기화 #69 Merge commit 수신 완료. 공연장 담당 @RagSpirin, 다른 공간/과거 기록 보존.
- Decided: 최종 공연용 Resources 프리팹은 별도 소유 배정 후. 이번 생성물은 테스트 전용이며 rootCount=0이다.
