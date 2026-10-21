# Setup

PM(@MoHoDu) 직접 작업. 사용자가 지정한 현재 프로젝트에서 진행한다.

## JIRA
- 관련 JIRA: 없음 (PM 직접 요청, 순차 gate 통합 아님)

## 작업 구역
- `Assets/Scripts/Systems/TelemetryPipeline/`
- `Assets/Scripts/Systems/TelemetryPipeline.meta`
- `Scripts/Telemetry/` — PM Google 서버·검증 코드
- `Docs/Log-system/`
- `Docs/Domains/log-system.md`
- `Harness/Project/Guides/Log-system/` — 2026-10-22 PM 요청: 비개발 기획자용 Windows·Mac 운영 안내
- Task 문서: 이 Task 폴더 (자동 포함)

## 배정된 공용 파일
- 2026-10-21 PM 채팅 승인: 아래 파일에 로그용 공개 조회·확정 결과 이벤트만 추가한다. 다른 Task의 게임 판정·가격·밸런스·씬 구성은 변경하지 않는다.
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonCourseSections.cs`
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonSystem.Landing.cs`
- `Assets/Scripts/WaitingRoom/ShopStorage/ShopDeliverySlot.cs`
- `Assets/Scripts/WaitingRoom/ShopStorage/WaitingRoomShop.cs`
- `Assets/Scripts/WaitingRoom/ShopStorage/PropCarry.cs`
- `Assets/Scripts/WaitingRoom/ExpertService/ExpertService.cs`
- `Assets/Scripts/MainStage/Tightrope/Piggyback/PiggybackSystem.cs`
- 2026-10-21 PM 추가 승인: 아래 5개 원천에 확정 관측 이벤트만 추가한다(detail-source-contracts.md).
- `Assets/Scripts/Systems/Campaign/RunState.cs`
- `Assets/Scripts/Systems/Campaign/CampaignRun.cs`
- `Assets/Scripts/WaitingRoom/Base/WaitingRoomSpawn.cs`
- `Assets/Scripts/WaitingRoom/ExpertService/ExpertRoomView.cs`
- `Assets/Scripts/MainStage/HumanCannon/HumanCannonHazardCourse.cs`

## 수정 금지
- `Assets/Scenes/`
- `Assets/Resources/`
- `Assets/_ThirdParty/`
- `ProjectSettings/`
- `Packages/`
- 위에서 명시 승인한 파일 이외의 진행 중인 다른 Task 코드·씬·프리팹

## 빌드 차단 수정 예외
- 2026-10-22 PM 명시 승인: `Assets/_ThirdParty/ithappy/Battle_Stadium/Scripts/Demonstration/Editor/Patrol/SplinePatrolWayPointEditor.cs`의 마지막 괄호를 UNITY_EDITOR 조건 안으로 이동. 비공개 저장소 커밋·제출 및 main 반영 후 공개 gitlink만 저장한다. 외부 원문은 공개 저장소에 넣지 않는다.
- 같은 날 추가 승인: 같은 Editor 폴더의 `SplinePatrolCharacterEditor.cs` 전체와 `ObstaclePathEditor.cs`의 using UnityEditor를 UNITY_EDITOR 안으로 이동. 게임 규칙·리소스는 변경하지 않는다.

## Exclusive Assets
- 신규 코드 폴더만, 기존 씬 배치·저장 없음.
- 에디터 경로 D:/github/POC_CURTAINCALL, 6000.3.25f1. Start 씬 clean·stopped 확인.

## 인계 전 체크
- [x] 정식 Task 번호 발급 및 구역 기록
- [x] Unity로 코드 폴더 생성
- [x] 공동 테스트 3항목 계획
- [x] 컴파일·자동 테스트·Google 왕복 검증 (전체 PlayMode 시간 초과는 handoff 참조)

## 테스트 씬 처리
- 신규 씬 없음. Start부터 실제 플레이 흐름으로 공동 검증.
