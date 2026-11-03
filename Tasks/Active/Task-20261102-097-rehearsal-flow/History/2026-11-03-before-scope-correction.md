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

## 2026-11-03 임의 변경 되돌리기 (사용자 지시)

- 사용자: 임의로 바꾼 내용은 원래대로, 기획과 다른 내용은 먼저 제안. 과반수 카운트다운 포기는 인간 대포 리허설 규칙. 대기실 120초는 임의 추가. 허공에 뜬 물체 금지.
- 되돌림:
  - WaitingRoomTutorial 가이드 쉬는 시간 120초 자동 완료 제거(전원이 안내를 마쳐야 타이머 시작).
  - 최초 리허설 포기 투표(F 길게·과반수 30초)·포기 상태·링 재진입 자동 이어하기·포기 시 연습실 철창 열림·로비 이동 제거, 관련 문구 키(rehearsal.giveup*, rehearsal.abandoned*) 삭제.
  - 무대 입구 통로 입구 앞 허공에 띄운 모니터 제거 → 통로 안(z 59) 천장 높이를 레이캐스트로 재서 막대가 천장까지 닿게 매닮(천장 못 찾으면 만들지 않음). 로비 모니터는 동쪽 벽면을 재서 뒷면이 벽에 닿게 검(약 20cm 떠 있던 것 수정).
- 미확인: Unity 연결 끊김으로 컴파일·화면 확인 못 함. W6_Rehearsal_BuildsCourse 테스트는 "리허설 중 연습실 철창 열림"을 기대해 현재 규칙과 맞지 않음(테스트는 사용자 요청 시).
- 2026-11-03 권장안 진행(사용자): 출발대·도착대·중간 발판 아래 네 모서리 받침 기둥(실제 발판 윗면을 레이캐스트로 재서 링 바닥까지), 끊긴 구간 점선을 줄 바로 아래 링 바닥에 그림. 나머지 공란 항목은 사용자 결정 전까지 현재 값 유지. 남은 떠 있는 것: 공연 그네 틀(줄 높이에서 시작)·해제봉 매다는 줄 위 끝 — 공연 장치 그대로라 별도 제안 필요. Unity 연결 끊김으로 컴파일 미확인.
- 2026-11-03 받침 추가(사용자): 리허설 코스에서만 그네 틀 기둥(줄 옆 ±1m, 매다는 자리)을 줄 높이에서 링 바닥까지, 트리거 그네(해제봉) 매다는 줄 위 끝(손잡이+1.6m)에 가로대와 양쪽 기둥(±1m). 충돌체 없음(줄 이동·그네 비행 방해 안 함). 공연 코스는 그대로. 컴파일·화면 미확인(Unity 연결 끊김).

## 2026-11-03 첫 공연 전후 기능 연결 (진행 중)

- 사용자 확정: 첫 외줄 공연 → 대기실 tutorial → **1일차 두 번째 공연** → 밤 정비. 리허설 공연은 현재 제작본을 유지하고, 다른 기능을 먼저 연결한 뒤 기능 검증과 대기실 버그를 수정한다. 추락·실패 시 위쪽 구간 출발 위치로 순간이동하는 방식 유지.
- `RunState.NotifyAllAtStageEntrance`: 가이드 쉬는 시간에 튜토리얼이 완료되지 않았다면 집결로 다음 공연에 넘어가지 못하도록 차단.
- `StageEntranceGather`: 거절된 집결은 완료로 기록하지 않고, 튜토리얼을 마친 뒤 전원이 계속 입구에 있으면 전환을 다시 시도.
- `WaitingRoomRehearsal`·`NightDoorLock`: 리허설부터 첫 공연 전환 중까지 철창과 상호작용 제한 유지, 첫 공연 후 가이드 쉬는 시간 진입 시 해제.
- `WaitingRoomSpawn`: 최초 리허설 진입 시 연습실 바닥 충돌면을 확인해 그 위에 플레이어를 배치. 바닥이 없으면 임의 좌표로 놓지 않고 기존 리허설 배치를 기다림.
- `ShopReach`·`ExplorationState`: 첫 공연 전 상호작용 잠금을 입력 선택과 호스트 탐색 판정에 함께 적용.
- `RehearsalPathGuide`: 점선에 방향 화살표 추가. 철창 개방 뒤 길을 굽고, NavMesh가 완전한 경로를 찾지 못하면 벽·철창 관통 직선을 표시하지 않음.
- 이번 묶음은 사용자 요청에 따라 Unity 기능 검증·대기실 버그 재현을 뒤로 미룸. 컴파일·실제 동선·다인 접속은 미확인.


