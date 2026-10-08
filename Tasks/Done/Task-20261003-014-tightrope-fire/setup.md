# Setup

2026-10-05 PM 승인된 0.0.2 계획에 따른 순차 배정. 021 담당 세션은 설정 두 파일의 편집 점유를 해제했으며, 021 미제출 코드·배치 에셋은 수정하지 않는다.

## JIRA

- 관련 JIRA: 없음

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/TightropeFire/`
- 스크립트 폴더: `Assets/Scripts/Tightrope/Fire/`
- 에셋 폴더: `Assets/Resources/Prefabs/Objects/Obstacles/Fire/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Settings/TightropeSettings.cs` — Fire 묶음만 추가
- `Assets/Resources/GameSettings/Tricks/TightropeSettings.asset` — Fire 기본값만 저장
- `Assets/Scripts/Tightrope/Rope/TightropeRun.cs` — 도착 우선 위험 판정 이벤트·회차 번호
- `Assets/Scripts/Tightrope/Rope/TightropeCourse.cs` — 구간 끝 제외 선택 지원(기존 호출 기본값 유지)

## 수정 금지

- `Assets/Scenes/`
- `ProjectSettings/`
- `Packages/`
- 톱날·배치 편집기·021 미커밋 파일은 읽기 전용

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 기존 톱날 씬을 복사해 테스트 씬을 만들었다 (.meta 생성됨)
- [x] 스크립트·에셋 폴더를 Unity에서 만들었다 (빈 폴더 .gitkeep)
- [x] 승인된 plan.md 공동 테스트 3개를 적었다

## 테스트 씬 처리

- 결정: 유지 (공간별 재배치로 Assets/Scenes/Tests/MainStage/Tightrope/·Tests/Systems/에 보존)
- 결정자·날짜: PM @MoHoDu, 2026-10-08
