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