## 2026-11-03 기능 연결·편집 가능한 임시 퀘스트
- 사용자 승인 계획에 따라 리허설 코스 제작본은 유지하고, 첫날 두 공연·대기실 안내·밤 정비의 실제 상태에 임시 우측 퀘스트를 연결했다. 퀘스트는 안내 끄기 설정과 독립적으로 표시하며 기존 팀 대기 패널·스케줄표 창과 겹치면 숨긴다.
- 문구는 WaitingRoomTexts.txt의 first-day.quest.*, 패널 위치·너비·제목/본문 크기는 first-day.panel.*. 리허설 우측 패널도 같은 수치를 사용한다. 좌측 안내 파일은 기존 그대로다.
- 가이드 쉬는 시간 무제한 설정에서도 전원 안내 완료 전 집결 전환을 차단하고, 완료 후 원래 무제한 상태로 복구한다.
- 동기화: origin fetch 완료, origin/dev는 현재 HEAD에 포함돼 병합 불필요. start-work는 기존 Task-097 중복 때문에 실패했으나 feat/rehearsal-flow와 정확한 Task 경로를 확인했다. doctor는 앞서 실행한 결과를 재사용했다.
- 검사 중간 결과: 컴파일 오류 0. RestTimeRulesTests 3/3 PASS(2인 완료 대기→같은 날 두 번째 공연→밤 포함). 전체 check-work는 안전성·문서·컴파일 PASS 후 검사 도중 문서·테스트 수정으로 fingerprint 실패. 최종 변경 후 재검사 예정.
- 첫 PlayMode 연결 테스트는 안내 완료 직후 퀘스트 Visible을 한 프레임만 기다려 FAIL. 패널이 그려질 때까지 유한 대기하도록 바꾸고 실제 캐릭터 상태를 실패 메시지에 포함해 원인을 구분한다. 결과 후속 기록.

- 재검사: W6_FirstDay_GuideSecondPerformanceAndNightQuests 1/1 PASS(90.7초). 안내 완료→집결→1일차 두 번째 공연→EndPerformance 공개 신호→첫날 밤→스케줄 확정→취침 퀘스트 전환 확인. 실제 공연 조작·다인 접속을 대체하는 검사는 아니다. 화면 캡처 `.harness/local/rehearsal/first-night-quest.png`에서 좌측 안내와 우측 취침 퀘스트 표시 확인.
- 추가 회귀: W5_Tutorial_NightShowsAndDisabledShowsNothing 1/1 PASS(155.3초). 퀘스트는 안내 끄기와 독립, 밤 연계 안내 순서·일정 확정 뒤 취침 안내·스케줄표 열 때 숨김/닫으면 복원 확인. 단독 호스트 자동 검사이며 실제 2인 접속·공연 입력·새 철창 틈은 미확인. 커밋·push 없음.
- 최종 check-work -NoRecord: exit 0, 안전성 PASS·문서/규칙 PASS·컴파일 PASS. 해당 명령의 테스트는 SKIPPED(선택 안 함), 별도로 실행한 EditMode 3/3·PlayMode 2/2 PASS와 구분한다. 검사 중 파일 변경 없음.


