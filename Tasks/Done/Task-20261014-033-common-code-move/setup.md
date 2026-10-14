# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다.

- 관련 JIRA: CC-117 — https://hrjoo122770.atlassian.net/browse/CC-117

## 작업 구역

- 테스트 씬: 새로 만들지 않음. 확인용 기존 씬(읽기 전용): `Assets/Scenes/Tests/MainStage/Tightrope/TightropeDamage/TightropeDamage.unity`, `Assets/Scenes/Tests/MainStage/Tightrope/TightropeResults/TightropeResults.unity`
- 스크립트 폴더(옮겨 받는 곳): `Assets/Scripts/Systems/Damage/`, `Assets/Scripts/Systems/Results/`
- 에셋 폴더: 없음
- 지도 갱신: `Docs/Domains/common-game-systems.md`, `Docs/Domains/tightrope-course.md`, `Docs/Domains/player-sync.md`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- `Assets/Scripts/MainStage/Tightrope/Damage/` — 공연장 소유. 공용 파일을 옮겨 내보내고 남는 외줄 전용 파일의 참조만 고친다 (2026-10-14 PM, 공간 작업 시작 전)
- `Assets/Scripts/MainStage/Tightrope/Results/` — 같은 이유. 외줄 전용 부분은 남긴다

## 수정 금지

- `Assets/Scenes/` (확인용 씬은 열어서 확인만, 저장하지 않음)
- `Assets/Scripts/MainStage/` 중 위에 배정되지 않은 폴더
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM에게 요청

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-117)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨) — 033은 새 씬 없음, 기존 외줄 씬으로 확인
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`, `.meta` 생성됨)
- [x] `plan.md`에 공동 테스트 항목 초안을 적었다 (2026-10-14 PM 승인)

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 해당 없음 — 033은 테스트 씬을 새로 만들지 않음. 확인에 쓴 외줄 씬은 공연장 소유로 그대로 유지
- 결정자·날짜: PM, 2026-10-14
