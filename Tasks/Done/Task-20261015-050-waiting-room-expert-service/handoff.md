# Handoff

## 지금 상태
- 단계: PR #154 제출(→ integration/waiting-room-base) — PM 검토·4인 공동 테스트 대기.
- 동작하는 것: 씬 구성 메뉴 `Tools/CurtainCall/대기실/5단계 전문가 서비스 테스트 씬 구성`(4단계 씬에서 맵·상점·밤 정비 가져옴), ③ 수화기 옆 전문가 서비스 칸, 의료(1·3일차): 잃은 팔다리 모두 복구(개수 × 30)·구매자 도착 시 ⑧ 붉은 커튼 좌우 열림·황금빛·구매자만 F 입장·쇠붙이 소리·재등장, 시설(2일차): 장애물 강화/약화 구매(다음 날 적용값 `ExpertService.ObstacleFor`), 없는 담당은 "담당자 부재중", 테스트 패널·마우스 시점·창 열림 시 커서 풀기, 4인 AI 점검 봇(MPPM 가상 플레이어 전용).
- 막힌 것: 없음.
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/154
## 다음 할 일
1. PM: PR #154 검토·Squash 병합. 테스트 실패 2개(공용 SpaceLinkPlayTests)는 무관한 기존 실패 — 처리 결정(테스트 안에서 waitingRoomScene 덮어쓰기 제안).
2. 4인 공동 테스트(plan.md 3항목) — 결과를 아래 '공동 테스트 결과'에 기록. MPPM으로 할 때는 AI 봇이 자동으로 움직이니 사람 테스트 전 확인.
## PM이 확인할 것
- 확인 요청: 테스트 실패 2개 처리, meta의 Open(가격·복구 시간 임시값), 통합 병합 순서.
- 보고: 공용 카메라에서 UI 창이 열려도 커서가 안 풀림(`CameraViewDirector.SetInputBlocked` 호출처 없음) — 5단계 테스트 씬에서만 보완.
- 요청: 시설 강화/약화의 공연장 실제 반영·수치(기획서 5.1 #2) 별도 Task.
- 공용 파일 요청: 없음.
## Verification
- 구역 검사: PASS (10-17)
- 1 문서/규칙: PASS (10-17)
- 2 컴파일: PASS (10-17)
- 3 테스트: FAIL — 테스트 오류 (10-17, 재검사 동일). PlayMode 46개 중 44 통과·2 실패 = 공용 `SpaceLinkPlayTests`(Single_FollowsPhasesAndKeepsRunState, Additive_KeepsHubAndSwapsSpaceScenes). 원인: 1단계 #123(00045af)이 BaseGameSettings.waitingRoomScene을 DummyWaitingRoom→WaitingRoomBase로 바꿨고 테스트(SpaceLinkPlayTests.cs:138·198)는 DummyWaitingRoom을 기대. 이 값은 integration/waiting-room-base 계열에만 있고 dev·integration/waiting-room은 DummyWaitingRoom(통합 시 dev에서도 실패 예상). CI(verify-fast)는 Unity 테스트를 돌리지 않아 #123·#132·#151·#152 검사에 안 잡힘. 3·4단계(048·049)도 같은 2개 실패를 '무관한 기존 실패'로 기록하고 제출·병합됨. 이 Task는 해당 파일 미변경, 5단계 EditMode 19/19 PASS, PlayMode 테스트 없음(NO_PROJECT_TESTS). 2026-10-17 담당자 결정: 3·4단계와 같이 PR 본문에 무관한 기존 실패로 적고 제출, PM이 리뷰에서 확인.
- 4 실행 로그: 1인 Play(호스트) 확인 2026-10-17 — 1일차 쉬는 시간 구매 거절("밤 정비 때만"), 1일차 밤 구매(자금 100→70)·문 열림·입구 F(Interact 직접 호출, 시뮬레이션 키는 백그라운드라 입력 미전달)·방 안 이동·복구(LeftLeg→None)·문 앞 재등장·문 닫힘, 2일차 밤 "담당자 부재중" 거절(자금 유지)·수화기 칸 비활성 표시. 콘솔 오류·경고 없음.
- 4-2 기획 대조 Play 2026-10-17: 3곳 결손 → 역순 복구(왼팔 먼저)·모델 팔다리 복원 일치, 걸어서 통과 불가(막), 이용 중 문 닫힘·쇠붙이 소리, 온전한 몸·자금 부족·사망 거절, 밤 종료 시 예약 정리, 3일차 의료 재개·시설 부재중. 미확인: 4인 실제 접속 동기화, 실제 F 키 입력, 황금빛은 2026-10-17 추가(문이 열린 만큼 방 안 조명이 밝아짐, Play 확인).
- 4-3 기획서 원문 대조 뒤 수정 Play 2026-10-17: 구매 전 커튼 앞 F 안내, 2곳 결손 한 번에 복구(비용 60)·구매자 도착 시 커튼 좌우 열림·황금빛·입장·재등장·커튼 닫힘, 2일차 시설 강화 구매(3일차 적용값)·재구매 거절, 마우스 시점 켜짐. 주의: Play 중 스크립트 저장 시 재컴파일로 ScheduleBoard.Update NRE 반복(코드 결함 아님) — Play를 멈춘 뒤 수정.
- 4-4 4인 AI 사전 점검 2026-10-17: MPPM 가상 플레이어 3개 실행·파일 동기화 확인, 자동 참가자(ExpertServiceBot, 가상 플레이어 창 전용) 추가. 가상 플레이어 창이 응답하지 않아(Play 미진입·로그 정지) 접속·점검 미실행 — 창 상태 확인 필요.
- 4-5 커서 수정 2026-10-17: 마우스 시점에서 수화기 창을 열어도 커서가 잠겨 클릭 불가(공용 CameraViewDirector.SetInputBlocked를 아무도 부르지 않음 — PM 보고 대상) → 테스트 씬에서 InputGate 막힘 동안 커서 풀기, ESC 뒤 패널 클릭 시 재잠금 안 되게 영역 지정. Play 확인: 수화기 연 뒤 lock=None·visible.
- 5 공동 테스트: 미요청
## 공동 테스트 결과
- 일시: 미실행
- 참여자: 미실행
- 재현: 미실행
- 예상: plan 참조
- 실제: 미실행
