# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-26 시작: origin/dev(158719b2, PR #488) 반영 후 fix/bugs-20261126 생성. doctor(-SkipEditor) FAIL 2: third-party HeadMismatch(사용자 결정으로 유지), task-ids 중복(기존 Task들, 이번 작업과 무관).

## 1·2 목마 집결 인식 (2026-11-26)
- 원인: "게임 시작 목마 2명까지"와 "입장 구역 목마 3층 이상 미적용"은 같은 버그. `StageEntranceGather`가 각자 발 위치+0.5m로 판정 상자(S07 프리팹 중심 y 1.5, 높이 3m) 안을 봤는데, 3층부터 발이 상자 위로 나가 집결 인원에서 빠짐 → 전원 집결이 안 돼 첫 공연(게임 시작) 전환이 안 됨.
- 수정: 목마에 탄 사람은 맨 아래 사람(`PiggybackSystem.GetCarrier`) 발 위치로 판정. 집결 카운트와 쉬는 시간 잡힘 판정(`RestTimeoutCatch.IsAtStageEntrance`) 모두 같은 기준. 목마가 아닌 사람은 기존 높이 판정 유지. 씬·프리팹 변경 없음.
- 검사: 새 `StageEntranceGatherStackTests` 5/5 PASS. 수정 전 동작으로 돌리면 3인·4인이 "Expected 3/4 But was 2"로 실패(피드백과 동일 재현).
- 회귀: CommonProgressionRegression 1171/1183. 실패 12건은 Task-204 기준선과 같은 목록(탈출로 아바타 인계 5, 늦은 봉 Release 3, 리프트 재부착 1, 탑승 감사 lift 1, 트램펄린 중복 응답 1, 리허설 대포 아트 1).
- 주의: 테스트 실행 중 TMP 동적 폰트 `Graffiti_Brush.asset`이 바뀜 → 작업 변경 아님, 커밋 제외.
- 남은 확인: 여러 PC 3~4인 목마 실제 플레이(사람).

## 3 대포 리허설 에셋 사라짐 — 조사 중, 재현 정보 필요 (2026-11-26)
- 코드 수정 없음. 피드백에 어떤 에셋이 언제 사라졌는지 없어 원인 확정 못 함.
- 확인한 것
  - 리허설 코스는 `RehearsalCourseFromPerformance.prefab` 복제본. 대포 메시는 `_ThirdParty/Task158/TouringCannon.fbx`(비공개 저장소). dev가 가리키는 _ThirdParty 커밋에 포함됨 → 저장소 누락은 아님. 단 _ThirdParty를 받지 않은/오래된 PC·빌드는 대포 메시가 빈다(공연 대포도 같이 비므로 리허설만의 문제는 아님).
  - 리허설 시작 시 연습실 소품(`PracticeLanes`·`CollisionRepairs`·`DomeSuspension`·조명 없는 `CanopyAndRig` 자식·발견된 대포/트램펄린 `PracticeProps`)을 숨기고 종료(Cleanup) 때 되살림. 대기실 상시 대포 시스템·트램펄린(`WaitingRoomStageDevices`)도 리허설 동안 비우고 끝나면 다시 만듦.
  - EditMode `RehearsalCannonSourceTests` 실패 1건: `CC_GripArt` 재질이 원본 씬 `CC_PropSurface` vs 복제 프리팹 `TouringRigging`. 6eb96569(공연 기물 외형)에서 프리팹만 손으로 바뀌고 원본 씬과 어긋남. 사라짐이 아니라 외형 불일치.
  - PlayMode `W6_CannonRehearsal_*` 5/11 PASS, 실패 6건은 사망·부활 규칙(무료 부활 기대값), 배치 후 추락, 첫 획득 안내 관련 — 에셋 표시 검사는 없음. 기준선 비교 미실행.
- 필요한 정보: 사라진 것(대포·트램펄린·발판·연습실 소품 중), 시점(리허설 중/종료 후/다음 날), 호스트·클라이언트 여부.
- 사용자 지시(2026-11-26): 일단 넘기고 다음 항목 진행.

