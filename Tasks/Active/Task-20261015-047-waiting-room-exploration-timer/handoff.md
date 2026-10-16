# Handoff

## 지금 상태
- 단계: working. 1차(첫 퇴장 300초·우측 하단 표시·60초 소집 경보·30/10초 방송) 구현, 호스트 1인 확인. LAN 2인·4인 공동 테스트 전.
- 동작하는 것:
  - `WaitingRoomExitWatch`(씬 `ExplorationTimer/WaitingRoomArea`, 영역 Area_Main ①~⑤·Area_Expert ⑧): 판정 화면에서 대기실 안에서 보였던 살아 있는 인원이 밖으로 나가면 쉬는 시간마다 한 번 `CampaignRun.NotifyLeftWaitingRoom()`. 나간 인원 목록 `HasLeft`/`PlayerLeft`(재진입 불가용).
  - `ExplorationTimerAnnouncer`: 공용 `RecallAlarm` → 소집 경보, `ExplorationTimerCues`(Logic asmdef) → 30·10초 방송. 이벤트 `Announced`.
  - `ExplorationTimerView`: 우측 하단 원형 시계+mm:ss(멈춤 회색·평소 크림·경보 뒤 빨강), 시계 옆 문구 4초, 소집 경보 때 코드 생성 임시 '딩동'.
  - 테스트 씬: 1단계 메뉴로 그레이박스 재생성, PlaySetup 끔, CinemachineBrain·PlayerFollowCamera, `TestUI`(접속·런 진행 IMGUI).
- 막힌 것: 없음.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/132
## 다음 할 일
1. LAN 2인 확인(시간·경보·방송 화면 일치) → 다음 항목(재진입 불가·전원 ⑥ 조기 시작).
2. 0초 추격 항목 전 공용 담당(@MoHoDu)에게 "0초 뒤 정리 대기" 신호 요청(plan Risks).
## PM이 확인할 것
- 확인 요청: 30·10초 문턱을 공용 세팅(`BaseGameSettings.Run`)으로 옮길지 — 지금은 `ExplorationTimerAnnouncer` 값.
- 대기 중인 결정: meta Open 참조. 공용 파일 요청: 없음(1차).
## Verification
- 구역 검사: PASS (10-16)
- 1 문서/규칙: PASS (10-16)
- 2 컴파일: PASS (10-16)
- 3 테스트: PASS (EditMode, 64/64, 10-16)
- 4 실행 로그: LAN 호스트 1인(개발자 모드 임시 켬 → 끝나고 끔). 가이드 쉬는 시간 대기 5:00 회색 → ⑥ 쪽으로 이동 → "첫 퇴장 → 시작 300초", 남은 시간 62초로 → 60초 소집 경보 로그·빨강, 31초로 → 30초 방송, 20초 뒤 10초 방송, 0초 → 공연 2 전환·표시 숨김. 새 오류 없음(콘솔 오류 3건은 이전 _ThirdParty 로드 오류). Game 뷰가 좁으면(433×565) 문구가 테스트 IMGUI 패널에 가린다. 공용 Dynamic 폰트(Pretendard, Resources)에 Play 중 글자가 추가돼 에셋이 바뀌던 문제 → 되돌림(사용자 승인). 통합 #122가 이 폰트를 Static으로 구워 해결(함정 tmp-dynamic-font-churn), 병합 뒤 타이머 문구 글자 누락 0자 확인.
- 5 공동 테스트: 미요청
## 공동 테스트 결과
- 일시: 미실행
- 참여자: 미실행
- 재현: 미실행
- 예상: plan 참조
- 실제: 미실행
