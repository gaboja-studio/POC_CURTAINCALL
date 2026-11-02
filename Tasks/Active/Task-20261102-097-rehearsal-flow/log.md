# Log

- 2026-11-02 Task 생성. 근거 Confluence 32702474·Jira CC-199. 브랜치 feat/rehearsal-flow를 feat/waiting-room/tutorial(origin/dev b0669a96 병합 후)에서 분기. 병합 때 Unity 에디터가 켜져 있었음(에디터에서 씬 저장 주의).
- 조사 결과: 연습실(S09)은 아트 전용(런타임 스크립트 없음). 철창(NightDoorLock)은 시간대로만 여닫음. 외줄 그네·트리거 그네는 코스 생성 모드에서만 자동 부착(`TightropeSwingPole`·`TightropeRelayGate`), 씬 지오메트리 모드에서는 안 붙음 → 리허설 코스는 생성 모드로 만든다. 투표·바닥 길안내 기존 코드 없음, NavMesh 미굽기(테스트가 콜라이더로 런타임 굽는 방식 사용).
- 2026-11-02 구현(1차): `CampaignPhase.Rehearsal`·`RunState.EndRehearsal`/`EnterRehearsal`(StartSession(withRehearsal)로만 진입, FailRun 세대 교체는 바로 공연), 가이드 쉬는 시간 `WaitingForExit` 대기 + `SetTutorialDone/RefreshGuideRest`(CampaignRun TutorialDone 메시지), SpaceRoute Rehearsal→대기실, Dev 시작 지점 "리허설", NightDoorLock 리허설 잠금(PracticeGate 제외), StageEntranceGather 리허설 완료 뒤 집결→첫 공연, WaitingRoomTutorial 연계 G01·G11 완료(또는 끔/120초) → NotifyTutorialDone, 리허설 중 Revived 무시, TightropeRun 연습 모드(PracticeMode: 도착·성공·실패 판정 없음).
  리허설: `WaitingRoom/Rehearsal/` RehearsalStage(5 소구간 코스 JSON)·WaitingRoomRehearsal(코스 생성·호스트 완료/추락 복구/포기 투표/재진입·메시지 공유)·.Local(출발대 배치·퀘스트·설명·길안내·포기 입력)·RehearsalPathGuide(런타임 NavMesh 점선). 문구 30키 WaitingRoomTexts.txt 추가.
  검사: 컴파일 PASS, EditMode RestTimeRulesTests 2/2(기대값 갱신)·DevStartPointTests 20/20·WaitingRoomTextTests 5/5.
  PlayMode W6_Rehearsal_BuildsCourseOnRingAndAdvancesWhenEveryoneArrives 1/1: 리허설 시작 지점 → 타이머 None, 연습실 링 위 1단계 코스(2줄)·연습 모드, PracticeGate만 열림, 내 캐릭터 출발대, 도착대로 옮기면 2-1(1줄·그네 봉)로 전환·새 출발대 배치.
  미확인(사람 플레이): 실제 줄 조작·그네·트리거 그네 소구간 통과, 추락 복구 체감, 포기 투표·재진입, 완료 뒤 점선 길안내와 집결→공연 전환, 공연 뒤 가이드 쉬는 시간 타이머가 tutorial 뒤 시작, MPPM 2인.
  발견·수정: RehearsalPathGuide가 NavMeshPath를 필드 초기화에서 만들어 예외(Unity 제한) → Awake로 이동. 재컴파일 PASS.
  재검사: PlayMode W6_Rehearsal 1/1 PASS(수정 후). 커밋·push는 미실행(요청 시).
- 2026-11-02 캡처(임시 PlayMode TempCapture_Rehearsal, 사용 후 삭제): .harness/local/rehearsal/01~06.png — 1단계 출발·옆줄 퀘스트·2-1 그네·2-3 트리거·3단계·완료 길안내. 발견·수정: WaitingRoomTutorial이 리허설 중 가이드 패널을 매 프레임 닫아 좌측 설명이 안 보임 → 리허설 시간대는 건너뜀. 길안내 선은 점선 무늬가 각도에 따라 흐릿함(후속 다듬기).