## 4 연출 컷씬 한 명만 보임 — 보류, 재현 정보 필요 (2026-11-26)
- 코드 수정 없음. "한 명 화면에만 재생"인지 "컷씬에 캐릭터가 한 명만 보임"인지, 어느 컷씬인지 피드백에 없음.
- 확인한 컷씬(코드 기준 동기화 구조는 정상으로 보임)
  - 대기실 `DiscoveryCutscene`(녹슨 벽 발견)·`EscapeOpenCutscene`(나가는 길 개방)·`DormEntryEvent`(숙소 문 오작동): 판정 화면이 탐색 장부에 기록 → 신뢰 전송(ReliableFragmentedSequenced)으로 모두에게 → 각 화면이 false→true 변화를 보고 재생. 상태를 받기 전(StateReady 전)에는 보지 않음.
  - 탈출 성공 엔딩 `EscapeEndingCinematic`: 호스트가 도착자 명단을 고정해 모두에게 보냄. 각 화면은 자기 캐릭터만 들판 시작점으로 옮기고 다른 사람은 네트워크 위치로 따라옴 → 원격 위치 동기화가 늦거나 끊기면 다른 사람이 카메라 밖에 남아 "한 명만 보임"이 될 수 있음(가설, 미재현).
- 필요한 정보: 컷씬 이름/장면, 증상 종류(화면에 안 나옴 vs 캐릭터가 안 보임), 호스트·클라이언트 여부.

## 5 런 성공 후 로비에서 기물 사용 (2026-11-26)
- 원인: 공연 기물 장소 규칙(`PropCarry.PlaceAt/PlaceOf`)이 "대기실이 아니면 정상 사용(Normal)"이라 시작 씬(Lobby, SpaceId.Hub)에서도 Q/E 공연 기물이 발동·소모됨.
- 수정: 활성 씬이 시작 씬(SpaceRoute 기준 Hub)이고 다른 공간 씬이 떠 있지 않으면 Blocked. 기존 차단 안내("공연 기물은 연습실이나 공연장에서 사용할 수 있습니다")와 인벤토리 사용 키 숨김이 그대로 적용. 장소 지정 없는 개발 확인 씬·공연·탈출로는 기존대로 Normal.
- 범위: 공연 기물(ItemUse.Performance) 발동만. 로비에서 기물 소지·표시 자체는 그대로.
- 검사: 새 `HubEquipmentPlaceTests` 3/3 PASS. 수정 전 동작으로 돌리면 시작 씬 검사가 "Expected Blocked But was Normal"로 실패.
- 회귀: CommonProgressionRegression 1174/1186, 실패 12건 기준선과 동일.
- 남은 확인: 실제 런 성공 → 로비 복귀 후 Q/E 사용 막힘(사람).

## 4 추가 정보 (2026-11-26 사용자)
- "연출 컷씬은 플레이어 동기화가 안 되는 것 같다. 튜토리얼이나 컷씬이 안 나오는 현상이 있다고 들었다." → 증상은 일부 화면에서 연출·튜토리얼이 재생되지 않음.
- 배제: 탐색 장부 상태 크기(ShopChannel 상한 16KB, UTF-16이라 약 8천 자). 씬의 탐색 ID 46개 전부 열려도 약 1천 자 → 상한 문제 아님.
- 다음: 2인 이상 실제 접속(MPPM)으로 참가자 화면 재현 필요. 코드 읽기로는 재현 경로 미확정.

