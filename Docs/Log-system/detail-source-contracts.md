# 남은 상세 관측 계약

Task-069 후속 연결. 게임 판정은 원천이 유지하며 로그는 확정된 결과만 읽는다.

## 추가 원천 작업 구역 (PM 승인·연결)

| 파일 | 관측 계약 | 필요한 이유 |
|---|---|---|
| Systems/Campaign/RunState.cs | Tick에서 실제 만료 직전 CampaignSnapshot을 읽기 전용으로 전달 | 조기 집결과 만료 모두 다음 공연·남은 시간 0이 되어 사후 상태로 구분 불가 |
| Systems/Campaign/CampaignRun.cs | 판정 화면의 RestExpired(snapshot), LocalReadyChanged(bool) 공개 이벤트 | 만료 순간 좌표와 마지막 준비자의 즉시 다음 날 전환도 빠짐없이 관측 |
| WaitingRoom/Base/WaitingRoomSpawn.cs | 실제 SetStartPose 완료 또는 LocalSpawned 시 PlayerPlaced(player) 공개 이벤트 | 씬 진입/어댑터 첫 프레임을 실제 스폰 시각으로 오인하지 않음 |
| WaitingRoom/ExpertService/ExpertRoomView.cs | 실제 MoveLocal 성공 후 입장/복귀 위치 관측 이벤트 | 서비스 Idle은 화면 복귀 성공의 증거가 아님 |
| MainStage/HumanCannon/HumanCannonHazardCourse.cs | ContactDetected로 원인 선관측, HitConfirmed로 실제 절단 결과·장애물·검출 위치 전달 | 치명상에서 정산이 먼저 실행되어도 실제 신체/생존 상태와 결합, 판정 복제 없음 |

경로는 Assets/Scripts/ 기준. 2026-10-21 PM이 5개 관측 API 추가를 명시 승인했고 setup.md·공용 파일 표에 반영했다.
이벤트 구독자 예외가 게임 판정·위치 이동을 바꾸지 않게 각각 격리한다.

## 측정 기준

- 최종 추가: HazardCourse.ContactResolved(player,hazard,cut,position,evaluated). 같은 프레임의 추가 접촉도 NO_CUT/FRAME_LIMIT로 구분하며 피해 판정은 기존대로다. WaitingRoom 어댑터의 BREAK_ENDED는 StepChanged의 이전 쉬는 시간 문맥을 1회 보존한다.

- 스폰→구역: 실제 배치 사건부터 같은 대기실 방문의 첫 구역 진입까지 단조 시간. 재방문은 별도 방문으로 세되 첫 도착 시간을 반복 집계하지 않는다.
- 준비→입구: 준비 취소 때 기준을 해제, 마지막 준비자의 다음 날 즉시 전환에도 기준을 보존한다. 새 런·새 밤·씬 종료 때 초기화.
- 첫 퇴장→집결: TimerStarted 대신 PlayerLeft 최초 사건 사용. A안 타이머에서도 실제 첫 퇴장을 관측.
- 경보→집결: 해당 쉬는 시간의 RecallAlarm부터 실제 Gathered까지. 경보 이전 집결은 미측정.
- 만료 위치: 호스트의 실제 Tick 만료 직전 스냅샷·접속 플레이어 위치를 기록. 클라이언트 화면 타이머 0을 만료 판정으로 사용하지 않음.
- 의료 복귀: 실제 위치 이동 성공 사건. 예약 정리·씬 이탈을 성공 복귀로 표시하지 않음.
- 치료 능력: 관측 가능한 이동/점프 배율·활성 여부·소지 한도는 설정 적용 관측값이며 물리 이동/점프 실측과 구분한다.
- 통로 막힘/우회: 공개 확정 판정이 아직 없음. 좌표 정체/회전을 실패나 우회로 추측하지 않음.

기존 이벤트에 선택 payload를 추가하므로 구버전 수신 계약과 호환된다. Google 집계는 측정된 duration만 사용하고 클라이언트 간 시계를 빼지 않는다.

2026-10-22: 기존 승인 파일 ExpertService.cs의 FacilityReserved(buyer, choice, appliesOnDay, amount)는 호스트 구매 성공 후 구독자별 예외를 격리해 알린다. 의료 PhaseChanged와 별도로 기록하며 가격·구매 판정은 변경하지 않는다. 배송 구독 순서가 뒤바뀌면 어댑터가 확정 주문 스냅샷을 먼저 읽어 구매→배출 기록 순서를 보존한다.
