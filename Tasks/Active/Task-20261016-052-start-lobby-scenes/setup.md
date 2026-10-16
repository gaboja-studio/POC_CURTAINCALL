# Setup

PM이 2026-10-16 Unity에서 씬과 폴더를 직접 만들었다. 시작·로비는 테스트 씬이 아닌 실제 게임 씬이라 `Assets/Scenes/Start_Lobby/`에 둔다(PM 결정).
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: CC-133 — https://hrjoo122770.atlassian.net/browse/CC-133

순차 gate 통합(`Active Task` 있음)은 실제 `https://.../browse/CC-번호` URL이 필수이며 없음·미정은 배정할 수 없다.

## 작업 구역

- 씬: `Assets/Scenes/Start_Lobby/` (`Start.unity` 시작 씬, `Lobby.unity` 로비 씬, 이 폴더 안 전용 Prefabs 포함)
- 스크립트 폴더: `Assets/Scripts/Systems/StartLobby/`
- 에셋 폴더: 없음 (UI 리소스가 필요하면 `Assets/Scenes/Start_Lobby/` 안에 둔다)
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

후속 단계의 예정 경로만 적는다. 예약은 Allowed가 아니다. PM이 앞 단계의 Allowed를 반환한 뒤 해당 단계의 작업 구역·배정된 공용 파일에만 부여한다. 소유권 이관 이력은 통합 준비 문서에 보존한다.

- 없음 (단일 Task)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- 없음. 빌드 씬 목록(ProjectSettings/EditorBuildSettings.asset)에 `Start.unity`·`Lobby.unity`를 추가하는 것은 '모든 작업자에게 허용'으로 한다(`Start.unity`를 0번).
- 읽기·호출만(작업 구역 아님): Assets/Scripts/Systems/Network/Session/ (NetworkSessionManager 공개 진입점), Assets/Scripts/Systems/SpaceLink/, Assets/Scripts/Systems/Campaign/. 수정이 필요하면 PM에게 구역 확대를 요청한다.

## 수정 금지

수정 금지여도 `team-roles.md` "모든 작업자에게 허용"(빌드 씬 목록·공동 시스템 이용·게임 세팅 값·새 에셋 추가)은 허용한다.

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM 또는 프로젝트에서 지정한 에셋 관리자만(역할 예외는 외부 에셋 정책 참조)

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨) — `Start.unity`, `Lobby.unity` 빈 씬(기본 카메라·조명)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다 — PM 겸 담당자 초안, 세부 화면 구성 확정 시 갱신

체크가 끝나면 `assign-task.ps1`이 기술 배정 준비를 올리고 작업 브랜치를 만든다. 기존 준비 branch는 `-UsePreparedBranch`로 검증 후 배정 문서 sync PR을 만든다(자동 병합·강제 이동 없음). 실제 전달 todo는 PM이 별도로 확인한다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 유지 — 테스트 씬이 아닌 실제 게임 씬
- 결정자·날짜: PM @MoHoDu, 2026-10-16
