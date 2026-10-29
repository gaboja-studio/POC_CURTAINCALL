# Setup

## JIRA

- 관련 JIRA: 없음 (사용자 직접 버그 제보, 외부 이슈 생성 미요청)

## 작업 구역

- `Assets/Scripts/Systems/SpaceLink/CampaignPerformanceEntry.cs`
- `Assets/Scripts/Systems/SpaceLink/RevivalTests/PerformanceEntryJudgeTests.cs`
- `Assets/Scripts/Systems/SpaceLink/PerformanceTests/PerformanceLinkPlayTests.cs`
- PM 지식 기록: `Harness/Project/Pitfalls/revived-before-performance-unload.md`, `Harness/Project/Pitfalls/index.md`
- Task 문서: 이 Task 폴더 (자동 포함)

2026-10-20 PM 직접 수정. 승인된 대기실 부활 버그 계획에 따라 위 세 파일만 이번 fix Task에 재배정한다. Task-062의 PR #188·#189 병합을 확인했으며 해당 Task 문서·사람 검증 미완료 기록·나머지 구현 범위는 수정하지 않는다. 기존 테스트 씬·폴더·어셈블리를 재사용하고 새 에셋은 만들지 않는다.

## 배정된 공용 파일

- 없음 (씬·프리팹 변경 없음)

## 수정 금지

- `Assets/Scenes/`
- `Assets/_ThirdParty/`
- `ProjectSettings/`
- `Packages/`
- 다른 Task 문서 및 위 세 파일 외 구현 (작업 구역에 지정한 PM 지식 기록은 허용)
- 커밋·push·PR 생성·병합 (별도 사용자 요청 필요)

## 인계 전 체크 (PM)

- [x] 관련 JIRA 없음 기록
- [x] 기존 PlayMode 테스트 씬·폴더 재사용
- [x] 승인된 계획과 공동 테스트 항목 기록
- [x] PM 직접 로컬 착수. 원격 배정·인계 자동화는 실행하지 않음

## 테스트 씬 처리

- 결정: 기존 씬 유지, 변경 없음
- 결정자·날짜: PM 승인 계획, 2026-10-20