## 6 프레임 드랍 측정 (2026-11-26)
- 도구: 기존 `Tools/Curtain Call/Performance/Measure Selected Waiting Room`(에디터 Play, 혼자). 보고서 `Logs/Performance/pursuit-waitingroom-20261126-144311.json`. 공연장은 같은 측정 도구가 없어 미측정.
- 초반 끊김(씬 로드): 로드 2.4초, 준비 4.8초, 최장 프레임 1.66초(1초 넘는 프레임 1개). 그 프레임 상위: 씬 통합 1.28초 중 GC.Collect 324ms, 추격자 생성·초기화 187ms, Destroy 79ms, ClothScene 39ms.
- 상시 렌더 부하: Lobby 시점 batches 6723(기준 3000), 그림자 캐스터 4789(기준 2000), 삼각형 247만. 숙소 batches 3925·그림자 3496. 씬 전체 렌더러 24137개 중 정적 배칭 412개, 실시간 조명 296개(그림자 조명 11).
- 판단: 원인이 씬·에셋 구성(배칭·그림자·LOD·조명)과 로드 시점 GC·추격자 일괄 생성이라 버그 수정 범위를 넘는다. 씬 YAML·기존 에셋 대량 수정이 필요해 PM·에셋 관리자 확인 대상 → 별도 성능 Task로 분리 권장. 이 Task에서는 코드 수정 없음.
- 저사양 PC·빌드 수치는 없음(에디터 수치).

## 고도화 1 관리실 단말기 입력 분리 (2026-11-26)
- 사용자: 남은 버그는 없음, 같은 브랜치에서 고도화 진행. 리허설·가이드·설명 항목도 추가 요청 → plan "고도화 진행"에 목록화.
- 변경(`ExplorationPanel`): 날짜(1/2 DATE)와 암구호(2/2 CMD)를 단계로 분리. 날짜 자리를 다 채우거나 ENTER면 암구호 단계, 암구호 단계 ENTER로 제출. 날짜 단계 글자·암구호 단계 숫자는 무시하고 안내. 빈 암구호에서 DEL이면 날짜 단계로 복귀. 활성 줄 ">" 표시·비활성 줄 흐리게.
- 안내 줄(초록): 1단계 "단서 2개(날짜·암구호) 필요 + 날짜 {n}자리 + 키보드로 바로 칠 수 있음", 2단계 "암구호(영문) 입력 후 ENTER + 키보드". 오답(빨강)은 다음 키까지 유지 후 단계 안내로 복귀. 실제 키보드 숫자 입력도 같은 단계 규칙을 탄다.
- 판정 값(`날짜 + "\n" + 암구호`)·`ExplorationGate` 판정은 그대로.
- 문구: `WaitingRoomTexts.txt`에 `gate.terminal-guide-*` 6개 추가, `gate.terminal-error`를 한국어 설명으로 변경(키 유지).
- 검사: 새 `TerminalTwoStepInputTests` 7/7 PASS. 화면 배치·글자 크기는 사람 확인 필요.

## 고도화 2 리허설·가이드·설명 (2026-11-26)
- 그네(`PoleControlGuide`, 신규): 대기실(리허설·연습실)에서 내가 처음 그네를 잡으면 왼쪽 가이드 1회(런마다). 직접 잡은 사람 = "W/S 반동 · Space 도약 · F 길게 놓기". 매달린 사람 = 공통 봉 설정 `TopRiderOnlySwing`이 켜져 있으면 "반동은 위 동료가 W/S로 줌", 꺼져 있으면 "함께 반동 가능"(반동 입력은 `PoleSettings.CanDrive(seat)` 기준).
- 대포 내리기: 대포 리허설 안내에 `cannon-exit` 추가(탑승 중 "F 길게 = 발사 없이 내리기, 짧게 = 발사", `CannonSeatState.Read` 기준).
- 휴대 절단기(`EscapeExitSaw`): 발사형 절단기를 처음 잡으면 화면 위 시스템 알림 1회 "갖다 대는 게 아니라 쏘는 장치, 조준하고 {장치 사용 키}로 발사".
- 배터리: 꽂았을 때 결과 문구에 "다시 F를 누르면 빼서 다른 곳에 쓸 수 있습니다" 추가(`explore.socket-in`).
- 공연 기물(`PerformanceItemGuide`, 신규): 공연 기물이 소지 칸에 처음 들어오면(런마다 기물별 1회) 화면 위 시스템 알림 "{기물} 사용법 + 상점 설명 + {Q/E}로 사용, 연습실·공연장". 기물별 `guide.piece.<기물 ID>.body`로 덮어쓰기 가능.
- 문구: `WaitingRoomTexts.txt`에 `guide.pole-*`, `guide.saw-fire.*`, `guide.piece.*`, `cannon-rehearsal.guide.cannon-exit.*` 추가.
- 검사: 새 `M3GuideTextTests` 5/5 PASS. CommonProgressionRegression 1174/1186(실패 12건 기준선 동일).
- 기존 실패(HEAD 문구 파일 대조로 판단, 기준선 재실행은 안 함): `WaitingRoomTextTests` 3건, `WaitingRoomHudProfileTests.PerCueOverrides...` 1건.
- 미착수: 공연 난이도 설정 가이드, 치료 위치·서비스 예약 후 엘리베이터 맥락 안내. 노출 시점·화면 겹침은 플레이 확인 필요.
- 사용자 지시: 단순 가이드 외 단서·동선·스토리·설정은 임의로 정하지 말고 정보를 먼저 요청한 뒤 반영.

