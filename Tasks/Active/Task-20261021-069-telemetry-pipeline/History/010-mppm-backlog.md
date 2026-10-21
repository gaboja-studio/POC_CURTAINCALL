# MPPM 실제 플레이 미표시 조사

2026-10-22. 사용자 02:46경 MPPM 2인 플레이 후 TEST/0.1.0 총 플레이 0·이벤트 1·공연 0 제보.

## 근거

- 로컬 대기 144묶음·857사건 중 해당 실행 2개 앱 58묶음·485사건 발견. runId 13290672-50a0-ce24-5ed8-b38260ca86ea, 외줄/대포 HOST 확정 실패·스케줄 확정/준비·D2T1 기록 보존.
- Editor.log 저장 오류 및 같은 senderId·공유 active.json 확인. MPPM 동시 저장 공간 충돌과 오래된 파일 우선 전송을 분리했다.
- 같은 대기 원문의 PowerShell·UnityWebRequest POST: HTTP 200, ok=true, batchId·SHA256·acceptedEvents 일치. 요청 1건 약 3초. 원본 완료 처리 진단은 자동 승인 검토가 삭제 위험으로 거절해 격리 복사본 ACK 검사로 대체, 사용자 원본 보존.
- Start 단독 실행의 프레임 진행·설정 true·LastError null과 대기 144→136을 관측. API·token 장애 아님. Play 종료 때 미완료 전송도 중지됨.

## 수정 및 검사

- MPPM /Library/VP/ dataPath별 clients 해시 큐·설치 ID. 메인 기존 큐 유지, 보조 프로젝트 재실행 복구.
- 현재 앱에서 봉인한 배치를 과거 대기보다 먼저 선택, 성공 ACK 뒤 1초 추가 대기 제거. 원문·ID/버전·해시 검증·실패 재시도 유지.
- PendingBatchCount·LastUploadAt·LastUploadError와 개발 Console [Telemetry] 저장/대기/Drive ACK 상태 추가. 비밀값·원문 출력 없음.
- origin/dev Scope PASS, Fast PASS(기존 길이 경고), 최종 컴파일 PASS, DurableOutbox EditMode 10/10. PlayMode 전체·새 MPPM 공동 입력 재검증·두 PC 검증 SKIPPED.
- 복구 POST는 원본 58개 그대로 전송. 첫 48묶음·405건 ACK, importer 실행 중 BUSY_RETRY 10묶음은 importer 종료 뒤 재시도하여 58/58·485/485 ACK. 기존 원본은 삭제하지 않음.
- 최종 실제 화면: TEST/0.1.0 총 플레이 1·이벤트 486(이번 485+기존 검사1)·공연 2. 외줄/대포 실패·2일차 탭, 편성 확정 3.3초·확정→준비 0.4초·화면→준비 평균 2.6초 확인.

## 증거

- .harness/local/telemetry-http-probe.json, telemetry-user-run-recovery{,-retry,-final}.json, test-results-editmode.json.
- .harness/local/telemetry-mppm-recovered.png: 사용자 Chrome의 1/486/2 요약.
- 기획자 운영: Docs/Log-system/operator-guide.md. 원인·제약: Docs/Log-system/mppm-troubleshooting.md.
- 5da374f는 이전 기반 커밋이며 이번 수정은 미커밋. push·PR·병합 없음. Harness 지식 색인은 Task-062 소유 때문에 수정하지 않고 knowledge.md에 이관 제안.
