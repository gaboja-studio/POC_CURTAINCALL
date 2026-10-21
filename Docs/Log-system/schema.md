# 전체 게임 로그 계약 v1

## 저장 구조
- Unity → 디스크 outbox → Apps Script 수신 → Drive 원본 → Sheets Events → 웹 대시보드.
- token과 batchJson을 POST. batchJson은 UTF-8 원문 그대로 SHA-256 검사·보관한다.
- ACK는 Drive 저장 완료를 뜻한다. Sheets 반영은 수동 또는 30분 importer 이후다.
- batchId/eventId는 재시도해도 바꾸지 않는다. 같은 ID에 다른 내용이면 충돌로 보존·보고한다.
- UTC는 사건의 시각, monotonicMs는 앱 실행 후 경과 시간, sequence는 해당 앱의 발생 순서다.
- 클라이언트 간 엄밀한 전체 순서는 보장하지 않는다. 타임라인 UTC 정렬은 근사다.

## 공통 요소
| 이름 | 형식·의미 |
|---|---|
| schemaVersion | 1, 배치 계약 버전 |
| batchId | UUID D 형식, immutable 전송 묶음 |
| senderId | 디스크에 유지하는 설치별 UUID |
| appSessionId | 실행마다 새 UUID |
| flushReason | CHECKPOINT/SCENE_CHANGED/RUN_ENDED/PLAYER_EXITED/GAME_EXITED/RECOVERY |
| eventId | 이벤트별 UUID D 형식 |
| sequence | 앱 실행 내 증가 정수, 배치 안에서 엄격히 증가 |
| occurredAt | ISO 8601 UTC, 끝에 Z |
| monotonicMs | 앱 시작 후 경과 ms, 0 이상 |
| environment | LIVE/TEST. Editor·Development Build는 TEST |
| gameVersion | 발생 시 Application.version(현재 0.1.0). 재전송·복구 시 원래 값 유지, 과거 누락은 버전 미기록 |
| playerId | opaque 문자열 1~128. 아직 로그인 ID 미연결이면 senderId를 명시적 대체값으로 사용 |
| playerNumber | 0~3 슬롯, 미배정이면 null. NGO clientId와 별개 |
| networkClientId | ulong의 10진 문자열. JSON 정밀도 손실 방지 |
| roomId | 소문자 16진수 32자리, 원천 미연결이면 null |
| runId | UUID D, 공유 보상 세션 ID + 세대의 결정적 파생값 |
| performanceId | D1P1처럼 표시하는 공연 순번. index+1, 단독 고유키 아님 |
| attemptId | 공유 UUID D, 재시도마다 새 값. 미연결이면 null |
| breakId | D1T1 형식, 실제 쉬는 시간 구분 매핑 전에는 null |
| sceneType | TITLE/LOBBY/WAITINGROOM/MAINSTAGE/BACKSTAGE/EXITROUTE/RESULTS/UNKNOWN |
| eventType | event-catalog.md의 등록 이름 |
| authority | LOCAL/HOST/OBSERVED. 진위 보증이 아닌 발신자의 관측 구분 |
| payload | 사건별 JSON 객체, 최대 8192문자 |

gameVersion은 schemaVersion과 별개다. 숫자 3개와 선택적 -/+ 접미사, 최대 64자. Events V열에 추가하고 기존 A~U열·원문·ID는 보존한다. 과거 누락을 0.1.0으로 소급하지 않는다. 요약 시트도 마지막 gameVersion 열로 구분한다.

요약 v15는 기존 gameVersion 뒤 environment를 추가하여 TEST/LIVE를 함께 게시한다. 자금 일계는 뒤에 balance/samples/carried/mode를 추가한다. mode=RUN의 runId별 잔액과 AVERAGE의 일차 평균은 dashboardView의 같은 funds projection을 사용하며 합산 대상이 아니다. Events 원본과 과거 버전 값은 변경하지 않는다.

CANNON_HAZARD_CONTACT는 일반 함정의 새 접촉마다 발행한다. cutApplied/part/lostCount와 CUT·NO_CUT·FRAME_LIMIT를 기록하며 기존 절단·사망 건수를 대체하거나 늘리지 않는다. BREAK_ENDED는 이전 breakId 문맥으로 발행하고 HOST는 presentNetworkClientIds를 보존한다. 종료가 없는 과거/오프라인 공백을 미수령·미이용으로 추정하지 않는다.

