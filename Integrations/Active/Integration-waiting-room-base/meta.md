# Integration-waiting-room-base

- **Feature:** 대기실 베이스 기능 5단계 순차 개발
- **Status:** preparing
- **Branch:** integration/waiting-room-base
- **Base:** integration/waiting-room
- **Active Task:** Task-20261015-046
- **PM:** @MoHoDu
- **Merge Time:** 단계별 공동 검증 뒤 PM 결정 (현재 미정)
- **Updated:** 2026-10-15

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- @gyumin9048-ctrl이 대기실만의 베이스 기능(공연 왕복·탐색 제한 시간·상점/보관·밤 정비·전문가 서비스)을 CC-132 순서대로 한 단계씩 구현·4인 검증할 작업공간을 준비한다. 현재는 문서·브랜치·빈 테스트 씬 준비이며 기능 구현이 아니다.
- 작업 → 이 통합은 Squash, 검증한 묶음 → 부모 integration/waiting-room은 Merge commit. dev 직행·빈 통합 병합·기존 close-integration 실행 금지.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261015-046 | feat | @gyumin9048-ctrl | feat/waiting-room-greybox-loop | - | scaffolded |
| Task-20261015-047 | feat | @gyumin9048-ctrl | feat/waiting-room-exploration-timer | - | scaffolded |
| Task-20261015-048 | feat | @gyumin9048-ctrl | feat/waiting-room-shop-storage | - | scaffolded |
| Task-20261015-049 | feat | @gyumin9048-ctrl | feat/waiting-room-night-schedule | - | scaffolded |
| Task-20261015-050 | feat | @gyumin9048-ctrl | feat/waiting-room-expert-service | - | scaffolded |

## 준비 브랜치

2026-10-15 승인 준비본 a864410에서 5개 브랜치 생성·push 확인. 브랜치 존재는 배정·실제 인계가 아니다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|
| Task-20261015-046 | feat/waiting-room-greybox-loop | a86441022defe9bc68e1da40549684de1f342029 | a86441022defe9bc68e1da40549684de1f342029 |
| Task-20261015-047 | feat/waiting-room-exploration-timer | a86441022defe9bc68e1da40549684de1f342029 | a86441022defe9bc68e1da40549684de1f342029 |
| Task-20261015-048 | feat/waiting-room-shop-storage | a86441022defe9bc68e1da40549684de1f342029 | a86441022defe9bc68e1da40549684de1f342029 |
| Task-20261015-049 | feat/waiting-room-night-schedule | a86441022defe9bc68e1da40549684de1f342029 | a86441022defe9bc68e1da40549684de1f342029 |
| Task-20261015-050 | feat/waiting-room-expert-service | a86441022defe9bc68e1da40549684de1f342029 | a86441022defe9bc68e1da40549684de1f342029 |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity | Task-20261015-046 | 베이스 기능 테스트 씬 단독 소유. 후속은 예약만 |

## 소유권 이관 이력

- 2026-10-15: Task-046에 WaitingRoom/Base 스크립트·테스트 구역·빈 씬 단독 배정. Task-047~050 Allowed/공용 소유 없음.
- 이관 대기: waiting_room.unity는 부모 통합에서 Task-024 소유. 024 통합·부모 소유 반환·최신 부모 Merge commit 수신 뒤 046으로 이관.
- 다음 이관 전: 앞 Task integrated·handoff 실제 공동 테스트 근거 → Allowed/소유 반환 → 다음 Task 한 개만 개방.

## Decisions

- Decided: 2026-10-15 PM — 대기실 담당이 작성한 CC-132 5단계를 Task-046~050으로 순차 준비. CC-132는 1단계 카드이자 전체 개발 순서 기준. 2~5단계 카드는 앞 단계 검증 뒤 발급해 연결.
- Decided: Active Task 하나만 assigned/착수. 후속은 앞 단계 실제 공동 검증·통합·소유 반환·실제 Jira URL 뒤 개방.
- Open: ⑥ 무대 입구 통로·⑦ 공연 스테이지 입구는 space-layout.md(PM)상 백스테이지 쪽이나 CC-132 1단계는 ⑥을 대기실 그레이박스·집결 판정에 포함 — 046 착수 전 PM 확인.
- Open: 2단계 '60초 소집 경보' 등 CC-132 표기와 exploration-time-limit.md 미적용 목록 대조, 세트리스트 미작성 처리(4단계 전 결정권자 합의).