## 기물 사용법 안내 → 화면 위 가운데 시스템 알림 (2026-11-26 사용자)
- 요청: 공연·탐색 기물 사용법은 왼쪽·오른쪽 가이드 패널이 아니라 가운데 시스템 설명으로. 이미 들어간 내용 위주로 바꾸고, 빠지거나 필요한 내용은 먼저 사용자에게 전달.
- 변경(데이터만): `Settings/UI/WaitingRoomHud.asset` 튜토리얼 역할을 학습 가이드(왼쪽) → 시스템 알림(위 가운데 배너, RoomNotice.Notice)으로: G05 기물 내려놓기, G06a 머리핀, G06b 말뚝 뽑개, G06c UV 등, S03 손이 가득 참, S04 내구도 소모. 문구는 그대로.
- 이미 시스템 알림: 휴대 절단기 첫 잡기 안내, 공연 기물 첫 획득 안내(`PerformanceItemGuide`).
- 그대로 둠(사용법이 아니라 결과·피드백): 탐색 대상 결과 문구(ToolTarget·BreakableWall, 2026-11-14 사용자 결정으로 왼쪽), 기물 파괴·남은 횟수(오른쪽), 사용 불가 장소 안내(오른쪽 피드백).
- 검사: `M3GuideTextTests` 11/11(역할 6건 포함), `WaitingRoomTutorialBookTests` 10/10. `WaitingRoomHudProfileTests` 4/5 — 실패 1건은 변경 전부터 있던 것(역할 값과 무관한 G01/G04 덮어쓰기 검사).
- 사용자에게 확인 요청: 사용법 문구가 없는 탐색 기물(열쇠류·배터리·키 카드 복제본·비디오 테이프), 공연 기물 상점 설명 길이, "가운데"의 위치 해석, 시스템 알림 1개씩 교체 표시.