## 2026-11-03 5단계 최소 기능·실동선 검사
- 사용자 지시: 필요한 기능 검사만 하고 체감·상세 조율은 사람이 확인. 반복 전면 회귀 대신 단일 리허설 스모크 뒤 실패 구간만 좁혀 확인한다.
- 편집 누락 수정: 리허설 우측 제목·좌측 단계 설명도 WaitingRoomTexts.txt rehearsal.stage.<ID>.title/intro에 연결. 2-2 자동 잡기처럼 읽히던 설명을 공중 F 잡기로 정정. 코스·조작·완료 규칙은 유지.
- 새 철창 3곳 입력 검사: 중앙·양끝 바깥 9지점 중 8곳을 일반 이동 입력으로 밀었으며 통과 0. 숙소 한쪽 끝은 시작점이 기존 벽으로 막혀 이동 불가. 보고서 .harness/local/rehearsal/verification/closed-gate-probes.txt. 각 시도 시작 위치만 배치했으며 길을 걸어서 우회하는 전수 검사는 아니다.
- 리허설 기능 스모크: 추락 상태 요청→호스트 복구→같은 단계 출발대, 1단계 도착→2-1 전환, 2-2 반동·놓기·F 잡기 요청→두 번째 그네 매달림 확인. 트리거 직전 FAIL: 매달린 채 시험이 다음 코스로 강제 전환한 상태가 섞임. 다음 코스 배치 전 봉 이탈을 명시하도록 시험 정리.
- 트리거·출구만 좁힌 검사: 새 상태에서 2-3 잡기→연결·놓기→끊김, 3단계 짝 줄 연결, 최종 도착→완료까지 정상. 이후 길안내 PathInvalid로 FAIL.
- 실제 원인: StageEntranceGather 집결 위치(-6,0,-10.15)가 기존 NavMesh 생성 범위(z>=0) 밖이다. RehearsalPathGuide 생성 범위에 출발점·목적지와 여유 12m를 포함하도록 수정. 출구 이동만 재검사 예정.
- 캡처: .harness/local/rehearsal/verification/01~10과 gate-*.png. 퀘스트 편집 방법은 quest-editing.md에 기록.
- 출구만 재검사 W6_Rehearsal_ExitRouteSmoke 1/1 PASS(133.3초). NavMesh 경로 10개 점 생성, 8개 경유점에서 캡처, 마지막 구간을 실제 이동 입력으로 걸어 집결→Performance 상태 전환 확인. 최종 캡처는 공간 전환 커튼이며 공연 씬 로딩 완료 증거는 아니다. 캡처 사본은 Previews/Rehearsal/, 순서 설명은 verification.md.
- 최종 check-work 1차: 안전성 PASS, 문서/규칙 FAIL(내가 복사한 철창 캡처 파일명 3개가 대문자·밑줄을 포함). 소문자 하이픈 파일명으로 바꾸고 문서 링크를 수정했다. 코드 컴파일은 앞선 별도 실행 오류 0이며 최종 필수 검사를 다시 확인한다.
- 최종 check-work 재확인 exit 0: 안전성 PASS·문서/규칙 PASS·컴파일 PASS. 전체 자동 테스트는 선택하지 않음(SKIPPED), 필요한 기능 스모크의 부분 확인·실패·출구 수정 후 PASS 기록과 구분한다. 실제 다인 협동·전체 우회 동선·조작 체감은 사람 확인 후속. 커밋·push 없음.


## 2026-11-03 사용자 재현 지적과 수정
- 이전 완료 보고 정정: 강제 위치·상태/서버 잡기 요청으로 검사한 결과를 실제 키 입력·추락 후 재진행 확인처럼 보고했다. 실제 공연 아트 프리팹과 균형 HUD 연결도 빠졌다. 현재 사용자 지시는 기존 제작본 유지보다 우선한다.
- origin/dev 56363116 반영(merge --no-commit). Unity 종료 확인, 기존 머티리얼·서브모듈 변경 보존. CampaignRun 충돌은 PursuitEnabled와 StartSession(true)를 모두 보존, 지식 문서는 양쪽 내용 보존. 씬 충돌 없음. 커밋·push 없음.
- 기존 TightropeCoursePrepared와 PerformanceHud/C1CheerGauge·PerformanceSceneBinding 재사용. CourseShapeSync는 리허설 단계 공유와 덮어쓰지 않도록 비활성. 새 입력·균형 계산은 추가하지 않는다.
- 코스 길이 40m로 연장, 첫 단계는 왼줄 18~28m 실제 끊김과 연결된 옆줄. 제목·설명 파일도 정정.
- 새 실제 입력 검사 1차: W로 줄 진입·기존 균형바·E+Space 옆줄 점프 통과, 실제 끊김 추락에서 FAIL(Normal로 바닥에 남음). 원인은 TightropeCourse.CheckLocalFall이 줄에서 벗어나 일반 이동으로 전환된 사람을 UsesRopeRules에서 제외한 것. 연습 모드 Running 동안은 기존 깊이 판정을 계속 적용하도록 최소 수정. 재검사 중.

