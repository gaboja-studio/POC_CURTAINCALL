# 대기실 이동·집결 / 의료 / 대포 원인

## 승인·환경
- PM이 원천 5개 관측 API 추가와 작업 구역 확장을 명시 승인했다. setup.md·Integration 공용 파일 표에 반영.
- Pipeline은 창 활성화만으로 살아나지 않았고 Window/Pipeline/Start Server를 사용자가 실행한 뒤 복구. Start clean·stopped로 시작.
- doctor는 이어지는 같은 Task이므로 중복 실행하지 않았다. 구현 커밋·push 없음.

## 변경
- WaitingRoomSpawn.PlayerPlaced는 실제 배치·늦은 슬롯 확정 이후 발행. 방별 첫 도착은 같은 방문의 첫 진입 1회만 단조 시간 측정.
- LocalReadyChanged는 마지막 플레이어의 즉시 다음 날 전환보다 먼저 관측. 준비 취소/새 밤에 기준 해제, 다음 아침은 유지.
- 첫 퇴장은 TimerStarted가 아닌 실제 PlayerLeft여서 A안도 수집. Gathered와 경보 시간을 같은 쉬는 시간 범위에서 결합.
- RunState.Tick 만료 직전 콜백→Campaign.RestExpired. 조기 집결·클라이언트 표시 타이머 0과 구분. EmitFor의 stepContext로 이전 breakId 보존.
- 의료 실제 SetStartPose 뒤 LocalMoved를 큐에 기록하여 MEDICAL_ENTERED/RETURNED 발행. Idle을 성공 복귀로 오인하지 않음.
- 복구 사지·이동/점프 배율·소지 한도는 APPLIED_CONFIGURATION. 물리 수행 실측과 구분.
- 대포 ContactDetected로 원인을 먼저 잡고 실제 절단/생존 상태와 결합. HitConfirmed도 공개. 네 번째 절단에서 정산이 StateChanged보다 먼저 실행돼도 사망 1회 보존.
- PlayerBalance.Fell은 네트워크 래그돌도 전달하므로 대포 사망을 OUT_OF_BALANCE로 덮지 않음.
- 서버는 측정된 선택 duration만 평균, null은 제외·0은 유지·같은 eventId 중복 제외. 카드와 기존 목록의 상세 문구에 표시.

## 검증
- Node 37/37, 로그 EditMode 31/31, 로그 PlayMode 최종 4/4. 증거 telemetry-detail-editmode-20261021.json / telemetry-detail-playmode-20261021.json.
- PlayMode: 기존 LAN/외줄, 대기실 구매→실제 타이머 만료→의료 입장/복귀→준비 즉시 전환→첫 퇴장/경보/집결, 대포 활성 톱 지속 접촉 중복 없음·재접촉·4번째 절단의 사망 원인/장애물 ID.
- 테스트 위치 이동·자금/정산은 합성 API 입력. 실제 다중 플레이·물리 입력을 검증했다고 보고하지 않음.
- 최초 대포 검사는 밀도 설정에서 비활성인 톱을 골라 접촉이 없었고 실제 활성 톱 조회로 수정. 치명상 검사에서 원인 덮어쓰기를 재현해 위 관측 버그를 수정.
- Compile 직후 run_tests HTTP400 두 번: ready/no_tests 상태 확인 후 별도 요청으로 성공. 실패를 PASS로 간주하지 않음.
- 기본 check-work는 integration 기준으로 dev 수신 71건을 구역 위반 처리, 단계 생략. origin/dev 구역 검사·문서·Unity 전용 검사는 별도 확인.
- 기존 전체 EditMode의 목마 점프 기대값 0.9/실제 1.35 실패는 미수정.

## Google
- 기존 본인 전용 동일 URL의 v8 배포(23:48 KST), 설치 소스 백업 같은 ID·폴더 유지. Code/HTML 복사 readback은 로컬과 줄바꿈 정규화 후 일치.
- 수신 v3·나만 권한·OAuth·INGEST_TOKEN·30분 트리거 변경 없음.
- 실제 TEST의 첫 도착/준비/집결 카드 확인. 일부 초기 실패 검사에는 퇴장 기점이 없어 null이며 그대로 보존.
- Start 재전송 ACK pending 43→24→10→0·오류 null. 이후 최종 회귀/종료 새 배치는 다음 실행에 전송된다.
- Google 증거 telemetry-detail-google-v8.png. 로컬 예시 데이터는 Google에 미업로드.

## 자동 변경
- 이전 승인된 GripYellow/pole-gate-black/section-sign 소수점 1줄 각각·runInBackground 원래 false 복원.
- 폰트 숫자 3 글리프/atlas 자동 추가는 사용자 별도 승인 후 해당 asset만 복원. 복구용 사본 .harness/local/telemetry-font-generated-20261021.asset.
- 씬·프리팹·게임 판정/가격/밸런스 수정 없음. 남은 공개 원천과 사람 검증은 coverage.md.
