# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: 없음

순차 gate 통합(`Active Task` 있음)은 실제 `https://.../browse/CC-번호` URL이 필수이며 없음·미정은 배정할 수 없다.

## 작업 구역

- 테스트 씬: 없음 (새 테스트 씬을 만들지 않고 기존 Start → Lobby, 외줄·인간 대포 공간으로 확인한다)
- 스크립트 폴더: 없음 (새 폴더 없이 아래 배정된 공용 파일만 수정한다)
- 에셋 폴더: 없음
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

후속 단계의 예정 경로만 적는다. 예약은 Allowed가 아니다. PM이 앞 단계의 Allowed를 반환한 뒤 해당 단계의 작업 구역·배정된 공용 파일에만 부여한다. 소유권 이관 이력은 통합 준비 문서에 보존한다.

- 없음 (단일 Task)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- `Assets/Resources/Input/PlayerControls.inputactions` — Common 맵에 Sprint(LShift)·Crouch(LCtrl) 역할 추가
- `Assets/Scripts/Systems/Player/PlayerInputReader.cs`
- `Assets/Scripts/Systems/Player/PlayerInputFrame.cs`
- `Assets/Scripts/Systems/Player/PlayerControlScheme.cs` — 플레이어별 신규 역할 입력 edge 상태·미정 로그
- `Assets/Scripts/Systems/Player/PlayerMover.cs` — 달리기·앉기 속도/점프/캡슐·기립 공간 검사
- `Assets/Scripts/Systems/Player/PlayerModelSlot.cs` — 앉기 외형 높이(발 고정)
- `Assets/Scripts/Systems/Player/PlayerRagdoll.cs` — 분리·복구 경계의 자세 스케일 처리(필요 시)
- `Assets/Scripts/Systems/Control/PlatformControlScheme.cs`
- `Assets/Scripts/Systems/Settings/BaseGameSettings.cs` — 캐릭터 달리기·앉기 배율/높이 비율
- `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset` — 기본값 1.5 / 0.5 / 0.6 명시
- `Assets/Scripts/Systems/Camera/CameraViewDirector.cs` — 1인칭 고정 EyeHeight 자세 반영
- `Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs` — 실제 앉기 상태 owner-write 공유
- `Assets/Scripts/Systems/LevelPreview/LevelPreviewControlScheme.cs`
- `Assets/Scripts/Systems/LevelPreview/LevelPreviewCamera.cs`
- `Assets/Scripts/MainStage/Tightrope/Control/TightropeControlScheme.cs` — 줄 위 신규 역할 미정 로그만
- `Assets/Scripts/MainStage/Tightrope/Rope/CourseControlSwitcher.cs` — 해제 시 자기 Scheme만 기본 복귀
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonControlScheme.cs` — 대포 전용 상태 미정 로그
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonPilot.cs` — 전용 이동 진입 시 자세 정리
- `Assets/Scripts/Systems/Control/Editor/Tests/ControlRulesTests.cs`
- `Assets/Scripts/Systems/Camera/Tests/PlayMode/CameraViewsPlayTests.cs`
- `Docs/Domains/player-control.md` — 홀드 규칙 반영

## 수정 금지

수정 금지여도 `team-roles.md` "모든 작업자에게 허용"(빌드 씬 목록·공동 시스템 이용·게임 세팅 값·새 에셋 추가)은 허용한다.

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM 또는 프로젝트에서 지정한 에셋 관리자만(역할 예외는 외부 에셋 정책 참조)

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (없음)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (해당 없음: 기존 공간 사용)
- [x] 스크립트·에셋 폴더를 만들었다 (해당 없음: 기존 파일 수정)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다 (2026-10-19 승인 계획의 3항목)

체크가 끝나면 `assign-task.ps1`이 기술 배정 준비를 올리고 작업 브랜치를 만든다. 기존 준비 branch는 `-UsePreparedBranch`로 검증 후 배정 문서 sync PR을 만든다(자동 병합·강제 이동 없음). 실제 전달 todo는 PM이 별도로 확인한다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