## 2026-11-02 구조도대로 재구성

- 계기: 구조도 이미지(Confluence 32702474) 대조 결과 2-2·2-3·3단계가 달랐다(코스 생성기의 끊김 1개·같은 위치·그네/해제봉 택일 제약). 사용자 결정: 2-2는 공중에서 그네2를 바로 잡기, 트리거 그네 시간 제한 없음, 연습실 옛 소품은 리허설 중 숨김.
- 구현:
  - `TightropeSwingPole.Spots.cs`(신규): 코스 값 대신 지정 자리(줄·구간)마다 그네(`UseSpots`). `ChainNext`이면 놓고 날아가는 중 다음 그네 매달림 위치 창(앞뒤 1.5m, 아래 1.2m~위 2.2m, 같은 줄)에 들어오면 공중에서 잡음(`AirGrab` 요청, 호스트 판정). 기존 공연 그네는 자리 미지정 시 동작 그대로(GapFrom/GapTo가 코스 값).
  - `TightropeControlScheme.ExternalInput`: 줄 위 외부 장치 입력 가로채기(리허설 트리거 그네 F).
  - 리허설: `RehearsalStage`에 그네 자리·끊긴 구간·트리거 그네·발판 데이터(구조도 줄 번호 = 위에서 0번). 끊김은 `BlockSegment`, 트리거 잡는 동안만 해당 구간 해제. 트리거 점유는 호스트가 정해 상태 메시지로 공유. `WaitingRoomRehearsal.Triggers.cs`(신규): 트리거 그네 모양·점선 끊김·중간 발판·입력·잡는 동안 균형 유지. `LegacyRing/PracticeLanes` 리허설 중 비활성, 끝나면 복구. 퀘스트 문구 trigger-* 6키로 교체(relay-* 삭제).
- 검사: 컴파일 PASS. PlayMode `W6_Rehearsal_CoursesFollowStageDiagrams` 1/1(소품 숨김·1단계 발판·2-2 반동→놓기→공중에서 그네2 잡기·2-3/3단계 트리거 짝 연결), `W6_Rehearsal_BuildsCourse...` 1/1, EditMode TightropeSwingGapTests 17/17. 캡처 `.harness/local/rehearsal/v2/`.
- 남은 것(사용자: 기능 먼저, 테스트·버그는 나중): 외줄 PlayMode `SwingPole*` 2건 FAIL — 원인은 이번 변경이 아니라 02611495의 게임 시작 흐름(도입→리허설) 때문에 공용 도우미 `BeginPerformance`가 공연 시작을 기다리다 시간 초과. 도우미에 리허설 건너뛰기를 넣었으나 재실행 안 함. 이 도우미를 쓰는 외줄 테스트 전체 재확인 필요.

## 2026-11-02 조작을 공연 외줄과 같게 (사용자 지적)

- 지적: 리허설은 외줄 규칙 연습이라 조작이 공연과 같아야 한다. 2-2 자동 공중 잡기와 자체 트리거 장치(줄 위에 선 채 유지)가 공연과 달랐다.
- 수정:
  - 그네 끊김을 공연 회전봉과 같은 8m로(2-1 8~16, 2-2 4~12+12~20, 3단계 3~11). 3단계 끊김·트리거 위치 재배치.
  - 2-2 공중 잡기: 날아가는 중 다음 그네 가까이에서 F를 눌러야 잡음(입력 0.3초 유예). 자동 잡기 제거.
  - 트리거 그네: 자체 장치를 지우고 공연 해제봉(`TightropeRelayGate`)에 지정 자리 모드(`TightropeRelayGate.Spots.cs`, `RelaySpot`) 추가. F로 잡으면 봉에 매달리고 F/Space/F 길게로 놓기, 봉 모양·연결 줄 표시 공연 그대로. 지정 자리 봉은 봉마다 혼자 잡아도 자기 짝 줄을 잇고, 시간 제한 없음(전광판 숨김, 사용자 결정 유지). 기존 공연 해제봉 4개 동작은 그대로.
  - `TightropeControlScheme.ExternalInput` 훅 되돌림(불필요).
