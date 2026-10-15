# Integration-waiting-room-base

- **Feature:** 대기실 베이스 기능 5단계 개발
- **Status:** preparing
- **Branch:** integration/waiting-room-base
- **Base:** integration/waiting-room
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:** 단계별 공동 검증 뒤 PM 결정 (현재 미정)
- **Updated:** 2026-10-15

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- @gyumin9048-ctrl이 대기실만의 베이스 기능(공연 왕복·탐색 제한 시간·상점/보관·밤 정비·전문가 서비스)을 CC-132 5단계로 나눠 구현·4인 검증한다. 2026-10-15 PM 결정으로 5단계 모두 동시 착수 가능하며, 단계마다 전용 구역·테스트 씬을 쓴다.
- 작업 → 이 통합은 Squash, 검증한 묶음 → 부모 integration/waiting-room은 Merge commit. dev 직행·빈 통합 병합·기존 close-integration 실행 금지.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261015-046 | feat | @gyumin9048-ctrl | feat/waiting-room-greybox-loop | - | assigned |
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
| Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity | Task-20261015-046 | 1단계 테스트 씬(waiting_room.unity 복사본) |
| Assets/Scenes/Tests/WaitingRoom/ExplorationTimer/WaitingRoomExplorationTimer.unity | Task-20261015-047 | 2단계 테스트 씬(복사본) |
| Assets/Scenes/Tests/WaitingRoom/ShopStorage/WaitingRoomShopStorage.unity | Task-20261015-048 | 3단계 테스트 씬(복사본) |
| Assets/Scenes/Tests/WaitingRoom/NightSchedule/WaitingRoomNightSchedule.unity | Task-20261015-049 | 4단계 테스트 씬(복사본) |
| Assets/Scenes/Tests/WaitingRoom/ExpertService/WaitingRoomExpertService.unity | Task-20261015-050 | 5단계 테스트 씬(복사본) |

## 소유권 이관 이력

- 2026-10-15: Task-046에 WaitingRoom/Base 구역·1단계 씬 배정. 같은 날 PM 동시 착수 결정으로 047~050에 WaitingRoom/ExplorationTimer·ShopStorage·NightSchedule·ExpertService 구역과 단계 씬을 각각 단독 배정.
- 원본 waiting_room.unity는 부모 통합의 Task-024 소유이며 이 통합의 Task는 수정하지 않는다. 복사본 → 원본 반영은 이 통합에서 PM이 나중에 별도로 정한다.

## Decisions

- Decided: 2026-10-15 PM — 대기실 담당이 작성한 CC-132 5단계를 Task-046~050으로 순차 준비. 1~5단계 모두 JIRA는 CC-132로 연결(PM 지시).
- Decided: 2026-10-15 PM — 순차 gate 해제(Active Task 비움). 2~5단계도 인계·허용 완료, 작업자는 PM 보고 없이 바로 착수. 병합 순서·충돌 정리는 PM.
- Decided: 2026-10-15 PM — ⑥·⑦은 백스테이지. 대기실 그레이박스는 ①~⑤·⑧과 대기실 문, ⑥은 테스트 씬의 집결 판정용 대체 통로만(백스테이지 연결 때 교체).
- Decided: 2026-10-15 PM — waiting_room.unity 복사본(WaitingRoomBase.unity)을 1차 테스트 씬으로 쓰고, 원본 반영은 통합 브랜치에서 나중에 정한다.
- Decided: 2026-10-15 PM — 2단계에 60초 소집 경보(종소리·방송)·30·10초 방송 적용. 하네스 결정은 dev PR #94.
- Decided: 2026-10-15 PM이 Task-046~050 모두 담당 @gyumin9048-ctrl에게 실제 전달 확인. 046 배정 sync #97, 047~050 배정 sync는 assign-task로 진행.
- Open: 세트리스트 미작성 처리(4단계 전 결정권자 합의).
