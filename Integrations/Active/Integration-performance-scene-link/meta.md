# Integration-performance-scene-link

- **Feature:** 실제 공연 씬 연결
- **Status:** preparing
- **Branch:** integration/performance-scene-link
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:**
- **Updated:** 2026-10-17

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 시작·로비의 기존 진행에 지정 줄타기·인간 대포를 연결하고 독립 실행을 유지한다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261017-057 | feat | @MoHoDu | feat/performance-scene-link | #153 | submitted |
| Task-20261017-058 | fix | @MoHoDu | fix/performance-revival-race | - | assigned |

## 준비 브랜치

선택 사항. 준비 branch를 재사용할 때만 기록한다. 두 SHA는 부모 통합 이력의 전체 40자리 커밋 SHA이며 승인 SHA는 준비 기준 SHA를 포함한다. `Active Task`가 없거나 비어 있으면 기존 일반 통합 흐름이다. 값이 있으면 해당 Task 하나만 배정·착수한다. Task meta의 선택 `Previous Task`에는 실제 Task ID만 적는다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Scripts/Systems/SpaceLink/SpaceRoute.cs | Task-20261017-057 | 묘기별 씬 선택 |
| Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs | Task-20261017-057 | 공연 전환 |
| Assets/Scripts/Systems/SpaceLink/CampaignPerformanceEntry.cs | Task-20261017-058 | 2026-10-17 057 병합 후 이관 — 배치 재시도 |
| Assets/Scripts/Systems/SpaceLink/CampaignTightropeAdapter.cs | Task-20261017-057 | 신규 줄타기 정산 |
| Assets/Scripts/MainStage/Tightrope/Rope/TightropeRun.cs | Task-20261017-058 | 2026-10-17 057 병합 후 이관 — 종료 공연 판정 |
| Assets/Scripts/MainStage/Tightrope/Rope/TightropeCourse.cs | Task-20261017-058 | 종료 공연 로컬 추락 판정 |
| Assets/Scripts/MainStage/Tightrope/Rider/RopeLaneJumpRules.cs | Task-20261017-058 | 순간이동 착지 오인 |
| Assets/Scripts/Systems/Player/PlayerMover.cs | Task-20261017-058 | 순간이동 착지 알림 순서 |
| Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs | Task-20261017-058 | 확정 사망 역송 |

## Decisions

- Open: None
- Decided: 2026-10-17 — PM 승인 계획으로 별도 연결 Task 하나 준비. 순차 gate/다른 작업자 실인계 없음. enhancement 브랜치 미수정.
- Decided: 2026-10-17 — 실제 2인 FAIL(부활 후 대포에서 P2 사망)로 PM 본인 수정 Task058 준비. 겹치는 파일은 057에서 반환·이관. 통합 마감·dev 반영·enhancement 전달 보류.
- Decided: 2026-10-17 — 준비 기록·빈 테스트 구역 최초 저장/push만 승인. 구현 코드 제출·병합 제외.