## 기물 첫 획득 배너 → 왼쪽 가이드, 시스템 배너 차례 (2026-11-26 사용자 답변 반영)
- 사용자: 열쇠류 제외. 배터리 = 꽂으면 전력 연결·문 열림, 빼면 닫힘. 키 카드 복제본 = 관리실 문을 여는 카드. 비디오 테이프 = "재생할 기기가 필요할 것 같다." 공연 기물은 한두 줄 요약. 최초 획득 때 가운데 시스템 배너 → 배너가 사라지면 왼쪽에 기존 가이드. 가운데 = 기존 시스템 알림 배너. 배너는 한 번에 하나, 사라지기 전 다음 것 받지 않고 차례대로.
- 되돌림: HUD 설정 튜토리얼 역할(G05·G06a~c·S03·S04) 변경을 원래(왼쪽 가이드)로 복구.
- `ItemPickupGuide`(PerformanceItemGuide 대체): 런마다 기물별 첫 획득 시 배너 요약(`guide.item.*`). 공연 기물은 배너 뒤 왼쪽에 기존 상점 설명, 탐색 도구 3종은 기존 튜토리얼(G06a~c)이 이어 받음 — 튜토리얼은 `ItemPickupGuide.Busy` 동안 대기. 배터리·키 카드·테이프는 배너만.
- `HudUIManager`: 가운데 시스템 배너(오른쪽 위 사망 경고 제외)는 앞 배너가 화면에서 사라질 때까지 카드를 꺼 둔 채 줄 세움. 호출한 쪽이 닫으면 건너뜀, 같은 주인 치우기(DismissOwned)는 줄에서도 치움, 경고음은 실제 표시 때.
- 검사: `M3GuideTextTests` 25/25, `SystemEventQueueTests` 2/2, `RoleHudPlayTests`(PlayMode) 5/5, 공통 회귀 실패 12건 기준선 동일. `WaitingRoomHudPlayTests`(PlayMode) 2건 SetUp(오프라인 런 시작 `StartOfflineRun/EndIntro/EndPerformance`)에서 실패 — 기준선 비교 안 함.
- Confluence 50659329 v13: 3·6·7 섹션에 "수정 결과" 토글(완료 항목만) 추가.
- 캡처: 에디터 WaitingRoom 플레이(오프라인, 내 캐릭터 없음)에서 실제 UI 코드를 직접 호출해 촬영(`.harness/local/captures/01~08`). 왼쪽 가이드는 캐릭터가 없으면 튜토리얼이 닫아서 같은 가이드 카드를 별도 주인으로 띄움. 실제 획득 흐름·여러 PC는 사람 확인.

## 관리실 단말기 재설계 · 배너 형식 (2026-11-26 사용자)
- 단계식 입력(이전 변경) 롤백 → 원래 `ExplorationPanel` 기준으로 다시 작성. `TerminalTwoStepInputTests` 삭제.
- 새 규칙(`TerminalEntry`, 신규): 암호 2개(날짜·암구호)를 따로 입력·저장. 칸 수 = 정답 글자 수(현재 날짜 8칸, 암구호 RUN → 3칸). 두 칸 모두 자판의 모든 글자(숫자·영문) 입력. ↑↓·클릭으로 입력란 선택. 칸을 다 채우면 그 칸만 확인 → 맞으면 줄 긋기(<s>)+OK+다음 칸, 틀리면 Error 후 그 칸만 비움. 닫아도 입력 유지(단말기 `ExplorationGate.TerminalState`, 내 화면, 런 바뀌면 처음부터). 둘 다 맞으면 기존 판정 요청(날짜 + "\n" + 암구호). 판정 화면 거절 시 전체 초기화.
- 칸별 확인은 각 화면이 단말기 설정값으로 함(`MatchesTerminalPart`), 문 열기는 그대로 판정 화면. 안내 줄 `gate.terminal-help`.
- 배너: 위 = 기물 설명(본문), 아래 = 조작법(배너 보조 줄 Condition, #B8B29C, 항목 사이 띄움). `HudEventAppearance`가 보조 줄에 값이 있을 때만 맨 아래에 그 색으로 덧붙임(다른 배너는 보조 줄이 비어 기존과 같음). 기물 배너는 Reward(초록 선물 마크). 배너 본문은 서식 태그를 글자로 보여 주므로 색 태그 대신 보조 줄 사용. 문구 `guide.item.<ID>`(설명) / `guide.item.<ID>.controls`(조작법, | 로 항목 구분).
- 실수: 캡처용 플레이 모드를 켠 채 `HudEventAppearance`·`HudUIManager`·`ItemPickupGuide`를 수정함 → 바로 플레이 종료 후 재컴파일. 이후 사용자 지시로 캡처 중단.
- 검사: `TerminalEntryTests` 7/7, `M3GuideTextTests` 25/25, `SystemEventQueueTests` 2/2, `RoleHudPlayTests`(PlayMode) 5/5, CommonProgressionRegression 1174/1186(실패 12건 기준선 동일). 보상 배너 회색 줄 화면 확인은 캡처 중단으로 미확인.
- 대기: 사용자가 기물 설명 문구를 정함. Confluence 6-1(관리실)·6-6(배너) 설명은 이전 방식 기준이라 갱신 필요.

- 2026-11-26 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
