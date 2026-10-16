# Integration-main-stage-human-cannon

- **Feature:** 인간 대포 5단계 순차 개발
- **Status:** working
- **Branch:** integration/main-stage-human-cannon
- **Base:** integration/main-stage
- **Active Task:** Task-20261015-043
- **PM:** @MoHoDu
- **Merge Time:** 작업 PR 순차 통합, 실제 4인 공동 검증은 전체 기능 통합 후
- **Updated:** 2026-10-16

## 목표

- @RagSpirin에게 Task-040~044 인계 완료·순차 실행 승인. 040 #93·041 #108·042 #111 통합 완료, 043 working. 실제4인 검증은 전체 통합 후 실행한다.
- 작업 → 이 통합은 Squash, 검증한 묶음 → 부모 integration/main-stage는 Merge commit. dev 직행·빈 통합 병합·기존 close-integration 실행 금지.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261015-040 | feat | @RagSpirin | feat/human-cannon-launch-rebound | #93 | integrated |
| Task-20261015-041 | feat | @RagSpirin | feat/human-cannon-pole-saw-gate | #108 | integrated (292667c) |
| Task-20261015-042 | feat | @RagSpirin | feat/human-cannon-route-shelter | #111 | integrated (75862ef) |
| Task-20261015-043 | feat | @RagSpirin | feat/human-cannon-finale-result | - | working (0128623 수신) |
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
| Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity | Task-20261015-043 | 042 #111 통합·043 0128623 수신 후 단독 소유 |

## 소유권 이관 이력

- 2026-10-15 초기: Task-040에 HumanCannon 스크립트/테스트 구역·빈 씬 단독 배정.
- 2026-10-15 현재 PM 확인: 040 완료·#93/8293bb9 통합, 041의 da4747c 수신 확인. 040 Allowed/씬 소유 반환 → 041 코드·테스트 구역/씬 단독 이관.
- 2026-10-16: 041 #108/292667c 통합·042 dae56af 수신 확인. 기존 위임으로 041 Allowed/씬 반환 → 042 단독 소유·working. 사용자 최신 지시로 042 전체 구현, 기능별 중간 확인 대기 해제. 전환 문서는2026-10-16 다른 PC 재개용042 저장 묶음에 포함, 이 통합 원격 반영은 별도 PR 대기.
- 2026-10-16: 042 #111/75862ef 통합·043 0128623 수신(부모204be8c·dev d8b6db2 포함) 확인. 042 Allowed/씬 반환 → 043 단독 이관. 사용자는 PM의 공용 실패 규칙 수정 허용을 전달하고 생존 1명 미만 실패로 전역 변경 요청; 043 setup의 명시 파일만 추가 배정.
- main_stage.unity는 Task-025 소유 유지. 공용 Systems는 043 setup의 승인 파일만 예외, Resources·외줄 원본은 구역 밖이다.

## Decisions

- Decided: Task-040~044는 CC-126~130 URL 연결·각 단계 제목 확인 완료. CC-124는 상위 범위/실행 기준이다.
- Open: 지상 2명 규칙·중계 대포·호응/보상 수치·2~3인 검증은 미정/후속.
- Decided: 2026-10-16 사용자 전달 PM 승인으로 공용 실패 기준을 생존 2명 미만→1명 미만으로 변경한다. 실제 목적지와 공용 종료 연결은 043에서 검증하며 기준 변경만으로 완료 처리하지 않는다.
- Decided: [순차 인계 결정](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md) — PM이 5개 인계 완료·순차 실행을 확인. 042~044 scaffolded는 기술적 순번 대기이며 승인 대기가 아니다. 각 단계의 실제 최신 구현/문서 수신을 확인한다.
- Decided: 부모 dev 동기화 #69 Merge commit 수신 완료. 공연장 담당 @RagSpirin, 다른 공간/과거 기록 보존.
- Decided: 최종 공연용 Resources 프리팹은 별도 소유 배정 후. 040 구현 씬은 테스트 전용(root10, 040 handoff 기준)이며 최종 씬 처리 결정은 대기한다.
