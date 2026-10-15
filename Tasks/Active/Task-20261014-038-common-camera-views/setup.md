# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: CC-122 — https://hrjoo122770.atlassian.net/browse/CC-122

## 작업 구역

- 없음 — 2026-10-15 통합 공동 테스트 PASS로 반환. Task 문서만 유지하고 기존 배정은 아래 이력으로 보존한다.

## 배정된 공용 파일

- 없음 — 2026-10-15 소유 반납. 후속 수정은 PM 재배정이 필요하다.

## 작업 구역 이력 (2026-10-15 반환)

- 테스트 씬: `Assets/Scenes/Tests/Systems/CameraViews/` (CameraViews.unity)
- 스크립트 폴더: `Assets/Scripts/Systems/Camera/`
- 에셋 폴더: `Assets/Resources/Settings/Camera/`
- 외줄 구도 받는 곳: `Assets/Scripts/MainStage/Tightrope/Camera/`
- 지도 갱신: `Docs/Domains/common-game-systems.md`, `Docs/Domains/tightrope-course.md`, `Docs/Domains/player-sync.md`
- Task 문서: 이 Task 폴더 (자동 포함)

## 공용 파일 배정 이력 (2026-10-15 반납)

아래는 구현 당시 배정이며 현재 수정 권한이 아니다.

- `Assets/Scripts/Systems/Network/PlayerSync/LocalPlayerCamera.cs` — 내 캐릭터 따라가기를 시점 전환기와 연결 (2026-10-14 PM)
- `Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab` — 카메라 리그
- 공연장 소유 파일은 고치지 않는다. 외줄 구도는 `Systems/Camera/`에서 위 받는 곳으로 옮기고, 외줄 시점 요청은 옮긴 구도 컴포넌트 안에서 한다

## 수정 금지

- `Assets/Scenes/` (외줄 확인용 씬은 열어서 확인만, 저장하지 않음. 이 Task 테스트 씬 제외)
- `Assets/Scripts/Systems/Player/`, `Assets/Scripts/Systems/Control/` (037·030 소유, 읽기만)
- `Assets/Scripts/Systems/Settings/` (`TightropeSettings` 포함, 읽기만)
- `Assets/Scripts/Systems/LevelPreview/` (맵 배치용, 참고만)
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM에게 요청

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-122)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨, 2026-10-14)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`, 2026-10-14)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다 (2026-10-14 PM 승인)

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 유지 — 전체 테스트 씬 마련 후 일괄 처리하는 기존 PM 방침 적용. 이번에 씬·전용 bootstrap을 삭제하지 않는다.
- 결정자·날짜: PM @MoHoDu, 2026-10-15 기존 통합 결정(`Integration-common-game-systems/meta.md` Decisions)
