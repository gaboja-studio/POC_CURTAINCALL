# Setup

2026-10-20 현재 대화 PM 사용자의 직접 저장 지시로 기존 공연장 제작 작업을 등록한다.

## JIRA

- 관련 JIRA: 없음

## 작업 구역

- 에셋·씬: `Assets/_ThirdParty/curtaincall/PerformanceBackground/`
- 상위 폴더 메타: `Assets/_ThirdParty/curtaincall.meta`
- Task 문서: 이 Task 폴더
- `Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity` — 2026-10-21 현재 대화 PM의 "묘기들에 맞게 공연장 적용" 지시에 따른 배경 프리팹 배치·환경/조명 연결만. 코스·캐릭터·게임 규칙 변경 제외.
- `Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity` — 같은 지시의 외줄 배경 배치·환경/조명 연결만.

## 배정된 공용 파일

- `Assets/_ThirdParty` — private 에셋을 먼저 저장하고 원격 main 포함 확인 후 gitlink 갱신.
- `Integrations/Active/Integration-main-stage/meta.md` — 이 Task 행과 포인터 소유 행만 추가.
- 위 묘기 씬 2개는 배경 적용에 한정한 후속 작업 구역이며 기존 기능 Task의 코드·기능 변경을 포함하지 않는다.

## 수정 금지

- `Assets/Scenes/`
- `Assets/Scripts/`
- `Assets/Resources/`
- `Packages/`
- `ProjectSettings/`

## 인계 전 체크 (PM)

- [x] 관련 JIRA 없음 기록
- [x] 기존 씬·에셋·메타 저장 확인
- [x] 기존 사용자 지시와 이번 PM 저장 지시 기록

## 테스트 씬 처리

- 결정: 공연장 결과 씬을 유지. 별도 테스트 씬 없음.
- 결정자·날짜: 현재 대화 PM 사용자, 2026-10-20
