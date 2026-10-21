# 단일 PC 저장·재전송·중복 적재 최소 검증

2026-10-22 사용자 요청. 두 PC 검증은 보류하고 테스트 로그 1건만 사용했다.

## 방법과 결과

- 실제 Start 씬에서 별도 임시 outbox를 주입했다. 기존 outbox/전송 설정은 메모리에 보존하고 finally에서 복원했다. PC 전체 네트워크는 변경하지 않았다.
- GAME_STARTED TEST 1건을 active.json에 저장한 뒤 큐를 다시 생성했다. batchId/appSessionId/eventId가 유지된 RECOVERY 배치를 확인했다. 실제 프로세스 강제 종료와 구분한다.
- 전송 경로를 localhost 폐쇄 포트로 바꾸고 대체 토큰만 사용했다. 업로드 실패/재시도 상태에서도 원본 바이트가 유지됐다.
- 기존 Google 설정으로 복구하자 실제 UnityWebRequest ACK를 받고 pending 파일이 제거됐다. 이후 동일 배치 원문을 한 번 더 POST해 같은 batchId/건수/SHA256 ACK를 확인했다.
- Drive 원본 검색 결과 1개. 대시보드의 지금 갱신을 두 번 실행한 후 Events의 같은 eventId는 1행, Batches의 같은 batchId는 1행 IMPORTED를 확인했다. TEST 총 이벤트는 두 갱신 모두 758건이었다.

## 식별자와 증거

- verificationId: 20f9224a-da69-4ec6-95dc-bd29303efde5
- batchId: 1d944900-a03b-482d-819f-c0088f841155
- eventId: 6503b9a9-87f9-40d3-a269-2e802e521394
- SHA256: b6ce54e078a8e20811d30efb4255ce6604d3302e2db658e0a4c15bf0e091e920
- Drive 파일: 1ptuWsAZZ3oQ_GxLgmhW38fm5vKRxYrVu. Events 756행, Batches 215행.
- .harness/local/telemetry-replay-proof.json, telemetry-replay-{editmode,playmode}-20261022.json, telemetry-replay-google-refresh.png.
- 검증용 원본은 TEST로 남겨둔다. 토큰·OAuth 상태는 보고서/원본에 저장하거나 출력하지 않았다.

## 검사 범위

- 컴파일 PASS, DurableOutbox EditMode 7/7, 격리 업로드 PlayMode 1/1(9.16초), Node 서버 6/6.
- Node에는 부분 적재 후 재시도·다른 배치의 동일 eventId·내용 충돌 거부도 포함된다. 이번 실제 요청은 같은 배치 두 번이며 부분 적재 장애를 실제 Google에 주입하지 않았다.
- 실제 실행 파일 재기동·프로세스 강제 종료·PC 전체 오프라인·두 PC 플레이는 미실행이다. 재기동의 디스크 큐 복구 경로와 전송 실패 경로만 통제해 확인했다.
- 생산 코드·Google 배포 변경 없음. 재사용 가능한 전용 테스트만 추가, 씬/리소스/설정 변경 없음. 커밋·push 없음.
