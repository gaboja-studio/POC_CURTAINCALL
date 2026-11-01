# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-10-31 Task 생성. 추천안 7건 채택(plan 결정). 브랜치 feat/waiting-room/tutorial을 design-rooms 2e906f11에서 분기(기존 미커밋 직렬화 변경 8건·_ThirdParty 그대로 따라옴). origin/dev 3커밋 미병합 — 착수 때 처리.
- 2026-10-31 문구 초안 23항목(가이드 13(도구 3종 분리 포함)·밤 5·아침 1·상황 4) 작성. 근거: 타이머 재생 구역(ExplorationTimerZones), 조사 화면 조작(ClueInspectable), 기본 2칸·가판대(shop-storage), 외팔 자동 부활(injury-rules #4). 기획 문서와 10-30 구역별 타이머 규칙 차이 확인 필요.
- 2026-10-31 문구 2차: 사용자 답변 반영(구현 기준·추격자 제외·G08 60초·G12 열쇠 구멍 추가·존댓말). G11은 RestTimeoutCatch 즉시 잡힘 그대로, N01 추격 언급 삭제. 세대 교체 확인: 앱 0.2.0이라 데모 종료 아님 → 4일차 아침 만료(Deadline)·호스트 포기 시 다음 세대 1일차, PropCarry가 소지·바닥 기물 비우고 물려줄 선반 4칸 유지. N04·M01 보류.
- 2026-10-31 문구 3차 확정: N04 물려줄 선반 포함, M01 탈출 기한(③ 2일차 아침) 삭제. 총 23항목.

- 2026-10-31 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-10-31 착수: start-work.ps1이 3단 브랜치 이름(feat/waiting-room/tutorial)을 거부해 수동 진행. 에디터 종료 확인 후 origin/dev 3커밋 병합(5da1e1ea, 충돌 없음, 미푸시). doctor FAIL 3건(_ThirdParty dirty·다른 Task 번호 중복·CLI 에디터 미탐지)은 이 작업 무관.
- 구현: WaitingRoomTutorialBook(Logic)·WaitingRoomTutorial(RuntimeInitialize 자동 생성)·WaitingRoomTutorialCatalog(+메뉴 Create Tutorial Catalog, 에셋 생성)·CommonSettingsUI 게임 탭 3행. 게임 규칙 변경 없음. 시간대가 바뀌면 못 띄운 안내 줄은 비움. G11A(소집 경보 때 짧은 재안내) 추가.
- 발견·수정: ① GetBindingDisplayString이 한글 자판에서 'ㄹ' → InputControlPath 위치 이름 사용. ② WaitingRoomGuidePanel 한글이 □로 깨짐(UiRuntimeFontFallback 허용 목록 밖) → 목록에 추가. 10-30 인간 대포·신체 복구 안내도 같은 문제였음(공용 UI 파일 1줄 수정).
- 검사: 컴파일 PASS, EditMode 5/5, PlayMode W5_Tutorial 2/2, 회귀 W3_ 4/4·W2_BodyRestore 2/2·SettingsUpgrade 5/5. check-work 문서/규칙 FAIL(Task-079 log.md 138줄 hard). 캡처 7장(임시 Explicit 테스트, 사용 후 삭제)으로 패널 확인. MPPM 2인·사람 플레이 미실행.
- 2026-10-31 사용자: 가이드와 나레이션 구분 → 튜토리얼은 툴팁 정본(Tooltips 프리팹) 패널 모드의 `WaitingRoomTutorialPanel`(왼쪽, 시계 아래, 정렬 340)로 분리. 상단 `WaitingRoomGuidePanel`은 나레이션(인간 대포 획득·신체 복구) 전용. 시간대 전환·대기실 이탈·끄기 시 가이드 즉시 닫음.
- 사용자: 설명 문구는 직접 수정 → 에셋(유니코드 이스케이프라 편집 불가) 방식 삭제, `WaitingRoomTutorialTexts.txt`(ID | 제목 | 본문)로 교체. EditMode 7/7(파싱·전 ID 포함).
- 발견·수정: 공용 UI 씬 재로드 때 UiRuntimeFontFallback이 폰트 보완본을 파괴해, 계속 살아 있는 두 패널 글자가 폰트를 잃음(테스트 순서 실패로 재현). 보완 소유자가 바뀌면 패널을 다시 만든다. PlayMode W5 2/2(연속)·W3_HumanCannonSet 1/1·W2_BodyRestore 2/2.
- 2026-10-31 사용자: 안내 기록은 런 단위, 지시형("~하십시오") 대기실 가이드 누락 지적 → 기획서(게임 흐름 4-4·7-1·5-1, 대기실 2차 4.1.3, M2 실행 계획) 대조해 directive-guide-plan.md 작성. 충돌: 2차 기획서 가이드 중 구매·3칸 ↔ M2 밤 구매·2칸.
- 2026-10-31 사용자: M2 결정이 최신 → M2 변경점 27721807 원문 재대조. 문구 오류 4(G11 "팔 하나를 잃고"→"팔 하나만 남은 채", G05 배낭 삭제, N04 가판대, G01 시계 위치 왼쪽 위). M2↔구현 차이: 쉬는 시간 구역별 시계 멈춤(M2는 바로 흐름·안전한 곳 없음), UV 무제한(M2 3회). directive-guide-plan.md 갱신.
- 2026-10-31 별건(사용자 요청): 대기실 벽 재질 깜빡임·기둥 사라짐 원인 조사·수정. 원인은 겹친 면(z-fighting), Cinemachine far 5000, 오래된 오클루전 데이터였다. `WaitingRoomOverlapFix` 메뉴 추가, 씬 정리, 오클루전 재굽기까지 했다. 상세는 Task-079 `waiting-room-optimization.md`. 재검사로 큰 겹침이 사라진 것을 확인했고, 두 번째 실행은 0건이다. 플레이 확인은 아직이다.
- 2026-10-31 사용자 정정: 현재 구현이 최신(구현 > M2 > 2차 기획서). 계획에서 "M2↔구현 결정 필요" 항목 삭제, G02·UV 무제한·"물려줄 선반" 유지. 구현 대비 문구 차이는 G11·G05·G01 3건.
- 2026-10-31 사용자 문구 수정본 반영 확인: 게임이 읽은 24줄이 파일과 일치. EditMode 7/7, 패널 24장 캡처(임시 테스트, 사용 후 삭제). G02에 "단장의 추격"이 있으나 구현에는 추격자 없음 → 사용자에게 알림.
- 2026-10-31 문제: 1일차 첫 공연 뒤 튜토리얼이 하나도 안 뜸. 원인은 PC 저장값 안내 끄기(Enabled=0)였다. 테스트가 중간에 끊겨 원래 값을 못 되돌린 것으로 추정한다. 또 테스트 정리 직후 남은 프레임에 관리자가 본 안내를 PC에 기록하고 있었다(G01,N01).
  수정: ① 본 안내 기록을 런 단위 메모리로 바꿨다(계획 A). CampaignRun이나 Generation이 바뀌면 비우고, PlayerPrefs `...Tutorial.v1`은 시작할 때 지운다. ② 테스트용 `MemoryOnly`를 추가했다. 켜져 있으면 끄기 설정도 PC에 읽고 쓰지 않으며, 플레이를 시작할 때 풀린다. ③ PlayMode 테스트가 제목을 문구 파일에서 읽도록 바꾸고, 새 런을 시작하면 안내가 다시 뜨는지 확인하는 단계를 넣었다.
  검사: 컴파일 PASS, EditMode 7/7, PlayMode W5_Tutorial 2/2, SettingsUpgrade 5/5. 테스트 뒤에도 PC Enabled=1이 유지되고 예전 기록 키가 없는 것을 확인했다. 사람 플레이는 미확인.
- 2026-10-31 14:07 사용자 문구 3차 수정(G07·G08·G11·G11A·N01·N02) 반영: Unity 강제 가져오기 후 게임 로딩 경로로 24줄 파싱·변경 6줄 일치 확인, EditMode 7/7.
- 2026-10-31 사용자 버그·요청 4건(세션 "게임 튜토리얼 및 일정 확정 버그"). origin/dev 7커밋(카메라 작업) 병합. CommonSettingsUI 충돌 1줄은 양쪽 유지. 주의: 병합 때 Unity 에디터가 열려 있었다(02:15부터). 에디터에서 씬을 저장하지 말고 디스크본으로 다시 읽어야 한다.
  ① 침대: 스케줄표 확정 전에는 상호작용 불가, 다가가거나 F를 누르면 가이드 패널에 "하루 일정이 마무리되지 않았습니다…"(안내 끄기와 무관, 5초). 원인은 확정 전 누우면 전원 준비 상태로 밤 종료 게이트에서 멈춘 것.
  ② 밤 소품실 개방: 소품실/작업실 공용 복도 철창(PropsGate, x -8.2 z 91.6)을 작업실 갈림길 입구 `WorkshopGate`(x -10.6 z 93.4, yaw 90)로 옮김(씬 YAML 직접 수정 + 생성 코드). 플레이 확인 전.
  ③ 클로즈업 리스트 확정(사용자 추천안 전부): 연계 G01→G03→G11(무대 입구 통로), N01→N02(상점)→N04(물려줄 선반)→N03(스케줄표 클로즈업 자리). 단일 클로즈업 N05(일정 확정 직후 숙소)·G10(신체 복구 승강기). G11A는 클로즈업 없음. 클로즈업 없는 연계 안내는 4초 잠금. 카메라 1.8초 이동·2.5초 유지·1초 복귀(`WaitingRoomTutorialCamera`, 브레인 블렌드는 그동안 Cut).
  ④ 기물 안내(G05·G06·S03·S04 등)는 잠그지 않고, 연계 중이면 끝난 뒤(시점 복귀·조작 해제 후) 출력.
  동시 작업: 세션 "대기실 튜토리얼 가이드"가 패널 설정 파일·문구 파일 `| 초` 칸을 추가함. 그 최신본 위에 부분 수정했고 문구 파일은 건드리지 않음(14:06 사용자 수정본 대비 주석 2줄만 다름).
  검사: 컴파일 미실행(에디터가 CLI에 응답 없음, TIMEOUT editor_status). PlayMode W5_Tutorial 2개·M2_NightPowerGatesAndBedWake에 확인 단계 추가, 미실행.
- 2026-10-31 검사(에디터 재시작 후): 컴파일 PASS. PlayMode W5_Tutorial 2/2(연계 잠금·G11 클로즈업·복귀, 밤 N03 후 확정→N05 숙소 클로즈업), M2_NightPowerGatesAndBedWake 1/1(확정 전 침대 불가·안내), W3_BedRestCamera 1/1. EditMode WaitingRoomTutorialBookTests 10/10.
  처음 W5 1건 실패: 테스트 공용 Launch가 시작 직후 조작 비차단을 확인해 연계 잠금과 충돌. DevStartLauncherPlayTests 준비에서 튜토리얼을 메모리 전용·꺼짐으로 두고(튜토리얼 테스트만 켬), 연계 중에는 그 확인을 건너뛰게 함.
  회귀: CameraViews 9/9, DemoRunEnding 1/1, W3_TierButtons 1/1, W3_HumanCannonSet 1/1, W2_BodyRestore 2/2, SettingsUpgrade 5/5. 실패 8건(WaitingRoomHud 1, InteractionPresentation 2, SpaceLink 5)은 메시지상 예전 그레이박스 좌표·M2 시계 규칙 기대값 문제로 보이나 이번 변경 제외 비교는 안 함.
- 2026-10-31 사용자: 설명 패널도 직접 수정 → 설정 파일 방식 선택. `Resources/Settings/WaitingRoom/WaitingRoomTutorialPanel.txt`(위치X·위치Y·너비·표시시간·간격·제목크기·본문크기, 범위 보정)와 `WaitingRoomTutorialPanelSettings`(Logic) 추가. 문구 파일에 4번째 칸 `| 초`로 안내별 표시 시간. 다른 세션("게임 튜토리얼 및 일정 확정 버그")이 WaitingRoomTutorial.cs·Book.cs를 동시에 수정 중이라 겹침 범위를 서로 알림. 검사: 컴파일 PASS(다른 세션 변경 포함 최신본), EditMode 10/10, PlayMode W5_Tutorial 2/2, PC Enabled=1 유지. 사람 플레이 미확인.
- 2026-10-31 사용자: 대기실에서 나오는 모든 패널 문구를 직접 수정(아이템 설명·상호작용 안내 포함) → text-edit-plan.md 추천안(1~3단계, 상점에 아이템 설명, 파일 1개, 공통 화면·디버그 제외)으로 진행.
  구현: `WaitingRoomText`(엔진 무관 asmdef `CurtainCall.WaitingRoom.Texts`, `{이름}`·조사 자동 `{x:을}`), `WaitingRoomTextLoader`(BeforeSceneLoad), 문구 파일 `Resources/Settings/WaitingRoom/WaitingRoomTexts.txt` 236키(상호작용 62·알림 49·상점 40·스케줄/밤 58·탐색 결과/선반 16·아이템 11). 문구 비교 로직 정리: 선반 IsAvailable 상태 판정, 탐색 결과에 usedUp 표시(튜토리얼 S04), WrongCode·PurchaseTimeRefusal·NightBed 상수→프로퍼티. 아이템 이름은 플레이 중 ItemCatalog 복사본에 덮어씀(에셋 불변), 설명 `item.<id>.desc`는 상점 설명 칸에 표시(지금은 주석 처리된 기획 메모만). 조사 표기 일부 교정(머리핀로→머리핀으로 등).
  작업 분담: 보조 에이전트 4개가 파일을 나눠 이전(겹침 없음). 다른 세션("게임 튜토리얼 및 일정 확정 버그")에 NightBed·ScheduleBoardView·WaitingRoomTutorial 부분 수정 알림, 동의 받음.
  검사: 컴파일 PASS, EditMode WaitingRoom 103/103(코드 키 전부 파일에 있음·이름표 일치·안 쓰는 키 없음 포함), PerformanceReward 39/39, PlayMode W5_Tutorial 2/2·M2_NightPowerGatesAndBedWake 1/1·W3_BedRestCamera 1/1.
  남은 것: 씬 값(단서·문 이름, 표지판 128, CCTV), ShopPanel 프리팹 고정 글자(기물/서비스/장부/닫기/빈 목록), 서드파티 ShopPanelView 하단 문구는 이번 범위 밖. 장부 설명은 100자 넘으면 기록 거절.
- 2026-10-31 대기실 PlayMode 전체(DevStartLauncherPlayTests)는 20분 넘게 걸려 사용자 요청으로 중단 → 결과 없음(미확인). 중단 뒤 에디터 정지 상태, PC 안내 설정 Enabled=1 유지 확인.
- 2026-10-31 origin/dev 동기화 확인: fetch 결과 새 커밋 없음(origin/dev=7164c818). 14:04 병합 커밋 d25fa490에 이미 포함. 미해결 충돌·충돌 표시 없음. 그 병합에서 손으로 맞춘 파일은 WaitingRoomBase.unity 1개이며, dev가 씬에 추가한 87줄이 현재 씬에 모두 남아 있음 확인.
- 2026-10-31 사용자: 밤 정비 튜토리얼이 "일정 확정 → 잠자기 → 다음 날" 흐름을 못 전함 → 순서(N01→N02→N04→N03, 확정 뒤 N05)는 유지하고 N01·N03·N05 문구를 목표·조건(호스트 확정, 모두 누우면 다음 날) 중심으로 교체(추천안 승인). 사용자는 WaitingRoomTexts.txt에서 아이템 설명 주석 해제·침대 문구 수정.
- 2026-10-31 사용자 문구 수정(G03·G04·N01·N03·N05) 반영 확인(24줄 파싱). 다음 날 재출력 확인: PlayMode W5_Tutorial_NotRepeatedNextDay 추가·통과(1일차 밤 연계 → 확정 → 잠자기 → 2일차, 같은 세대, 연계 안내·조작 잠김 재발 없음, 본 기록 유지). 초기화 조건은 앱 재시작·설정 "안내 처음부터"·세대 교체(런 실패/호스트 포기)·새 게임(CampaignRun 교체). 사용자 새 기준 "게임을 완전히 종료하고 다시 시작했을 때만" 과 세대 교체 초기화가 다름 → 결정 요청.
- 2026-10-31 사용자 결정: 튜토리얼은 앱 재시작 때만 처음부터 → 세대 교체·CampaignRun 교체 시 ResetAll 제거(정적 초기화만). W5_Tutorial_GuideRestShowsInOrderAndRemembers 기대값을 "새 런에서도 다시 안 뜸"으로 변경. 검사: 컴파일 PASS, PlayMode W5_Tutorial 3/3(다음 날 미재출력 포함).
- 2026-10-31 사용자: G10(신체 복구·의료용 서비스) 안내 클로즈업을 승강기 대신 상점 수화기로 변경(TryFocus). 임시 캡처로 수화기·배송구 화면 확인(방향 일치 0.988, 거리 2.5m), 임시 테스트 삭제. G10 문구는 사용자 수정본 그대로.
- 2026-10-31 사용자 문구 수정(G05·G06b·G07·G10 등) 반영 확인: 튜토리얼 24줄·대기실 241키 파싱, WaitingRoomTextTests 4/4.
- 2026-10-31 버그: 4일차 아침에 런이 끝나지 않음. 원인은 마지막 아침이 쉬는 시간 타이머 만료로만 끝나는데(통로 집결은 무시), 구역 규칙(10-30)이 생활 구역에서 타이머를 멈춰 기한이 흐르지 않았던 것. 수정: RunState.SetRestTimerPaused가 IsFinalMorning이면 멈추지 않음. EditMode RunTimeLimitTests 16/16(새 FinalMorningDeadlineIgnoresZonePauseAndEndsRun 포함).
- 2026-10-31 사용자: 내구도 소진으로 기물이 파괴되면 오른쪽 패널로 알림 → WaitingRoomItemBrokenNotice(툴팁 정본 패널, 오른쪽, 4초, 매번). ExplorationState 결과에 usedUpItem 추가. 문구 notice.item-broken-title({item})·notice.item-broken. 캔버스 실제 너비 기준 배치(16:9 아닌 화면에서 잘림 수정). PlayMode W5_ItemBrokenNoticeShowsWhenToolUsedUp 1/1, 캡처로 위치 확인. 파괴 경로는 탐색 도구 사용 1곳뿐(공연 장비 소모는 아직 구현 없음).
- 사용자 질문(gate 문구): 열쇠 소지 여부가 아니라 문 종류로 갈림. 선행 조건(열쇠 등) 미충족 → gate.conditions-unmet, 충족 후 열쇠 구멍 2개 문 → needs-two-keys, 협동 문(구멍 없음) → needs-two.
- 2026-10-31 사용자 문구 수정(gate 3줄 등) 반영 확인(24줄·241키, WaitingRoomTextTests 4/4). 아이템 구역 주석("설명 아직 없음") 현행화. Confluence 작업노트(M2 작업 - 서민규 폴더)에 "[M2] 대기실 안내·상호작용 문구 파일 — 경로·기획·수정 가이드 (Task-086)" 페이지 31981570 생성: 파일 경로·역할, 튜토리얼 기획(패널 구성·연계·출력 조건·반복 규칙), 문구 파일 구성, 수정 가이드, gate 조건, 파일 밖 문구, 세 파일 원문.
- 2026-10-31 사용자 요청 묶음:
  ① 오늘 할 일(N01)을 오른쪽 패널(제목 [tutorial])로. 첫 밤 내내 표시, 스케줄표 보는 동안 숨김, 모두 잠들면(밤 종료) 사라짐. 공용 WaitingRoomRightPanel 추가, 기물 파괴 알림은 그 아래(Top 560)로.
  ② G03 상호작용 안내는 연계에서 빼고, 상호작용 가능한 대상을 처음 발견(ShopReach.Focused)할 때 표시. 연계 항목을 일반 안내보다 먼저 꺼내도록 Pump 수정(확정 직후 N05가 밀리던 문제). 다른 세션에 알림.
  ③ 디버그 탈출로 열쇠 1·2: DebugEscapeKeyProp + ExplorationState.RequestDebugOpen(에디터·개발 빌드 전용). DebugLobbyEscapeKeyInstaller로 로비 (-4.2,0,75.3)/(−4.2,0,76.2), 디버그 돈 옆에 배치·씬 저장.
  ④ 확정 용어(의료용 서비스·신체·공연 준비자·의료용 승강기·상점·보관용 선반) 메모리 저장, 두 문구 파일 표시 문구·설명에 적용. 작업노트 페이지 31981570 v2: 0장 용어, 오늘 할 일·상호작용 조건 반영, 원문 갱신.
  검사: 컴파일 PASS, EditMode WaitingRoom 103/103, PlayMode W5_ 5/5(W5_DebugEscapeKeysOpenEscapeRoute 신규). 스케줄표 숨김 테스트 첫 실패 원인은 테스트 캐릭터가 멀어 표가 자동으로 닫힌 것(진단 후 테스트 수정). 사람 플레이 미확인.

- 2026-10-31 동료 세션 튜토리얼 흐름 변경(연계 G01·G11 / N02·N04·N03, N01 오른쪽 패널, 연계 우선) 뒤 재검사: PlayMode M2_NightPowerGatesAndBedWake 1/1, W3_BedRestCamera 1/1 PASS.
- 2026-11-01 사용자: 상자·문·벽 등 씬에 저장된 탐색 대상 이름도 문구 파일에서 고치게 해 달라 → WaitingRoomTexts.txt 맨 아래 "탐색 대상 이름" 구역(target.<targetId>.name 44줄, 단서 .desc, 모니터 .text). ExplorationGate·ToolTarget·BreakableWall·ClueInspectable·UvMark의 Label/Title이 파일 값을 먼저 쓰고 없으면 씬 값. 키는 씬 targetId(모두 고유, dev 씬과 목록 동일). 벽 표지판·CCTV·공연 이름은 여전히 인스펙터. 새 테스트 TargetKeysMatchSceneTargets(씬에 없는 id·잘못된 끝말 검출). 검사: 컴파일 PASS, EditMode Exploration 31/31. origin/dev 병합은 Unity 에디터가 켜져 있어 미진행. 사람 플레이 미확인.
- 2026-11-01 사용자 문구 수정(대상 이름 등) 반영 확인, WaitingRoomTextTests 5/5. 사용자: 도구 없을 때 도구 이름 대신 힌트 → ExplorationTargets.Hint(target.<id>.hint → item.<도구 id>.hint → "알맞은 도구가 필요할 것 같다", 힌트 안 {tool} 사용 가능). tool.needs-tool·wall.needs-tool·explore.need-tool이 {hint}를 씀. 힌트 초안 3줄(머리핀·말뚝 뽑개·UV 등). 사용자: notice.item-broken 본문에 {item} 허용 → 기본 "{item:이} 파괴되었습니다.". 검사: 컴파일 PASS, EditMode Exploration 31/31. 사람 플레이 미확인.
- 2026-11-01 사용자 요청 수정 문구 캡처(임시 PlayMode TempCapture_EditedTexts, 캡처 후 삭제, .harness/local/text-capture). 확인: 트렁크 [F] 힌트(말줄임으로 잘림), 맞지 않는 도구 힌트 토스트, 파괴 알림 "천막 말뚝 뽑개가 파괴되었습니다"(본문 2줄 줄바꿈), 압수된 노트 안내·설명. 못 찍음: 녹슨 벽·캐비넷 2·찢겨진 노트 낱장(자동 자리 찾기 실패, 벽은 Focused여도 안내 가림 판정 통과 자리 없음 → 플레이 확인 필요).
- 2026-11-01 사용자: [F] 안내 박스에서 긴 힌트가 말줄임으로 잘림 → "박스에 다 보이게". NotificationTextLayout(상호작용 안내만)이 문구 한 줄 폭에 맞춰 박스를 넓히고(기본 384~최대 640), 그래도 넘치면 3줄까지 줄바꿈(기존 2줄). 알림·배너는 그대로 2줄. 외부 패키지(_ThirdParty InteractionPromptView)는 수정 안 함. 테스트 InteractionReappliesTwoLines… → InteractionWidensForLongTextWrapsToThreeLinesAndRestoresShortSize로 교체. 검사: 컴파일 PASS, EditMode NotificationTextLayoutTests 3/3, 임시 PlayMode 캡처로 트렁크 안내 2줄 전체 표시 확인(임시 테스트 삭제). 다른 화면(로비·공연)의 [F] 안내도 같은 규칙이 적용됨, 사람 확인 필요.
- 2026-11-01 사용자 지적: UV 등 힌트는 나올 상황이 없음 → 확인(UvMark는 [F] 대상이 아니고 UV 빛이 비출 때만 발견 요청, uv-lamp를 요구하는 관문 없음). 사용자 문구 "희미한 얼룩이 보인다."는 지우지 않고 주석(#)으로 보존.
- 2026-11-01 origin/dev 병합(빠른 진행, 0fafe124 #255까지) + _ThirdParty 하위 저장소 149108fa로 갱신. 병합을 막던 에디터 자동 저장분(CommonButtons·CheerGauge Pretendard 폰트 글자표, escape-labels 텍스처 1×1로 비워짐)은 지우지 않고 stash 2개로 보관. 검사: check-work 문서/규칙 FAIL(Task-079 log.md 138줄 hard 한도, 이번 변경과 무관) → 컴파일·테스트 SKIPPED. verify-unity 단독 실행은 ENV_BLOCKED(에디터 닫힘). Unity 다시 열고 컴파일 확인 필요.
- 2026-11-01 dev 병합 후 검증: 컴파일 오류 0(up_to_date), EditMode WaitingRoom.Exploration 31/31 PASS, NotificationTextLayoutTests 3/3 PASS. 벽 [F] 표시·파괴 알림 줄바꿈은 사람 플레이 확인 남음.
- 2026-11-01 origin/dev 2차 병합(빠른 진행, b723ca79 #257까지: #256 아이템 아트·상호작용, #257 혈흔 연출). 작업 변경은 stash로 보관 후 다시 적용. ExplorationGate.ActionLabel 충돌: dev의 "맞는 열쇠를 선택하세요" 줄 유지 + 이름은 Label(텍스트 파일)로 통일. 보관 stash는 남겨 둠. 컴파일·테스트는 Unity 재실행 후 확인 필요.
- 2026-11-01 2차 병합 후 검증: 컴파일 오류 0(up_to_date), EditMode WaitingRoom.Exploration 31/31 PASS, NotificationTextLayoutTests 3/3 PASS. 열쇠 안내(Label 통일) 화면 확인은 플레이로 남음.
- 2026-11-01 숨은 단서·녹슨 벽·배관함 피드백 반영(미커밋).
  - UV 단서: UvMark 겉판(얼룩·철판) 렌더러 숨김, 오버뷰로 보는 단서는 빛이 꺼져도 닫을 때까지 유지(UvMark.SetMarks). ClueInspectable 오버뷰·크기는 꺼진 렌더러 제외.
  - 낙서: 받침판 없이 벽에 직접 그림(StructureAPropShapes.Document). 낙서1 세모·2 네모·3 동그라미·4 X, UV1 화살표(녹슨 벽 쪽)+X, 앞자리 "12", 숨은 낙서3 캐비넷+"3". 화장실 UvArrow/UvCross 판은 marks에서 뺌.
  - 화장실 녹슨 벽(StructureAHiddenDetailPass): 옆 벽 UV 규칙으로 타일 무늬 이음+같은 평면, 튀어나온 문틀 철판(DoorReveal) 숨김, 외곽선 끔(IShopOutlineOptional), 조각 들뜸 끔(loosenPieces), 바닥 문턱 내림, 벽 전체 진한 녹, 서 있는 동안 실금+타격마다 거미줄 금 5단계(LA_RestroomBreachCracks.asset).
  - 배관함: 철창 제거, 스테인리스 문(테두리·검은 세로 손잡이·경첩)이 키패드를 가림, 문 조각은 무너질 때 같이 쏟아짐.
  - 탐색 배치물 다듬기: 상자 뚜껑 이음새, 캐비넷 문 테두리, 강철 상자 모서리 철판.
  - 버그 "손이 가득 찼습니다" 미출력: 원인 = 배송구 수령(WaitingRoomShop)·열쇠 줍기(ExplorationState)의 기물 거절이 토스트로 안 이어짐. CommonWaitingRoomUI가 두 채널의 기물 거절 문구(PropCarry.IsRefusalText)도 토스트로 띄움, 튜토리얼 S03도 열쇠 경로 감지. 실제 플레이 확인은 안 함.
  - 버그 선반 근처 애니메이션 반복: 재현 실패. 말뚝 뽑개를 든 채 선반 3.2m→0.7m 접근·9초 정지(튜토리얼 켬/끔, N04 클로즈업 포함) 동안 Idle·ItemHold 재시작 없음. 실제 걷기 입력·부상 단계·기물 종류 등 재현 조건 필요.
  - 검사: 컴파일 PASS, EditMode WaitingRoom 31/31 PASS, PlayMode M2_Confirmed 3/3·M2_CrowbarBreaks 1/1·M2_Placement_LobbyUv 1/1 PASS. M2_Placement 3개 FAIL(탈출문 ConditionsMet·열쇠2 획득·서랍 내구도 — 이번 변경과 무관해 보임, 그동안 ExplorationReach.From 인자 수 불일치로 실행 자체가 안 되던 테스트. 도우미 인자만 맞춤. 변경 전 상태로 돌려 비교하지는 않음).

## 2026-11-01 폐기물 보관소 숨은 단서 이름

- 문제: 폐기물 보관소 UV 단서(passwordPrefixClue)가 벽 그림 "12"인데 이름이 "찢겨진 노트 낱장"으로 나왔다.
- 원인: 텍스트 외부화 때 기획서 폐기물 보관소 목록의 "탈출 힌트가 적힌 찢겨진 노트 낱장"을 이 단서에 붙였다. 숨은 단서를 낙서 그림으로 바꾼 뒤 문구가 남았다.
- 수정: WaitingRoomTexts.txt 이름 "숨겨진 단서 2", 설명 `벽에 "12"라는 숫자가 그려져 있다.` (숨겨진 단서 1·3과 이름 규칙 맞춤).
- 확인: 에디터 재임포트 후 문구 반영 확인. 플레이 확인 안 함.

## 2026-11-01 단서 새김 글자 연출 (압수된 노트 "34")

- 사용자 결정: 압수된 노트 설명에서 34를 지우고 뒷면에 "34"를 새긴다. 오버뷰에서 그 면을 보면 화면이 강조되고 `노트 구석에 "34"라는 숫자가 새겨져 있다.` 출력. 찾은 플레이어 화면에만(공유 안 함). 다른 단서의 새김 내용은 나중에 정한다. 공연 기록은 텍스트 노트 오버뷰, 지도는 탑다운 지도 오브젝트 제작 뒤 교체 예정.
- 구현:
  - `ClueInspectable`: 선택 필드 `secretMark`(위쪽 축 = 새긴 면 바깥), `secretText`. 문구 키 `target.<id>.secret`이 우선. 오버뷰에서 새김 면이 카메라를 정면(내적 > .8)으로, 화면 가운데 60% 안에서 0.4초 유지되면 찾음. 금빛 화면 테두리 번쩍임(0.8초) + 새김 글자 발광(번쩍인 뒤 은은하게 유지) + 설명란에 금색 문구 추가. 찾은 상태는 이 화면에서 정적 집합으로 기억(다시 열어도 유지, 게임 재시작 시 초기화).
  - `StructureAPropShapes.Secret`: 압수된 노트 아래면 구석에 "34" 획을 만들고 secretMark·secretText를 연결, 씬 description을 비움. 생성기 재실행·씬 저장.
  - WaitingRoomTexts.txt: `confiscated-notebook.desc` 제거, `.secret` 추가. 문구 테스트가 `secret` 끝말을 허용.
- 검사: 컴파일 PASS. EditMode WaitingRoom 108/108. PlayMode M2_Confirmed 4/4(새 테스트 `M2_Confirmed_NotebookBackEngravingRevealsHintWhenFaced` 포함). 첫 묶음 실행에서 UV 테스트가 우클릭 입력을 못 받아 1회 실패, 단독·재실행 모두 통과(입력 타이밍 흔들림으로 판단).
- 캡처: `.harness/local/visual/10-notebook-secret.png`.
- 사람 확인 필요: 실제 마우스로 돌려 찾기 난이도(판정 각도·시간), 번쩍임 세기.

- 2026-11-01 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-01 탈출로 열쇠 → 은색 열쇠 이름 통일

- 문제: 바닥 대상 이름은 "은색 열쇠 1·2"인데 주운 기물(슬롯) 이름·설명은 "탈출로 열쇠"로 남음. 원인: 열쇠 아이템 이름이 `ExplorationKeys.Names`·`ItemCatalog.asset`·씬 label에 따로 고정돼 있었음.
- 수정: ItemCatalog.cs 고정 이름, ItemCatalog.asset displayName, 씬 escapeKey1·2 label, StructureAPlacement 생성 이름, 디버그 열쇠 안내 문구를 "은색 열쇠"로. WaitingRoomTexts.txt에 `item.structure-a:escape-key-1/2.name` 추가(문구 파일에서 고칠 수 있게).
- 검사: 컴파일 PASS, EditMode WaitingRoom 108/108. 플레이 화면 확인 안 함.
