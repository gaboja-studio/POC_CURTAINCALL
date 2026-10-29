# dev 수신·원격 수령 관측

2026-10-22 사용자 지시. 최신 dev 접속 수정을 먼저 수신하고 필요한 검사·다음 연결만 수행했다.

- e6da182→12267d7 FF. 방 코드/LAN 접속 제한시간 30초·취소/이전 요청 격리 수정과 외줄 가이드를 수신했다. 새 구현 커밋·push 없음.
- 기존 미커밋 88개 파일을 .harness/local/dev-sync-20261022/에 사본·SHA256 manifest로 보관. FF 직후 변경 해시 0건, 충돌 없음.
- WaitingRoomLogAdapter는 다른 플레이어의 확정 수령도 HOST_RECEIPT_SNAPSHOT으로 기록한다. 구매자와 수령자를 구분하고 같은 주문 flowId를 사용한다.
- 호스트의 DELIVERY_AVAILABLE→ITEM_COLLECTED를 같은 호스트 단조 시계로 계산한다. 실제 수령자를 찾지 못하면 호스트에게 잘못 귀속하지 않고 생략한다.
- 수령자의 LOCAL_RECEIPT_SNAPSHOT은 기존대로 보존한다. 호스트 본인 수령은 LOCAL 한 건, 다른 클라이언트의 타인 수령은 발행하지 않는다. 최초 수령 목록·동일 스냅샷은 중복 발행하지 않는다.
- 기물 이용 집계는 LOCAL만 사용하므로 HOST 복제본으로 횟수를 늘리지 않는다. Google 코드/배포/권한은 변경하지 않았다.

## 검사

- origin/dev 기준 구역 PASS, 문서/공백 PASS(기존 경고), Unity 컴파일 PASS.
- dev 핵심 접속 회귀 PendingJoinSurvivesThreeSecondsAndUserCancellationIsLogged 1/1. 실제 Relay/CODE 성공 검증은 아니다.
- 시간 전용 EditMode 7/7, 대기실 공개 API PlayMode 1/1, Node 계약/서버 23/23.
- 여러 클래스 이름을 합친 초기 필터는 NO_PROJECT_TESTS. 단일 필터로 실제 실행한 결과만 위 숫자에 포함했다.
- 합성 단위 검사에서는 호스트/수령자의 서로 다른 시계를 분리했다. 실제 원격 수령 콜백은 다인 공동 검사로 확인해야 한다.
- 결과: .harness/local/telemetry-dev-{join,flow,waiting}-20261022.json. 전체 검사·Google 신규 왕복은 반복하지 않았다.
