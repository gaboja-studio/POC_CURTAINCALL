# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: 없음 — PM 본인 수정 Task, 순차 Active Task gate 미사용.

순차 gate 통합(`Active Task` 있음)은 실제 `https://.../browse/CC-번호` URL이 필수이며 없음·미정은 배정할 수 없다.

## 작업 구역

- 테스트 씬: 없음 — 기존 `proto_0.0.2`·`HumanCannon` 씬을 읽기 전용으로 불러 재현한다.
- 스크립트 폴더: `Assets/Scripts/Systems/SpaceLink/RevivalTests/`
- 에셋 폴더: 없음
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

후속 단계의 예정 경로만 적는다. 예약은 Allowed가 아니다. PM이 앞 단계의 Allowed를 반환한 뒤 해당 단계의 작업 구역·배정된 공용 파일에만 부여한다. 소유권 이관 이력은 통합 준비 문서에 보존한다.

- 미정

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다. 재현된 원인에 필요한 파일만 수정한다.

- `Assets/Scripts/MainStage/Tightrope/Rope/TightropeCourse.cs`
- `Assets/Scripts/MainStage/Tightrope/Rope/TightropeRun.cs` (Task057에서 반환·이관)
- `Assets/Scripts/MainStage/Tightrope/Rider/RopeLaneJumpRules.cs`
- `Assets/Scripts/Systems/Player/PlayerMover.cs`
- `Assets/Scripts/Systems/Network/PlayerSync/NetworkPlayer.cs`
- `Assets/Scripts/Systems/SpaceLink/CampaignPerformanceEntry.cs` (Task057에서 반환·이관)
- 신규 코드의 Unity 생성 `.meta` 포함.

## 수정 금지

수정 금지여도 `team-roles.md` "모든 작업자에게 허용"(빌드 씬 목록·공동 시스템 이용·게임 세팅 값·새 에셋 추가)은 허용한다.

- `Assets/Scenes/`
- `Assets/Scripts/MainStage/HumanCannon/`
- `Assets/Scripts/Systems/SpaceLink/PerformanceTests/` — Task057 소유 회귀, 실행만.
- 게임 순서·부상/부활 규칙·보상·시간·난도·카메라·UI·세팅 값.
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM 또는 프로젝트에서 지정한 에셋 관리자만(역할 예외는 외부 에셋 정책 참조)

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨) — 신규 씬 없음, 기존 씬 재사용
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 기술 배정 준비를 올리고 작업 브랜치를 만든다. 기존 준비 branch는 `-UsePreparedBranch`로 검증 후 배정 문서 sync PR을 만든다(자동 병합·강제 이동 없음). 실제 전달 todo는 PM이 별도로 확인한다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 해당 없음 — 신규 테스트 씬 없음.
- 결정자·날짜: PM @MoHoDu, 2026-10-17 계획 승인.