## 사용자 초안과의 대응
- ‘유저 코드’/‘플레이어 ID’는 playerId. 인증 ID는 24자리로 잘라내거나 변환하지 않는다.
- ‘클라이언트 ID(플레이어 넘버)’는 playerNumber로 명명. 실제 NGO ID는 별도 보존한다.
- ‘게임 ID/게임 세션 ID’는 runId. 앱 실행 ID와 방 ID를 겸하지 않는다.
- ‘공연 ID D0P0’은 performanceId, ‘시도 ID’는 attemptId, 시도 횟수는 retryIndex.
- 잃은 팔다리 수는 lostCount 0~4. 남은 개수는 4-lostCount로 계산할 수 있다.
- 기존 GameplayRecord.lostParts는 bitmask이므로 그대로 count 열에 넣지 않는다.
- 좌표는 {x,y,z} 객체. 위치 계측 어댑터에서 소수점 1자리로 반올림한다.
- 공연 사건은 기존 position이 없을 때 발행 시점 관측 위치를 추가하며 positionSource=EMIT_OBSERVATION으로 구분한다. 사망/절단 원천 스냅샷 및 명시적 null은 우선 보존한다. 기존 v1 배치와 호환한다.
- 지속 시간은 숫자 ms. 00m00s는 화면 표현이며 원시 데이터 형식으로 쓰지 않는다.
- 사망 원인 초안 오탈자(OUT-OF-BALLANCE, HURTED)는 원천별 매핑을 합의한 뒤 통일한다.
- OB_/IN_ 뒤 12자리 해시를 사용한다. 씬 경로·계층·sibling 순서 또는 원천 종류/출현 번호에 의존하므로 빌드 간 영속 GUID는 아니다.
- 마이크는 타임스탬프만, 음성 원본을 저장하지 않는다.
- v3 배포: 대포 구간·일부 사망 구간·목표 도달/손잡기 상태를 확인할 수 없으면 명시적 null. 0·false로 대체하지 않는다. 기존 유효 배치는 그대로 호환된다.
- 입력은 실제 키 코드·버튼 ID만 수집한다. 음성은 Vivox 본인 SpeechDetected 구간이며 녹음 길이와 다를 수 있다.
- 방 ID는 로그 어댑터의 호스트 생성 UUID N. 게임 접속 코드/IP 및 외줄 Results.RoomId와는 별개다.
- 서비스 로그인 ID는 방 연결 시 사용 가능한 경우 적용하고, 방 안에서 지연 로그인으로 바꾸지 않는다. 설치 ID 대체 여부는 ROOM_ENTERED.identityScope에 남긴다.

## 집계 기준
- 승률: HOST의 서로 다른 attemptId 중 확정 성공/(확정 성공+실패). 미완료는 분모 제외.
- 거리: LOCAL의 플레이어·시도별 최대 거리의 평균. 이동 궤적 길이와 다르다.
- 사망·협동: HOST만 집계. 플레이어별 관측 복제본은 제외.
- 이용률: 대기실에서 관측된 런·플레이어 조합 중 해당 기물/서비스 이용자.
- 자금: HOST 장부 transactionId별 1회. EXPENSE는 상세 사용처가 아직 미분류된 실제 지출.
- 누락 입력은 미수집/null. 데이터 없는 승률이나 거리 평균을 0으로 대체하지 않는다.

## 대기실 상세 시간
- 대기실 선택 payload: ZONE_ENTERED.spawnToZoneMs, STAGE_ENTRANCE_REACHED.readyToEntranceMs, ALL_PLAYERS_ASSEMBLED.firstLeftToAssemblyMs/alarmToAssemblyMs, MEDICAL_RETURNED.enteredToReturnMs. 동일 화면 단조 시간의 ms이며 미관측은 null. 준비 취소·재방문·새 쉬는 시간은 이전 기준을 재사용하지 않는다.
- RECALL_EXPIRED는 실제 HOST_TICK_EXPIRED만 발행하며 이전 쉬는 시간 breakId를 보존한다. MEDICAL_ENTERED/RETURNED의 LOCAL_POSE_APPLIED는 실제 위치 이동 완료다. RESTORATION_VERIFIED의 APPLIED_CONFIGURATION은 적용 배율·허용 상태·소지 한도이며 물리 수행 실측과 구분한다.

## 거래·서비스 단계별 시간 (선택 payload)

2026-10-22 추가. 같은 TelemetryRuntime의 저장 완료된 시작 사건과 현재 사건의 monotonicMs 차이다. timingSource=OBSERVED_CLIENT_MONOTONIC. 양 끝 미관측·시각 역전·다른 방/런은 null, 실제 0ms는 보존한다.

