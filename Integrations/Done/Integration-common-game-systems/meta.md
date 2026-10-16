# Integration-common-game-systems

- **Feature:** 공통 게임 시스템
- **Status:** to-dev
- **Branch:** integration/common-game-systems
- **Base:** dev
- **PM:** @MoHoDu
- **Merge Time:** 2026-10-15 완료분 dev 중간 반영, 최종 종료는 남은 Task 완료 후
- **Updated:** 2026-10-16

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- 런(1~3일차 + 4일차 아침)의 공연·쉬는 시간·밤 정비 진행, 부상·생존, 공연 결과·공동 자금, 장소 전환을 공통 시스템으로 만들고 더미 공간으로 런 한 바퀴를 검증한다. 실제 공간 교체는 이후.
- 작업 순서·병렬 구성: `plan.md` (Task-029에서 작성, 029 병합 후 이 폴더에 들어옴)
## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261013-028 | feat | @MoHoDu | feat/campaign-flow | #59 | done |
| Task-20261013-029 | feat | @MoHoDu | feat/feature-reuse-audit | #51 | done |
| Task-20261013-030 | feat | @MoHoDu | feat/injury-survival-update | #62 | done |
| Task-20261013-031 | feat | @MoHoDu | feat/performance-reward-funds | #67 | done |
| Task-20261013-032 | feat | @MoHoDu | feat/space-integration-check | #109 | done |
| Task-20261014-033 | refactor | @MoHoDu | refactor/common-code-move | #53 | done |
| Task-20261014-034 | feat | @MoHoDu | feat/dev-mode | #60 | done |
| Task-20261014-035 | feat | @MoHoDu | feat/dummy-spaces | #106 | done |
| Task-20261014-036 | fix | @MoHoDu | fix/check-work-editor-tests | #52 | done |
| Task-20261014-037 | feat | @MoHoDu | feat/common-control-rules | #61 | done |
| Task-20261014-038 | feat | @MoHoDu | feat/common-camera-views | #66 | done |
| Task-20261015-045 | fix | @MoHoDu | fix/first-person-movement | #107 | done |

## 준비 브랜치

