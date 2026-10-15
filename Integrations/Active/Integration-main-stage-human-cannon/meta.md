# Integration-main-stage-human-cannon

- **Feature:** 인간 대포 5단계 순차 개발
- **Status:** working
- **Branch:** integration/main-stage-human-cannon
- **Base:** integration/main-stage
- **Active Task:** Task-20261015-041
- **PM:** @MoHoDu
- **Merge Time:** 작업 PR 순차 통합, 실제 4인 공동 검증은 전체 기능 통합 후
- **Updated:** 2026-10-15

## 목표

- @RagSpirin에게 Task-040~044 인계 완료·순차 실행 승인(2026-10-15 PM 확인). 040은 #93 통합 완료. 041은 코드·씬 초안 작성 후 봉 조작 확인 단계이며, 내부 기능도 하나씩 사용자 확인 후 진행한다.
- 작업 → 이 통합은 Squash, 검증한 묶음 → 부모 integration/main-stage는 Merge commit. dev 직행·빈 통합 병합·기존 close-integration 실행 금지.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261015-040 | feat | @RagSpirin | feat/human-cannon-launch-rebound | #93 | integrated |
| Task-20261015-041 | feat | @RagSpirin | feat/human-cannon-pole-saw-gate | - | working |
| Task-20261015-042 | feat | @RagSpirin | feat/human-cannon-route-shelter | - | scaffolded (순차 실행 승인 완료) |
| Task-20261015-043 | feat | @RagSpirin | feat/human-cannon-finale-result | - | scaffolded (순차 실행 승인 완료) |
| Task-20261015-044 | feat | @RagSpirin | feat/human-cannon-hazards-course | - | scaffolded (순차 실행 승인 완료) |

## 준비 브랜치

2026-10-15 승인 준비본 edf561c에서 5개 브랜치 생성·push 확인. 준비 이력은 아래에 보존한다. 현재 인계·실행 승인은 [PM 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md)이 기준이다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|
| Task-20261015-040 | feat/human-cannon-launch-rebound | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a |
| Task-20261015-041 | feat/human-cannon-pole-saw-gate | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a |
| Task-20261015-042 | feat/human-cannon-route-shelter | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a |
| Task-20261015-043 | feat/human-cannon-finale-result | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a |
| Task-20261015-044 | feat/human-cannon-hazards-course | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a | edf561ce37fd90e5bfb1e4d2eb2fd7c3a448440a |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity | Task-20261015-041 | 040 반환 후 2단계 단독 소유. 후속은 순서대로 이관 |

## 소유권 이관 이력

- 2026-10-15 초기: Task-040에 HumanCannon 스크립트/테스트 구역·빈 씬 단독 배정.
- 2026-10-15 현재 PM 확인: 040 완료·#93/8293bb9 통합, 041의 da4747c 수신 확인. 040 Allowed/씬 소유 반환 → 041 코드·테스트 구역/씬 단독 이관.
- 다음 이관: 선행 구현 통합 → 최신 부모 수신 → 이전 Allowed 반환/다음 assigned·Active Task·Allowed·씬 소유 갱신. 이 전환 문서 갱신은 담당자에게 사전 승인했으며 추가 인계 승인 불필요.
- main_stage.unity는 Task-025 소유 유지. 공용 Systems·Resources·외줄 원본은 이 통합 구역 밖이다.

## Decisions

- Decided: Task-040~044는 CC-126~130 URL 연결·각 단계 제목 확인 완료. CC-124는 상위 범위/실행 기준이다.
- Open: 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 검증은 미정/후속.
- Open: 4단계의 실제 공용 공연 종료 연결은 별도 공용 소유 Task 의존. 미연결이면 해당 통합 검증 BLOCKED, 공용 최소 생존 2명 전역 삭제/임시 우회 금지.
- Decided: [순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md) — PM이 5개 인계 완료·순차 실행을 확인. 042~044 scaffolded는 기술적 순번 대기이며 승인 대기가 아니다. 각 단계의 실제 최신 구현/문서 수신을 확인한다.
- Decided: 단계별 실제 4인 PASS 선행조건은 전체 기능 통합 후 검증으로 대체한다. 미실행 기록·최종 공동검증 의무는 유지한다. 이번 승인 기록의 원격 sync는 미실행이며 커밋/push는 별도 요청 시 수행한다.
- Decided: 부모 dev 동기화 #69 Merge commit 수신 완료. 공연장 담당 @RagSpirin, 다른 공간/과거 기록 보존.
- Decided: 최종 공연용 Resources 프리팹은 별도 소유 배정 후. 040 구현 씬은 테스트 전용(root10, 040 handoff 기준)이며 최종 씬 처리 결정은 대기한다.
