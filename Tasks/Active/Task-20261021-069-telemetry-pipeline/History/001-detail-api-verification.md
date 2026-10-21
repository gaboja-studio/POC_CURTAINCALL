# Handoff

## 지금 상태
- 단계: 기반·Google 배포와 공개 API 상세 연결 완료. PM 승인 7개 파일의 관측 API 추가. 정밀 공간 지표·공동 테스트 남음.
- 동작: outbox·공유 방/런/시도·부상/사망·입력/음성·공연/협동·구매/배송/운반·의료 단계 관측, 서버 적재·집계.
- 실제 TEST: 누적 Events 159건·Batches 66개 IMPORTED. UnityWebRequest ACK 후 pending 0, 오류 없음. 이번 상세 PlayMode 원본도 Drive→Sheets 반영.
- 대시보드: 본인 전용 v3 상세 계약. 수동/30분 갱신, LIVE/TEST 분리. 추가 원천 API는 기존 계약을 사용하므로 서버 재배포 필요 없음.
- 남음: coverage.md의 구역 경계·실측 이동/능력·충돌 원인, 호스트+참가자 공동 검증. 종료 배치는 다음 실행 전송 대상.
- PR: 없음. 커밋·push 요청 없음.

## 다음 할 일
1. coverage.md의 남은 세부 구역·실측 지표 원천 계약 확인. 추가 씬/원천 소유권은 이번 7개 파일 승인 범위 밖.
2. 호스트+참가자 공동 테스트·오프라인 종료/복구·상세 통계 대조.
3. 전체 PlayMode 테스트 시간 초과 원인 확인. 사용자 제출 요청 시 PR 준비.

## PM이 확인할 것
- 확인 요청: 실제 대포 착지·손잡기 및 호스트+참가자 조작 검증. 키/마이크의 실제 조작, 정밀 구역은 자동 테스트로 완료 처리하지 않음.
- 기존 자동 변경 4개 PM 승인 원복. 이번 추적 수정은 승인된 원천 7개 파일. 기존 씬/프리팹/설정 수정 없음.
- 교훈: knowledge.md. Task-062의 광역 Harness 소유 때문에 정식 Facts 이관은 보류.
- 교훈 제안: 테스트에서 MonoBehaviour.enabled=false만으로 C# 이벤트 구독·코루틴이 멈추지 않는다. 씬 전환 fixture는 StopAllCoroutines+Destroy로 정리해 OnDestroy 구독 해제를 확인한다.

## Verification
- 구역 검사: PASS (10-21)
- 1 문서/규칙: PASS (10-21)
- 2 컴파일: PASS (10-21)
- 3 테스트: PASS (EditMode, 928/928, 10-21)
- 4 실행 로그: Start 프레임 1205→44777·현재 Console 오류 0·HTTP ACK pending 0, 웹 TEST 159건 확인. 합성 PlayMode 3/3은 실제 입력 검증 아님.
- 5 공동 테스트: SKIPPED(공간별 연결·호스트+참가자 실플레이 남음)

## 공동 테스트 결과
- 미실시. plan.md 3항목으로 진행. TEST 적재는 게임의 전체 통계 검증을 대신하지 않음.

## 상세 API 검사
- Node 계약/서버 19/19, 전용 PlayMode 3/3(로비·연속 부상·외줄 정산·대기실 구매/실패/배송/수령/보관).
- 실패 코드는 원천 enum, 동일 거절 2회는 2건. 배송은 원장 갱신 직후 available 미발행·낙하 완료 1회, 주문 flowId 끝까지 일치.
- 최초 PlayMode 2/3: 이전 테스트의 자동 Lobby 로드가 접속 종료와 충돌. 게임 코드는 바꾸지 않고 fixture의 씬 전환 객체 정리 후 3/3.
- 대포 실제 설정 구간 쿼리 EditMode 확인. 착지·손잡기 콜백은 컴파일/연결 확인이며 실제 협동 물리 성공을 대신하지 않음.
- Google 실제 체인: PURCHASE_COMPLETED→DELIVERY_DISPATCHED→DELIVERY_AVAILABLE→ITEM_COLLECTED→ITEM_STORED 모두 flowId 일치. 배출→수령 가능 462ms. 실패 코드·HOST/LOCAL 원문 일치. 종료 배치는 다음 실행 대상.