035는 기존 PM scaffold·배정 기록을 복원(2026-10-15). 두 SHA의 원격 통합 조상 관계·고유 구현 0개 확인. 준비 branch는 착수 허가가 아니며 배정 문서 sync·실제 전달은 별도 확인한다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|
| Task-20261014-035 | feat/dummy-spaces | 16f9adff99623cdb7863a43404c2793ae93e0afd | 84b5761fa2415cafbb1e29c7a2c9e184a4e9a192 |
| Task-20261015-045 | fix/first-person-movement | 03f4d5c070f3075e6a19c1acc402287bae1069e0 | 03f4d5c070f3075e6a19c1acc402287bae1069e0 |

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Scripts/Systems/Settings/`, `Assets/Resources/Settings/GameSettings/` | ~~Task-20261013-028 (1단계)~~ 반납 (#59 병합) → 2단계는 아래 파일별 배정 | 세팅 5묶음 틀 완료 |
| `Systems/Settings/BaseGameSettings.cs`, `Resources/Settings/GameSettings/BaseGameSettings.asset` | ~~Task-20261013-030 (2단계)~~ 반납 (#62 병합, 2026-10-14) | 신체 손상 묶음: 점프 높이 배율·탐색 중 부상 패널티 값(3공간 공통)·`BodyRestartRecovery` 폐기 표시 |
| `Systems/Settings/WaitingRoomSettings.cs`·`ExplorationSettings.cs`·`EscapeRouteSettings.cs` + 같은 이름 `.asset` | Task-20261014-035 (2단계) | 더미 공간 값 |
| `Systems/Settings/RewardTable.cs`·`TrickSettings.cs`, `Resources/Settings/GameSettings/Tricks/Rewards/` | Task-20261013-031 (2단계) | 보상 규칙 구현 |
| `Systems/Settings/GameSettings.cs`, `Systems/Campaign/` | 잠금 (2단계 읽기·호출만) | 5묶음 연결·런 진행 완료. 고칠 일은 PM 배정 |
| `Systems/Network/Session/NetworkSessionManager.cs` | ~~Task-20261014-034 (2단계)~~ 반납 (#60 병합, 2026-10-14) | 최소 인원 예외 완료(`MinPlayers` → `DevMode.MinPlayers`). 개발자 모드 스위치는 `Systems/DevMode/` |
| `Assets/Scripts/MainStage/Tightrope/Damage/`, `Assets/Scripts/MainStage/Tightrope/Results/` | ~~Task-20261014-033 (1단계)~~ 반납 (#53 병합, 2026-10-14) | 공용 코드 공통 폴더 이전 완료. 이후 공연장 소유로 돌아감(031은 Results를 읽기만) |
| `Assets/Scripts/Systems/Damage/`, `NetworkPlayer.cs`·`PlayerState.cs`·`PlayerCondition.cs`·`PlayerMover.cs` | ~~Task-20261013-030 (2단계)~~ 반납 (#62 병합, 2026-10-14) | 부상·생존의 중심 |
| `Systems/Player/PlayerController.cs`·`PlayerInputReader.cs`·`PlayerControlScheme.cs`·`PlayerCommandDebugHud.cs`·`TightropeControlScheme.cs`, 새 `Systems/Control/`·`Resources/Settings/Control/`, 공연장 `PlatformControlScheme.cs`·`CourseControlSwitcher.cs`·`TightropeInputGate.cs`·`TightropeUIFocus.cs` | ~~Task-20261014-037 (2단계 병렬)~~ 반납 (#61 병합, 2026-10-14) | 공용 조작의 외줄 의존 정리·기본 이동 규칙. 공연장 파일은 공간 작업 전 PM 진행 |
| `Systems/Camera/`, `Systems/Network/PlayerSync/LocalPlayerCamera.cs`, `Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab`, 새 `Resources/Settings/Camera/` | ~~Task-20261014-038 (2단계 병렬)~~ 반납 (2026-10-15 공동 테스트 PASS) | 공용 시점·관전 완료, 기존 배정은 Task setup 이력 보존 |

| `Systems/Control/PlatformControlScheme.cs`, `Systems/Control/Editor/Tests/ControlRulesTests.cs`, `Systems/Camera/Tests/PlayMode/CameraViewsPlayTests.cs`, FirstPersonMovement 전용 테스트 씬·스크립트 폴더 | Task-20261015-045 | 반환된 037·038의 최소 수정·회귀, CC-131. Player·035 범위는 제외 |
| `Systems/Camera/CameraViewPreset.cs`·`CameraViewDirector.cs`, 새 `Resources/Settings/Camera/FreeThirdPerson.asset` | Task-20261015-045 | 2026-10-15 PM 범위 확대: 3인칭 비고정(마우스 시선·궤도 카메라). 기존 프리셋 에셋·LocalPlayerCamera·카메라 프리팹은 제외 |
| `Systems/Settings/BaseGameSettings.cs`, `Resources/Settings/GameSettings/BaseGameSettings.asset`, `ProjectSettings/EditorBuildSettings.asset` | Task-20261013-032 | 2026-10-16 PM: 씬 전환 방식(통째 교체/추가 로드) 공통 세팅 "장소 전환" 묶음, SpaceLink·더미 4개 빌드 목록 추가 |

## Decisions

- Open: 없음 (2단계 연결 약속·공용 파일 배정 확정)
- Decided: 2026-10-16 PM — 032(#109) 병합 후 dev로 마감. 035·045 남은 공동 테스트는 기존 확인으로 승인(미실행 항목은 PASS 아님, 후속 공동 테스트). 031·032·035·045 테스트 씬 유지. 교훈 5건 기록(18124cc). #109는 Merge commit으로 병합됨(규칙상 Squash, 내용 동일해 유지)
- Decided: 2026-10-14 PM — 쉬는 시간 B안 기본·A/B 전환, 기물 소지 2/1/+2 세팅값, 세팅 5묶음, 공통 폴더 이전 별도 Task(033), 더미 공간 우선(035), 생존 2명 미만 묘기 실패, 게임 시작 후 재접속 불가(로비 허용), 최소 인원 2명·개발자 모드(034) 먼저·테스트 AI는 이후(run-rules.md)
- Decided: 2026-10-14 PM — 033 병합 후 통합을 닫지 않고 dev에 중간 반영(merge commit), dev → 공간 통합 4곳 반영. 공간 작업자가 `Systems/Damage/` 새 경로로 시작하게 하기 위함
- Decided: 2026-10-14 PM — 028 확인 사항: B안 타이머 팀 공용·첫 사람이 나올 때 시작(안 나오면 멈춤, 플레이테스트 후 재논의 가능), 시간대 신호 = `CampaignRun` 메서드 + `StepChanged`(연결 약속 중 시간대 알림 확정), 최소 인원 2 유지·034 먼저(run-rules.md #6~8)
- Decided: 2026-10-14 PM — 2단계 연결 약속: (1) 공연 결과 — 묘기가 종료를 알리면 031이 결과(성공/실패·종료 이유·사망자·팔다리 변화·정산액)를 확정·공유한 뒤 `CampaignRun.EndPerformance()`를 부른다(결과 먼저 → 시간대 전환). (2) 부활 — 030이 `CampaignRun.StepChanged`를 직접 받아 쉬는 시간·밤 정비 시작 때 사망자를 외팔로 부활, 새 세대 도입 때 전원 복구. 외팔 설정 함수는 공개(035·032 호출), 대기실 이동은 032. (3) 공용 파일은 파일별 1주인(위 표), 병합 순서 034 → 030 → 031 → 035
- Decided: 2026-10-14 PM — 공용 코드의 남은 외줄 의존 정리(Confluence 19398663 6장): 037 조작(외줄 플랫폼 이동 = 공용 기본 이동 규칙, 에디터에서 전환, 공간 규칙은 공용 수정 없이 연결, UI 입력 막기 공용화)·038 카메라(1인칭·3인칭 공용 + 상황별 시점 전환)를 034와 병렬로 진행. 균형은 외줄 전용으로 유지. 걷기 속도·점프 높이 공통 세팅 이전은 030에 포함. 038은 037 조작 규칙을 고치지 않고 바라보는 방향만 공개(연결 약속)
- Decided: 2026-10-15 PM — 공간 작업 시작을 위해 028·030·034·037·#63을 dev에 중간 반영(Merge commit), 이전 마지막 반영 방침의 예외. 통합은 유지하고 038·031·035·032는 이후 제출. 036은 dev #54 반영 완료를 근거로 Done 이동·소유권 반납. 기능별 테스트 씬은 총 테스트 씬 마련 후 일괄 처리하는 기존 방침 유지
- Decided: 2026-10-14 PM — 병합 순서 변경: 037 → 030 → 038 → 031 → 035 → 032(034는 완료). 037·030을 먼저 동시 진행, 038·031을 그다음 동시 진행(동시 2개까지). 038은 037의 조작 규칙 구조 위에서 1인칭 이동을 확인하고, 035는 앞의 넷을 모두 쓰기 때문
