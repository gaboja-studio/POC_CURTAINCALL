# 더미 연결 상태

2026-10-15, 기준 HEAD 6efc65c. 사용자 정정·계획 승인: 흐름/변수/시스템 관계 가시화가 목적이며 실제 공간 완성은 아님. 아래 공백은 표시 구현 전체를 차단하지 않는다.

## 실제 공통 API 연결

- 공연: PerformanceRewards BeginPerformance/BeginOfflinePerformance·ObserveParticipant·FinishPerformance. 정산/공동 자금 반영 후 031이 Campaign 전환. DummyPerformance는 EndPerformance를 직접 호출하지 않음
- 준비: 독립 씬에서 시작→도입 종료→보상 명부→031 성공 결과→쉬는 시간, 이후 같은 공개 경로로 밤까지 단계 진행. snapshot 직접 주입/진행 중 런 리셋 없음
- 자금: TrySpend(amount, description)만 독립 검증. 물품 지급/유료 치료를 완료했다고 표시하지 않음
- 신체: 서버 NetworkPlayer의 ServerRestorePart·ServerCatch. 잡힘은 서버 권한·현재 ServerPlace가 공간과 맞을 때만 호출. 032의 장소 설정을 대신하지 않음
- 런: NotifyLeftWaitingRoom·NotifyAllAtStageEntrance·SetReady·StartEscape·EndEscape(bool). 028 이벤트와 변수 전후를 제한 길이 로그에 표시

## 더미 입력/규칙 미리보기

- Dummy-0/1은 031 보상 명부이며 실제 신체/밤 준비 인원이 아님. 준비 인원은 Campaign.State의 실제 값을 표시
- 더미 공연 자동 시계는 오프라인 전용. 새 명부 ID당 줄타기 TimeLimit을 시작 시 읽고 실제 deltaTime으로 감소, 만료 신호만 031에 전달. 모든 묘기의 정식 규칙/온라인 동기 시계가 아니며 씬 재로드 후 기존 명부의 시간 복원은 미지원
- 큰 시계는 실제 공통 탐색 타이머와 더미 공연 시계를 구분. 밤/탈출은 자동 제한 시간 없음. 시각 있는 상황 기록은 의미 있는 변화만 최대 24개 표시하고 최근 사용자 선택 결과와 분리
- 의료 방문 1·3일차 밤은 기획 설명. 오프라인은 PlayerCondition.NextRestore 조회, 실제 복구와 지출은 별개 입력. 비용/일정의 유료 치료 연결 없음
- 탐색 구역은 수동 입력, 이동·충돌·전원 감지 없음. A/B 안전 조건 표시와 SurvivalRules.ResolveCatch 조회는 실제 추격/신체 변경이 아님
- 탈출 행은 수동 관찰 입력. EndEscape(true/false)는 별도 수동 결론이며 실제 도착/전원 리타이어 자동 집계 아님
- 공간 세팅은 더미 전용: WaitingRoom.DummySpendAmount(요청액), Exploration.DummyLostCount(조회 손실 수), EscapeRoute.DummyInputRows(입력 행 수). 정식 가격/규칙/인원 설정 아님

## 후속 연결/미완료

1. 탐색 만료: RunState.Tick/EndRest는 즉시 공연 전환. 안전지대 밖 추격 정리/공연 대기 계약 없음. 타이머를 되돌리거나 중복 소유하지 않음. 기존 공동 테스트 2번의 해당 연결은 미완료
2. 기물: 공동 지출과 물품 지급의 원자 거래/인벤토리 계약 없음. 별도 소유 배정 필요
3. 탈출: 실제 하역장 도착/전원 리타이어 감지·집계 계약 없음. 외줄 전용 Arrived 상태 전용하지 않음
4. 씬 전환·세션 유지·데이터 전달/장소 설정은 032, 이동 수정은 045. 이 Task에서 수정하지 않음

## 검사 해석

자동 회귀는 실제 소유 씬·공개 API와 설정 반영을 검사한다. 마우스 입력/실제 서버 참가자/공동 테스트 PASS를 뜻하지 않는다. Base A/B는 테스트에서 메모리 값만 바꾸고 teardown에서 원복하며 에셋 저장하지 않는다.
