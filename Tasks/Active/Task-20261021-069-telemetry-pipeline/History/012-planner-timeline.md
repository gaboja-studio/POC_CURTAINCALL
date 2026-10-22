# 기획 분석 타임라인·호스트 전송

- 같은 UGS 로그인 ID가 MPPM 두 플레이어의 동선을 합쳤다. run+slot, 접속 ID, 과거 ID 순으로 화면/집계 식별. 협동 participants는 방+접속 ID로 유일하게 기록해 같은 로그인 두 명의 중복 IDs 계약 거부도 수정.
- 키·일반 버튼·마이크·SCENE_EXITED·TELEMETRY_GAP·주기 진행·OBSERVED는 분석 타임라인에서 제외. 원본 보존. 플레이어별 사망/최대구간/절단 요약, 사건 순서, 50~800% 줌·휠·가로 스크롤, 기술 ID/원본 JSON 기본 상세 제외.
- 세션 검색 입력과 native select 분리. 날짜/최대 도달 일차/짧은 ID로 선택. 검색 중 선택 보존, 조건 변경 시 검색 초기화. 자금 평균 옵션 유지.
- 대기실 지표의 분모·표본·확정 시점·진단 관점을 화면에 안내. 추정 정상/이상 기준은 만들지 않는다.
- 호스트만 Google 전송, 참가자는 immutable 원본을 호스트로 NGO 전달. HOST 권위 위조 배치 거부, 원문/ID/시계 보존, 디스크 성공 후 해시 ACK. ACK 유실 재전달은 동일 파일, 이미 Google에 갔으면 기존 eventId로 중복 방지. 참가자 종료 직전 미전달은 다음 방 접속까지 자기 큐 보존.
- 빌드 전 설정 검증 및 Library 임시 파일→StreamingAssets 포함. 사용자 cmd 불필요, 기존 빌드는 재빌드 필요. 수신 토큰은 클라이언트에서 추출 가능한 입력 제한값이며 계정 인증 비밀로 취급하지 않는다.

## 검사
- Node 63/63. Unity Compile PASS, TransportPolicyTests EditMode 7/7(실제 네트워크 플레이 아님).
- Chrome 예시 실제 화면: TEST 선택·세션 native 선택·플레이어 1/2·150% 확대·플레이어2 사망 클릭 상세 확인. .harness/local/telemetry-planner-preview.png.
- 실제 PC 빌드·다인 네트워크·두 PC·강제 종료 검사는 요청대로 SKIPPED. 기존 미커밋 작업 보존, 커밋/push 없음.
- 에디터 검사 중 폰트 atlas 및 머티리얼 3개 색상 정밀도 변경 관측. 복원 승인 없이 보존(현재 로그 코드와 무관).

- Google 본인 전용 동일 URL v17(03:43 KST) 배포. 편집기 Code/HTML readback은 개행 정규화 후 로컬과 일치. Drive 설치 소스 같은 ID 2개 갱신. 실제 수신 v13·권한·트리거 불변.

- 실제 486건 원문 재집계에서 두 플레이어 분리 확인(외줄 표시면 9사건). 종료 LOCAL 번호 공백은 같은 app/room/run 내 단 하나의 LOCAL slot이 있을 때만 표시 사본에 보강하고 원문 불변. 대기실 관측 이용자 2명, 대포 거리 표본 2명. Node 추가 회귀 통과. Google 렌더 프레임 blank는 이번 연결에서도 재현해 최종 실화면 완료로 보고하지 않는다.

## 이전 인계 보관
- 같은 앱의 저장 완료 사건을 연결해 구매/배송/수령/보관·스케줄/준비·의료 단계별 시간 추가. 누락/다른 방·런/시각 역전은 null, 최근 2,048개 기준 유지.
- 시설 성공 예약에 구매자·조정·적용 일차·지불액 기록. 원천 구독자 예외 격리, 의료 흐름과 분리.
- 기반 로컬 커밋 5da374f 완료. 후속 MPPM 조사/큐 분리·새 기록 우선/상태 표시 수정은 미커밋. 58묶음·485사건 보존 원문 Drive ACK 완료(History/010). 기존 대기 원본 삭제 없음.

- 최종 본인 전용 v18(03:49 KST), scheduledRefresh 03:50 완료. 실제 시트 CANNON 거리 표본2 및 평균41.8061, WAITINGROOM 이용자2 readback 확인. 기본 check-work는 기존 dev 유입77건 구역 오탐 FAIL, 별도 origin/dev Scope/Fast PASS. 씬 Start dirty0.