- 확인: 컴파일 PASS. 임시 캡처 테스트(실제 F·W·Space 입력을 외줄 조작 규칙에 넣음, 사용 후 삭제): 2-1 착지 16.6m, 2-2 공중에서 F로 두 번째 그네 잡기 → 착지 20.6m, 3단계 착지 11.6m, 트리거 2-3·3단계 F로 매달리면 짝 줄 연결·Space로 놓으면 다시 끊김. 캡처 `.harness/local/rehearsal/controls/`.
- 테스트 정리는 기능 구현 뒤(사용자): `W6_Rehearsal_CoursesFollowStageDiagrams`를 해제봉·F 공중 잡기 기준으로 고쳤으나 재실행 안 함.

## 2026-11-02 첫 공연 전 동선 제한·단장 모니터 (사용자 순서)

- 사용자 순서: 서명 → 연습실에서 시작(입구 철창 닫힘) → 리허설 성공 → 연습실 철창 열림·무대 입구 길안내 → 무대 입구 통로·로비·공연 스테이지 입구 외 모두 철창, 상점·소품실·숙소 등 상호작용 잠금, 연습실은 나오면 다시 잠김 → 무대 입구 통로 접근 시 치직 소리와 함께 로비·통로 위 모니터에 가면 쓴 단장 얼굴 → 외줄 공연 → 대기실 튜토리얼 때 해제 → 2일차 → 밤 정비 튜토리얼.
- 실측(구조 A): 로비에서 철창 없이 열린 입구 3곳 — 북쪽 소품실·작업실 갈림길(약 4m), 북동쪽 상점·승강기·지하 홀(약 12m), 동쪽 숙소(약 4m). 기존 NightDoorLock 4곳(연습실·작업실·물류·지하)+무대 통로 셔터.
- 구현:
  - `RehearsalLockdownGate`(신규): 위 3곳에 런타임 철창(기존 철창 재질), 리허설 단계 동안 닫힘·이후 말려 올라가 사라짐.
  - `NightDoorLock` 리허설 규칙 → `WaitingRoomRehearsal.IsGateLocked`: 연습실 철창은 리허설 중 닫힘, 포기 시 열림(재진입), 성공 시 열림 → 모두 연습실 밖이면 다시 잠금(`practiceSealed`, 호스트 판정·공유). 무대 통로 셔터는 성공 뒤 열림. 나머지는 닫힘.
  - `ShopReach.Locked`: 리허설 단계 동안 상점·침대·스케줄표·선반 등 F 상호작용 전부 잠금.
  - `RingmasterMonitor`(신규): 로비 동쪽 벽·무대 통로 입구 위 모니터. 리허설 성공 뒤 누구든 통로 입구 9m 안 → 잡음 1.3초+치직 소리 → 가면 쓴 단장 얼굴(브라운관 깜박임). 얼굴 그림·소리는 코드로 만든 임시 자원(아트·사운드 교체 필요).
- 확인: 컴파일 PASS. 임시 캡처 테스트(삭제): 리허설 중 전부 닫힘 → 성공 직후 연습실·무대 통로 열림 → 연습실 밖이면 연습실 다시 닫힘, 신규 3곳·작업실·물류·지하 닫힘 유지, ShopReach.Locked = True, 통로 접근 시 두 모니터 단장 얼굴. 캡처 `.harness/local/rehearsal/lockdown/`.
- 미확인: 실제 걸어서 새 철창 사이 틈 여부(사람 플레이), 2인 접속 시 연습실 재잠금, 외줄 공연 뒤 대기실 복귀 시 해제 화면.
