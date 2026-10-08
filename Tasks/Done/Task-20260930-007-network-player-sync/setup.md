# Setup

PM이 Unity에서 테스트 씬과 폴더를 직접 만든 뒤 작성한다.
작업자와 AI는 **작업 구역**과 **배정된 공용 파일**만 수정한다. `verify-scope`가 이 파일의 백틱(`) 경로를 읽어 검사한다.
경로는 백틱으로 감싼다. 폴더는 `/`로 끝낸다. 예: `Assets/Scenes/Tests/CurtainOpen/`

## JIRA

이 Task와 관련된 JIRA 번호를 모두 적는다. PR의 "관련 JIRA 이슈" 칸에 자동으로 들어간다. 없으면 `없음`.

- 관련 JIRA: https://hrjoo122770.atlassian.net/browse/CC-54

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/NetworkPlayerSync/` (NetworkPlayerSync.unity, 테스트 전용 프리팹은 Prefabs 하위)
- 스크립트 폴더: `Assets/Scripts/Network/PlayerSync/`
- 이어받기(기존 코드): `Assets/Scripts/Player/` — 004 병합 완료, 소유자 처리에 필요한 최소 변경만
- 에셋 폴더: 아래 공용 파일
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

통합 기록(Integration meta)의 공용 파일 소유 표에서 이 Task에 배정된 것만 적는다.

- `Assets/Resources/Prefabs/Characters/Players/` — 플레이어 프리팹에 네트워크 적용 (004에서 이어받음)
- `Assets/Resources/Input/` — 입력 에셋 (004에서 이어받음, 필요할 때만)
- `Assets/Resources/Prefabs/UIs/Player/` — 균형 게이지가 내 캐릭터를 따르게 (004에서 이어받음)
- `Assets/Resources/Prefabs/Controllers/Network/` — NetworkManager 플레이어 프리팹 지정·네트워크 프리팹 목록 (003에서 이어받음)
- `Assets/Resources/Prefabs/Controllers/Camera/` — Cinemachine 3인칭 카메라 리그 프리팹 (신규, 2026-10-02 PM @MoHoDu 배정)

## 수정 금지

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

체크가 끝나면 `assign-task.ps1`이 준비 내용을 올리고 작업 브랜치를 만든다.

## 테스트 씬 처리

Task를 닫을 때 사람이 결정한다. 스크립트는 씬을 지우지 않는다.

- 결정: 유지 (공간별 재배치로 Assets/Scenes/Tests/MainStage/Tightrope/·Tests/Systems/에 보존)
- 결정자·날짜: PM @MoHoDu, 2026-10-08