| 사건 | 필드 |
|---|---|
| PURCHASE_COMPLETED | screenToPurchaseMs |
| DELIVERY_DISPATCHED | purchaseToDispatchMs |
| DELIVERY_AVAILABLE | dispatchToAvailableMs, purchaseToAvailableMs |
| ITEM_COLLECTED | availableToCollectionMs |
| ITEM_STORED | storageArrivalToStoreMs |
| SCHEDULE_CONFIRMED | screenToConfirmMs |
| READY_CHANGED(ready=true) | screenToReadyMs, confirmationToReadyMs |
| SERVICE_RESERVED | screenToReservationMs |
| MEDICAL_ROOM_REACHED | reservationToRoomMs |
| MEDICAL_ENTERED | roomToEntryMs, reservationToEntryMs |
| RESTORATION_COMPLETED | enteredToRestoreMs |
| MEDICAL_RETURNED | restorationToReturnMs (기존 실제 이동의 enteredToReturnMs는 보존) |

화면 시간은 본인의 screenFlowId, 의료/보관은 flowId+playerId, 배송은 주문 flowId로 연결한다. 준비 시간은 같은 breakId로 제한한다. 다른 PC에서 관측한 시작 사건을 가져와 시계를 빼지 않는다. 최근 2,048개 시작 표본만 유지하며 오래된 표본이 없으면 미수집이다. 내려놓기/재줍기는 최초 소품실 도착을 유지하며 인계/보관은 해당 소지자의 기준을 닫는다.

시설 서비스는 HOST의 SERVICE_RESERVED(serviceType=FACILITY)에 adjustment·appliesOnDay·amount를 기록한다. 신체 대상이 없어 lostCount/targetCount=0이며 의료 흐름을 시작하지 않는다. 이용 집계는 같은 런·예약 flowId를 1회만 센다. 의료/기물은 기존 LOCAL 집계를 유지한다.

다른 플레이어의 수령은 호스트가 ITEM_COLLECTED(authority=HOST, source=HOST_RECEIPT_SNAPSHOT)를 수령자 대상으로 기록한다. 수령자는 LOCAL/LOCAL_RECEIPT_SNAPSHOT을 기록하며, 호스트 본인 수령은 LOCAL 1건만 기록한다. 동일 주문 flowId를 사용하므로 호스트의 DELIVERY_AVAILABLE→수령 시간만 산출하고 다른 PC의 단조 시각은 사용하지 않는다. 기물 이용 집계는 LOCAL만 사용해 복제본을 중복으로 세지 않는다. 초기 수령 목록은 과거 사건으로 다시 발행하지 않는다.

대시보드 stageTimings는 단계별 avgSeconds/minSeconds/maxSeconds/samples를 반환한다. 누락·null·음수·유한하지 않은 값·문자열은 제외하고 실제 0ms는 포함한다. 중복 eventId는 같은 앱에서 1회만 세며, 수령 시간만 roomId+runId+flowId로 합쳐 유효 HOST 표본을 우선하고 LOCAL을 대안으로 사용한다. 준비 취소는 준비 완료 표본이 아니다. 의료/시설 예약은 serviceType·권위를 분리한다. LIVE/TEST 및 요청한 runId 필터를 적용하고 과거 타임스탬프로 빠진 소요 시간을 복원하지 않는다.

## 한도·실패 처리
- 배치 250건 이하, 로컬 배치 약 100KiB, HTTP body 256KiB 이하.
- outbox 64MiB 초과 시 수집 실패 표시, 미전송 원본을 자동 삭제하지 않는다.
- 수신 오류/ACK 불일치/오프라인은 원본 유지·지수 재시도(최대 약 5분).
- 종료 시 네트워크 완료를 기다리며 게임 UX를 변경하지 않는다. 미전송은 다음 실행에서 복구한다.
- Sheets 50,000 이벤트 초과 시 명시적 CAPACITY_LIMIT. 조용히 앞부분만 집계하지 않는다.
- 무료 Google 할당량 초과 시 오류·재시도. 유료 리소스 자동 생성·요금제 전환 없음.
- 공개 수신 배포는 token 없는 POST 저장 거부·GET 차단. 별도 대시보드는 2026-10-22 사용자 승인으로 입구 공개·등록 이메일 인증 후 조회, 관리 RPC는 소유자만 허용.
