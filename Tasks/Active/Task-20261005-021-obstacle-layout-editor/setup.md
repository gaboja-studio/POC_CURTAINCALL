# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-66

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeSaws/` (012 테스트 씬 이어받기 — 편집기 확인용 오브젝트 추가 가능)
- 스크립트 폴더: `Assets/Scripts/Tightrope/Saws/` (012 이어받기: 배치 데이터·실행기 입력), `Assets/Scripts/Tightrope/Saws/Editor/` (씬 뷰 편집·인스펙터·시간 슬라이더, 에디터 전용)
- 에셋 폴더: `Assets/Resources/Prefabs/Objects/Obstacles/Saws/` (012 이어받기)
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Resources/GameSettings/` (새 장애물 배치 파일 추가, `TightropeSettings` 톱날 칸 정리)
- `Assets/Scripts/Settings/` (`TightropeSettings`에 배치 파일 연결 칸)
- 런타임 판정·동기화(012 `TightropeSaws.cs`의 절단·발판·공유)는 동작을 바꾸지 않는다. 입력 형식만 바꾼다.

## 수정 금지

- `Assets/Scenes/` (자기 테스트 씬 폴더 제외)
- `ProjectSettings/`
- `Packages/`
- `Assets/Settings/`
- `Assets/InputSystem_Actions.inputactions`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨) — 012 씬 이어받기
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`) — `Saws/Editor/` 2026-10-05 생성
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
