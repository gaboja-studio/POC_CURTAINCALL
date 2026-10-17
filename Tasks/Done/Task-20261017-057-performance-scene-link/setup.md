# Setup

## JIRA

- 관련 JIRA: 없음 — PM 본인 단일 연결 Task, 순차 Active Task gate 미사용.

## 작업 구역

- 테스트 씬: `Assets/Scenes/Tests/Systems/PerformanceLink/`
- 스크립트 폴더: `Assets/Scripts/Systems/SpaceLink/PerformanceTests/`
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일

- `Assets/Scripts/Systems/SpaceLink/SpaceRoute.cs`
- `Assets/Scripts/Systems/SpaceLink/SpaceLinkDirector.cs`
- `Assets/Scripts/Systems/SpaceLink/CampaignTightropeAdapter.cs`
- 반환 이력(2026-10-17, PR #153 병합 후 PM): CampaignPerformanceEntry.cs·TightropeRun.cs → Task-20261017-058로 이관.
- 세팅 코드/값과 빌드 목록은 team-roles의 현재 Task 관련 공용 허용 적용.
- 신규 코드의 Unity 생성 `.meta` 포함. 기존 Task052 소유 테스트 파일은 수정하지 않는다.

## 수정 금지

- `Assets/Scripts/MainStage/HumanCannon/`
- `Assets/Scenes/Tests/MainStage/HumanCannon/`
- `Assets/Scenes/Start_Lobby/`
- `Assets/Scripts/Systems/StartLobby/`
- `Assets/Scenes/Prototypes/`
- `Assets/_ThirdParty/`
- `Packages/`
- 다른 Task 문서, private/gitlink/속성, 작업 중인 enhancement 브랜치.

## 인계 전 체크 (PM)

- [x] 관련 JIRA 번호를 모두 적었다
- [x] Unity에서 테스트 씬을 만들고 저장했다 (`.meta` 생성됨)
- [x] 스크립트·에셋 폴더를 만들었다 (빈 폴더에는 `.gitkeep`)
- [x] `plan.md`에 공동 테스트 항목을 합의해 적었다

## 테스트 씬 처리

- 결정: 유지 — 연결 회귀 재현용 빈 씬.
- 결정자·날짜: PM @MoHoDu, 2026-10-17 계획 승인.
