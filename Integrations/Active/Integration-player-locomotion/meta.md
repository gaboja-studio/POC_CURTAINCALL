# Integration-player-locomotion

- **Feature:** 달리기·앉기 공용 조작
- **Status:** preparing
- **Branch:** integration/player-locomotion
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:**
- **Updated:** 2026-10-19

Status 순서: preparing(Task 준비) → working(작업 중) → merging(순차 병합) → testing(공동 테스트) → to-dev(dev로 PR) → done. 막히면 blocked.

## 목표

- dev에서 Start 진입 후 대기실·배치 미리보기·외줄/인간 대포 플랫폼 어디서나 LShift 홀드 달리기(1.5배)·LCtrl 홀드 앉기(0.5배, 높이 60%, 점프 금지)를 같은 규칙으로 쓸 수 있다.
- 묘기 전용 조작(줄 위, 대포 탑승·비행)은 동작 없이 입력 진입 로그만 남기며, 온라인에서 실제 앉기 자세가 공유된다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261019-059 | feat | @MoHoDu | feat/player-locomotion | - | assigned |

## 준비 브랜치

선택 사항. 준비 branch를 재사용할 때만 기록한다. 두 SHA는 부모 통합 이력의 전체 40자리 커밋 SHA이며 승인 SHA는 준비 기준 SHA를 포함한다. `Active Task`가 없거나 비어 있으면 기존 일반 통합 흐름이다. 값이 있으면 해당 Task 하나만 배정·착수한다. Task meta의 선택 `Previous Task`에는 실제 Task ID만 적는다.

| Task | 브랜치 | 준비 기준 SHA | 승인 SHA |
|---|---|---|---|

## 공용 파일 소유 표

공용 씬·프리팹·설정 파일은 이 Integration 안에서 Task 1개만 소유한다. 배정된 파일은 해당 Task `setup.md`에도 적는다.

| 파일 | 소유 Task | 이유 |
|---|---|---|
| `Assets/Resources/Input/PlayerControls.inputactions` | Task-20261019-059 | Common 맵에 Sprint(LShift)·Crouch(LCtrl) 역할 추가 |
| `Assets/Scripts/Systems/Player/PlayerInputReader.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Assets/Scripts/Systems/Player/PlayerInputFrame.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Assets/Scripts/Systems/Player/PlayerControlScheme.cs` | Task-20261019-059 | 플레이어별 신규 역할 입력 edge 상태·미정 로그 |
| `Assets/Scripts/Systems/Player/PlayerMover.cs` | Task-20261019-059 | 달리기·앉기 속도/점프/캡슐·기립 공간 검사 |
| `Assets/Scripts/Systems/Player/PlayerModelSlot.cs` | Task-20261019-059 | 앉기 외형 높이(발 고정) |
| `Assets/Scripts/Systems/Player/PlayerRagdoll.cs` | Task-20261019-059 | 분리·복구 경계의 자세 스케일 처리(필요 시) |
| `Assets/Scripts/Systems/Control/PlatformControlScheme.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Assets/Scripts/Systems/Settings/BaseGameSettings.cs` | Task-20261019-059 | 캐릭터 달리기·앉기 배율/높이 비율 |
| `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset` | Task-20261019-059 | 기본값 1.5 / 0.5 / 0.6 명시 |
| `Assets/Scripts/Systems/Camera/CameraViewDirector.cs` | Task-20261019-059 | 1인칭 고정 EyeHeight 자세 반영 |
| `Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs` | Task-20261019-059 | 실제 앉기 상태 owner-write 공유 |
| `Assets/Scripts/Systems/LevelPreview/LevelPreviewControlScheme.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Assets/Scripts/Systems/LevelPreview/LevelPreviewCamera.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Assets/Scripts/MainStage/Tightrope/Control/TightropeControlScheme.cs` | Task-20261019-059 | 줄 위 신규 역할 미정 로그만 |
| `Assets/Scripts/MainStage/Tightrope/Rope/CourseControlSwitcher.cs` | Task-20261019-059 | 해제 시 자기 Scheme만 기본 복귀 |
| `Assets/Scripts/MainStage/HumanCannon/HumanCannonControlScheme.cs` | Task-20261019-059 | 대포 전용 상태 미정 로그 |
| `Assets/Scripts/MainStage/HumanCannon/HumanCannonPilot.cs` | Task-20261019-059 | 전용 이동 진입 시 자세 정리 |
| `Assets/Scripts/Systems/Control/Editor/Tests/ControlRulesTests.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Assets/Scripts/Systems/Camera/Tests/PlayMode/CameraViewsPlayTests.cs` | Task-20261019-059 | 달리기·앉기 공용 조작 |
| `Docs/Domains/player-control.md` | Task-20261019-059 | 홀드 규칙 반영 |

## Decisions

- Open: None
- Decided: None
