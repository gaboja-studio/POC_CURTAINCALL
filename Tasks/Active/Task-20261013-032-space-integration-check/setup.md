# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다.

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다.

- 관련 JIRA: CC-111 — https://hrjoo122770.atlassian.net/browse/CC-111

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/Systems/SpaceLink/SpaceLink.unity`
- 스크립트 폴더: `Assets/Scripts/Systems/SpaceLink/`
- 에셋 폴더: 없음
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- 없음 (공간 담당자 소유 파일에 연결이 필요하면 PM이 담당자와 협의 후 배정)

## 수정 금지

- `Assets/Scenes/` (단, 위 SpaceLink 테스트 씬만 허용)
- 각 공간 담당자 작업 구역과 CC-108~110의 작업 구역 (읽기 전용)
- `ProjectSettings/`
- `Packages/`
- `Assets/_ThirdParty/` — 외부 에셋. 씬·프리팹에 가져다 배치·참조하는 것은 허용, 파일 수정·추가는 PM에게 요청

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다 (CC-111)
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트 폴더를 만들었다 (빈 폴더에는 `.gitkeep`, `.meta` 생성됨)
- [ ] CC-107~110 병합을 확인하고 공간 담당자와 연결 범위·공용 파일 배정을 합의했다
- [ ] `plan.md` 공동 테스트 항목을 확정했다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 미정 (유지 / 삭제)
- 결정자·날짜:
