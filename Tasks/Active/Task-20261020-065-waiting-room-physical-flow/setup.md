# Setup

PM 직접 연결 수정. 기존 통합 씬·스크립트 폴더를 재사용하며 새 공간은 만들지 않는다.

## JIRA

- 관련 JIRA: 없음 (PM 직접 로컬 버그 수정, 순차 gate 통합 아님)

## 작업 구역

- `Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs`
- `Assets/Scripts/Systems/SpaceLink/Tests/PlayMode/SpaceLinkPlayTests.cs`
- `Assets/Scripts/WaitingRoom/ExplorationTimer/WaitingRoomExitWatch.cs`
- Task 문서: 이 Task 폴더 (자동 포함)

## 예약 구역

- 없음

## 배정된 공용 파일

- `Assets/Scripts/Systems/Settings/BaseGameSettings.cs`
- `Assets/Resources/Settings/GameSettings/BaseGameSettings.asset`
- `Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity`

## 수정 금지

- `Assets/Scenes/` — 위 단일 배정 씬 외 변경 금지
- `Assets/_ThirdParty/`
- `ProjectSettings/` — 기존 Vivox 설정 변경 보존, 이 Task에서 수정·저장하지 않음
- `Packages/`
- `Assets/Scripts/Systems/SpaceLink/CampaignPerformanceEntry.cs`
- `Assets/Scripts/Systems/SpaceLink/PerformanceTests/PerformanceLinkPlayTests.cs`
- `Assets/Scripts/Systems/SpaceLink/RevivalTests/PerformanceEntryJudgeTests.cs`

## Exclusive Assets

- WaitingRoomBase.unity와 BaseGameSettings.asset은 이번 로컬 연결 작업에서만 편집한다.
- 씬·설정 에셋은 대상 Unity 에디터에서 수정·저장한다. 사람이 동시에 편집하지 않는다.

## 테스트 씬 처리

- 결정: 기존 WaitingRoomBase 유지, 실제 맵과 기존 기능 연결
- 결정자·날짜: PM 요청 기반, 2026-10-20
