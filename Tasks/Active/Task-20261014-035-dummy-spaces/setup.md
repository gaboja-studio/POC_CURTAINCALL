# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다.

- 관련 JIRA: CC-119 — https://hrjoo122770.atlassian.net/browse/CC-119

## 작업 구역

- 테스트 씬 폴더: `Assets/Scenes/Tests/Systems/DummySpaces/` — 독립 씬 4개(PM 사용자 승인 2026-10-15): DummyPerformance.unity, DummyWaitingRoom.unity, DummyExploration.unity, DummyEscapeRoute.unity. 2026-10-15 Unity 에디터 API로 빈 씬 생성·저장 및 재열기 검증 완료
- 스크립트 폴더: `Assets/Scripts/Systems/DummySpaces/`
- 에셋 폴더: 없음
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- `Assets/Scripts/Systems/Settings/WaitingRoomSettings.cs`, `Assets/Scripts/Systems/Settings/ExplorationSettings.cs`, `Assets/Scripts/Systems/Settings/EscapeRouteSettings.cs` — 대기실·탐색·탈출로 세팅 칸 (2026-10-14 PM 배정, 10-15 검사 가능한 전체 경로로 정정)
- `Assets/Resources/Settings/GameSettings/WaitingRoomSettings.asset`, `Assets/Resources/Settings/GameSettings/ExplorationSettings.asset`, `Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset` — 위 세팅 에셋

## 수정 금지

- `Assets/Scenes/` (단, 위 DummySpaces 테스트 씬 폴더만 허용)
- 각 공간 폴더 `Assets/Scripts/MainStage/`·`WaitingRoom/`·`BackStage/`·`EscapeRoute/`와 `Assets/Scenes/Levels/` (실제 공간은 공간 담당 소유)
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM에게 요청

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-119)
- [x] Unity에서 테스트 씬 4개를 만들고 저장했다 (2026-10-15: 사용자 Play 종료 후 빈 씬 생성·저장·재열기 확인. 각 root 0·Missing Script 0·dirty false, .meta/GUID 생성. 빌드 설정 추가 없음)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`, `.meta` 생성됨)
- [x] `plan.md`에 공동 테스트 항목 초안을 적었다 (2026-10-14 PM 승인)

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
