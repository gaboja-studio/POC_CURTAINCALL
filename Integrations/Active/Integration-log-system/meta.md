# Integration-log-system

- **Feature:** 게임 로그·무료 Google 대시보드
- **Status:** working
- **Branch:** integration/log-system
- **Base:** dev
- **Active Task:**
- **PM:** @MoHoDu
- **Merge Time:** 미정
- **Updated:** 2026-10-21

## 목표
- Start부터 연결되는 게임 공간의 로그를 Drive 원본·Sheets 분석 데이터로 누적한다.
- Apps Script 대시보드에서 타임라인·묘기·이용·공동 자금을 수동 또는 30분마다 갱신한다.

## Tasks

| Task | 종류 | 담당 | 브랜치 | PR | 상태 |
|---|---|---|---|---|---|
| Task-20261021-069 | feat | @MoHoDu | feat/telemetry-pipeline | - | working |

## 공용 파일 소유 표

| 파일 | 소유 Task | 이유 |
|---|---|---|
| Assets/Scripts/MainStage/HumanCannon/HumanCannonCourseSections.cs | Task-20261021-069 | PM 승인: 실제 구간 공개 조회만 |
| Assets/Scripts/MainStage/HumanCannon/HumanCannonSystem.Landing.cs | Task-20261021-069 | PM 승인: 호스트 착지 결과 이벤트만 |
| Assets/Scripts/WaitingRoom/ShopStorage/ShopDeliverySlot.cs | Task-20261021-069 | PM 승인: 배출·수령 가능 이벤트만 |
| Assets/Scripts/WaitingRoom/ShopStorage/WaitingRoomShop.cs | Task-20261021-069 | PM 승인: 작업·실패 코드 이벤트만 |
| Assets/Scripts/WaitingRoom/ShopStorage/PropCarry.cs | Task-20261021-069 | PM 승인: 작업·UID·실패 코드 이벤트만 |
| Assets/Scripts/WaitingRoom/ExpertService/ExpertService.cs | Task-20261021-069 | PM 승인: 작업·실패 코드 이벤트만 |
| Assets/Scripts/MainStage/Tightrope/Piggyback/PiggybackSystem.cs | Task-20261021-069 | PM 승인: 손잡기 확정 참여자 이벤트만 |
| Assets/Scripts/Systems/Campaign/RunState.cs | Task-20261021-069 | PM 추가 승인: 실제 만료 직전 관측 콜백만 |
| Assets/Scripts/Systems/Campaign/CampaignRun.cs | Task-20261021-069 | PM 추가 승인: 만료·로컬 준비 공개 이벤트만 |
| Assets/Scripts/WaitingRoom/Base/WaitingRoomSpawn.cs | Task-20261021-069 | PM 추가 승인: 실제 배치 완료 이벤트만 |
| Assets/Scripts/WaitingRoom/ExpertService/ExpertRoomView.cs | Task-20261021-069 | PM 추가 승인: 실제 입장·복귀 이동 이벤트만 |
| Assets/Scripts/MainStage/HumanCannon/HumanCannonHazardCourse.cs | Task-20261021-069 | PM 추가 승인: 확정 절단 원인 이벤트만 |

## Decisions
- 2026-10-21 사용자: 이 통합에서 로그 시스템 구현 시작, 과금 금지.
- 2026-10-21 사용자: Drive 로그 폴더 1zLstjPM44i_aWO6hjvFfBcOSXdClrLAX 사용.
- 기술 선택: Apps Script·Drive·Sheets만 사용, 유료 서비스·결제 연결 없음.
- 신규 시트와 원본 JSON 하위 폴더 생성 승인. 실제 게임 로그와 검증용 데이터 분리.
- 작업 브랜치에서 구현 후 이 통합으로 PR. 커밋·push·병합은 별도 사용자 요청 전까지 하지 않음.
