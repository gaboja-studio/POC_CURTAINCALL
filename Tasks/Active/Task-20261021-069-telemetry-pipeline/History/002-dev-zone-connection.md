# dev 수신·대기실 구역 연결 (2026-10-21)

## 수신
- 사용자 요청으로 origin/dev fetch, bc0afaf → e6da182 fast-forward. 충돌·수신 merge commit 없음.
- 기존 작업 74개 파일 SHA256 비교로 보존 확인. dev 수신 84개 경로와 로컬 변경 교집합 0. 서드파티 gitlink 동일·LFS 준비 후 수신.
- 현재 feat/telemetry-pipeline 유지, 통합 대상 integration/log-system. 구현 커밋·push·PR 없음.
- doctor fail 0/warn 2(CLI Facts 버전 불일치·skill-links 없음). CLI beta.13 최신 확인. 연결 프로젝트 D:/github/POC_CURTAINCALL, 6000.3.25f1, Start clean/stopped.

## 연결
- WaitingRoomZoneMap: 기존 CameraZones/Zone_Dormitory, Restroom, PropRoom, JunkStorage, Corridor, ExpertRoom의 CameraViewZone+BoxCollider를 읽기만 한다.
- 침실·화장실·소품실·잡동사니 보관소·복도·전문가 대기 구역으로 매핑. 회전/스케일/비활성 반영. 겹치는 volume은 임의 선택 없이 WAITINGROOM/AMBIGUOUS_CAMERA_VOLUMES.
- 기존 StageEntranceGather가 무대 입구 우선. camera exitPadding은 공간 경계에 적용하지 않는다. 위치·boundarySource·zoneId 포함.
- StorageArrivalTracker: 본인 수령/줍기/인계 획득을 관측한 UID만 소품실 도착 기록. 같은 주문 flowId, acquisitionSource, durationMs. 초기 스냅샷·재방문으로 도착을 만들지 않는다.
- 내려놓기/재줍기는 시작 시각 유지, 인계·보관·씬 종료 시 해당 관측 정리. 구역 관측 사이 즉시 보관도 이전 소지 상태로 도착을 확인한다.
- 씬·프리팹·기존 원천 7개 파일에는 이번 후속 연결의 추가 수정 없음.

## 검증
- check-work 최초 실행은 origin/integration/log-system보다 앞선 dev 수신 파일을 구역 위반(72건)으로 계산해 중단. 소속 통합을 강제 이동하거나 소유권 예외를 추가하지 않음.
- verify-scope -Base origin/dev PASS(78파일). verify-fast PASS(기존 길이 경고만). Unity 컴파일 PASS.
- 로그 전용 EditMode 25/25(신규 구역/운반 4건 포함), Node 28/28.
- 실제 WaitingRoomBase 씬의 전용 PlayMode 1/1: LAN 호스트·구매/배송/수령 후 침실→소품실→침실→소품실 합성 이동, 체류 양수·재방문 2회·도착 1회·flowId 일치·보관 확인. 실제 물리 입력 검증과 구분.
- 전체 EditMode 964/963/실패1, 한 번 재현해 동일. TightropeMovementFeedbackTests.PiggybackHopUsesSameHeightAndGravityAsRopeJump: 기대 0.9, 실제 1.35 (145행). dev의 목마 JumpHeightMultiplier=1.5가 테스트 기대에 미반영된 상태와 일치. 이번 로그 변경은 해당 계산/테스트를 수정하지 않음. 다른 소유 구역이라 원천 수정 보류.
- 전체 결과는 도메인 리로드 뒤 일부 leaf만 응답되어, Test Runner 창의 저장 결과로 실패 이름·assertion 확인. 전체 PASS로 바꾸지 않는다.
- 증거: .harness/local/telemetry-dev-full-editmode-20261021.json, telemetry-dev-reproduced-editmode-20261021.json, telemetry-zone-editmode-20261021.json, telemetry-zone-playmode-20261021.json(로컬 전용).

## Google
- Google Drive의 검증된 기존 dashboard.html.txt(1w9JDNvvEH3cSA9s5x_CmWdu_zoUQRe73, parents=1SR2_9ajlqunHN39kr2PRKUoy8YYvvbf2) 원본 갱신 성공.
- Apps Script dashboard.html의 한글 구역 사전만 변경. 기존 본인 전용 URL v5(20:57 KST) 배포 완료. 접근 범위 나만·실행 사용자·수신 v3·OAuth·token 변경 없음.
- Start 실제 프레임 1,851→22,368 증가·Google ACK 후 pending 0·LastError null. 이후 Stop으로 Start clean/stopped·runInBackground false 확인. 종료 직전 이벤트는 다음 Play 재전송 대상일 수 있다.
- Google TEST 240건(21:05 KST) 갱신 완료. 침실/소품실 각 2회 진입·이용자 1명·평균 체류 0.2/0.6초 표시. 합성 이동 테스트 데이터를 TEST로 누적했으며 LIVE 통계로 섞지 않는다. 증거 .harness/local/telemetry-zone-dashboard-20261021.png.

## 다음
1. 스폰 기준 이동 시간, 집결 타이머 확정 만료/위치·통로 막힘/우회의 게임 원천 계약 확인.
2. 의료실 실제 복귀 경계·치료 후 이동/점프/소지 능력 실측.
3. 공연별 추가 장애물 충돌·외줄 발판/손잡기 지속 상태.
4. CODE 다중 클라이언트·재접속·오프라인 재전송·실제 조작 공동 검증.