- 사용자 재정정: 세 개를 같게 만드는 것이 아니라 신규 대체 구현을 기존 외줄 시스템으로 교체하고 각각 기획대로 수정할 것. 공통 40m와 1단계 왼줄 재연결 패턴을 폐기, Confluence 32702474 d1:70 및 구조도 1~3 재대조. 중간 발판·2단계 세 소구간·3단계 네 줄 짝 트리거를 보존한다. 배치 길이는 원문 공란이므로 늘어난 임시 배치값으로만 기록한다.
- 실제 입력 재검사 과정: 추락 수정 뒤 W 재진입까지 확인, 인간 그네 반동 검사에서는 검사 자체가 놓기 가능 기준보다 이른 예측 거리에서 멈춰 FAIL. 원본 CanLetGo 신호가 될 때까지 W/S 입력을 계속하도록 수정. 다른 실행의 E+Space 실패는 한 프레임 입력·균형 방치 조건도 섞여 있어 실제 키를 여러 프레임 유지하고 이동 중 A/D로 균형을 보정하도록 검사 보완. 통과로 기록하지 않음.
- 현재 검사는 실제 F/W/S/Space 기본 그네·연속 그네·트리거 연결/해제까지 포함한다. 단계 간 시험 시작점 배치는 분리해 기록한다. 검사 실행 중。

- 최종 실제 입력 검사 W6_Rehearsal_PerformanceInputHudAndFallRecovery 1/1 PASS(48.1초). W 진입·A/D 균형 보정·E+Space 옆줄 점프·실제 추락 복귀와 W 재진입·기본/연속 그네 F/W/S/Space 및 공중 W 착지·2-3/3단계 F 연결/Space 해제 확인. 시작 위치·단계만 준비하며 Q 방향·2인 협동 완주는 미확인. 최신 캡처 11장은 Previews/Rehearsal-reuse/, 설명은 reuse-verification.md.
- 최종 check-work -NoRecord exit 0: 안전성 PASS, 문서/규칙 PASS(길이 경고만, hard 0), Unity 컴파일 PASS(up_to_date). 해당 명령의 자동 테스트는 SKIPPED이며 별도 실제 입력 PlayMode 1/1 PASS와 구분한다. 기존 private 에셋 변경은 보존. dev 병합은 커밋 전 상태이며 새 커밋·push 없음.

## 2026-11-03 캡처 추가 요청
- 기존 연결 시나리오 첫 재실행은 밤 FirstDayQuestVisible 대기 실패(71.5초). 제품 버그 확정 아님. 기존 테스트에 단계별 화면 저장·실패 상태 진단만 추가. 재실행 1/1 PASS(68.6초), 생산 코드 수정 없음. 최초 실패 원인은 미확정이며 간헐 가능성은 남긴다.
- 대기실 안내·밤 일정·취침 준비 캡처 확보. 준비 퀘스트는 별도 dev 시작 배치와 메모리 전용 안내 끄기로 캡처. 패널 숨김 프레임·전환 커튼은 증거에서 제외. 첫/두 번째 공연 패널·취침 후 다음 날 화면은 미확보. Previews/Quest-flow 및 capture-walkthrough.md에 실제 범위 기록.
- 캡처 추가 후 check-work -NoRecord exit 0: 안전성·문서/규칙·컴파일 PASS. 전체 자동 테스트 SKIPPED, 위 단일 연결 검사 재실행 PASS와 구분. 캡처용 Play 종료, 커밋·push 없음.

## 전체 코스·후속 화면 원인 조사
- fetch origin/dev 결과 HEAD..origin/dev=0. 병합 불필요, 기존 변경 보존. doctor는 같은 Task에서 앞서 실행했으므로 반복하지 않음.
- 묶음 필터는 도구가 정규식으로 해석하지 않아 total 0(통과 아님). 개별 정확한 이름으로 실행.
- 확장 연결 검사 최초 FAIL: 공연 씬·Normal·커튼 해제·전환 종료인데 퀘스트가 비어 있음. 별도 실제 첫 공연 시작으로 재현, firstDayQuest 루트/뷰는 활성이나 두 TMP label.font가 NULL.
- 원인: WaitingRoomRightPanel이 옛 UI 소유자의 임시 폰트를 보관한 채 씬 전환. 옛 UiRuntimeFontFallback이 삭제되면 복제 폰트도 삭제됨. Unity 비교에서 파괴된 fontOwner와 새 null이 같아 변경 검사를 통과하고 패널이 잘못 재사용됨. 관리 참조의 동일성 비교로 바꾸고 공연에서는 CommonInteractionUI의 폰트 보완 소유자를 사용한다.
- 별도 검사 결함: 커튼 시작 전 false를 종료로 오인하던 대기. SpaceLink.IsTransitioning=false·커튼 해제·해당 퀘스트 안정 표시를 함께 기다리며 실제 뷰/폰트도 검사한다.
- 수정 후 첫 공연의 실제 툴팁 표시·폰트 참조는 통과하고 00-first-performance.png 확보. 이어지는 대기실 복귀가 35초 대기 한도를 넘어 FAIL(커튼 활성·Normal·공간은 대기실). 기존 Launch의 75초 로딩 예산과 맞추되 조건이 성립하면 즉시 종료하도록 수정하고, 이미 확인한 첫 공연은 반복하지 않고 Between부터 좁힌다.
- 중간 실행 1건은 로딩 중 eval 진단 명령의 메인 스레드 시간 초과 오류 로그로 중단됨. 제품 실패와 구분하며 이후 실행 중 eval을 보내지 않는다.
- 코스 촬영 1/1 PASS(88.4초)지만 이미지 육안 확인에서 줄 모듈 누락·저조도 발견. 프레임 중 생성된 모듈의 레이어가 누락된 캡처 도구 문제이므로 생성 완료 뒤 레이어 분리·즉시 렌더·검토 조명을 적용해 다시 촬영한다. 이 이미지는 완료 증거로 배포하지 않는다.
- 최종 전체 배치 촬영 1/1 PASS(57.3초). 1600×1200 검토 카메라+임시 조명으로 실제 런타임 코스만 촬영. 5장 모두 육안으로 줄·발판·그네·점선 끊김 표시 확인. Previews/Overview에 보관, 코스 배치 변경 없음.
- 최종 좁힌 연결 검사 1/1 PASS(87.2초). 실제 툴팁 IsVisible·TMP 폰트 검사, Between 안내→집결→두 번째 공연 표시→공개 종료 신호→밤 일정→확정→침대 Interact→2일차 RestTime·침대 시점 해제 확인. Day 2 오버레이 캡처 확보. 첫 공연의 앞선 부분 확인과 구분한다.
- 상태 추적에서 Performance 직후 공간 WaitingRoom·moving=true·curtain=false를 실제 확인. 이전 커튼 대기의 조기 통과 원인을 재현했다. 최종 증거 full-course-and-night.md, Previews/Overview(5장), Previews/Progress(8장), 결과 JSON 두 개, quest-transition-trace.txt.
- 최종 check-work -NoRecord exit 0: 안전성·문서/규칙·컴파일 PASS, 해당 명령의 자동 테스트 SKIPPED(추가 선택 안 함). 별도 최종 촬영 1/1·후속 연결 1/1 PASS와 구분. 에디터 WaitingRoomBase 활성·씬 dirty 0 확인. 새 커밋·push 없음.

