# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-05 원인 측정
- origin/dev(a016f93e)를 fix/loading에 병합(충돌 없음, 사용자가 Unity 종료 확인). doctor FAIL: task-ids 중복(기존 Task 다수), unity-editor 미설치 판정(실제 6000.3.25f1 실행됨) — 이번 측정에 영향 없음.
- 새 에디터 세션, Start 씬 → DevStartLauncher 1일차 공연1 Tightrope(LAN 호스트). `[Session] 메인 스레드 정지 13328ms scene=Tightrope` 재현.
- LongFrameRecorder 결과(Logs/Performance/long-frame-20261105-162353-f26.json, 로컬·미추적): 13279ms 중 12504ms가 `PerformanceAudience.LateUpdate > Loading.ReadObject > Loading.LockPersistentManager`.
- 원인: `AudienceReactionSettings.Current`가 매번 `Resources.Load`(동기). 공연이 열리는 첫 프레임에 처음 호출되는데, 같은 때 `PerformanceBackgroundLoader`가 공연 배경 씬(_ThirdParty 1.4GB 폴더)을 LoadSceneAsync 중이다. 비동기 로드 중 동기 로드는 그 로드가 끝날 때까지 메인 스레드를 막는다. 기존 Editor.log의 줄타기 12.9s/11.1s 튕김도 같은 순서(Tightrope 로드 → 정지 → 배경 씬 Loaded).
- 같은 패턴 후보: `PerformanceEndingSettings.Current`도 매번 Resources.Load.
- 다음: 해결안 사용자 확인.

## 2026-11-05 1번 수정과 대기실·탈출로 측정
- 수정: `AudienceReactionSettings`·`PerformanceEndingSettings.Current`를 정적 캐시 + `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 사전 로드. 값·규칙 변경 없음.
- 재측정(같은 에디터 세션 2회차, 따뜻한 캐시): 줄타기 진입 최장 13.3s → 3.2s. PerformanceAudience 대기 0. 남은 3.2s 중 2.76s는 EditorLoop(에디터 전용), Telemetry.Outbox.AtomicWrite 0.35s.
- 줄타기→대기실(DevStartLauncher Between): 10.1s(PlayerLoop 0, EditorLoop만, 이때는 에디터 프로파일 꺼짐) + 4.1s. 4.1s 중 `CameraViewZone.OnTriggerEnter` 2584회 1.9s(OverlapBoxNonAlloc 0.59s 포함), 씬 통합 1.0s, Telemetry 쓰기 0.5s.
- 대기실→탈출로(CampaignRun.StartEscape, 에디터 프로파일 켬): 7.9s 중 7.79s가 `SceneHierarchyWindow.Paint > EditorResources.Load > AssetDatabase.GetGUIDFromAssetPathV2 > Loading.LockPersistentManager`. 에디터 Hierarchy 창이 아이콘을 동기 로드하다 escape_route 비동기 로드가 끝날 때까지 대기. 빌드에는 없는 에디터 전용 정지. 이어서 2.0s(씬 통합 0.95s, Telemetry 0.8s).
- 탈출로→대기실(에디터 프로파일 켬): 3.5s, CameraViewZone 2.0s·Telemetry 0.5s. 10s급 EditorLoop 정지는 재현 안 됨(아이콘 캐시 추정). 첫 대기실 10.1s는 같은 Hierarchy 원인으로 추정, 미확정.
- 후속 후보: CameraViewZone 트리거에서 로컬 플레이어 외 콜라이더 무시, Telemetry Outbox 원자 쓰기 묶기, 나머지 매 호출 Resources.Load(63곳) 점검, 빌드·저사양 PC 측정.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-05 대기실 첫 안내 누락·조작 불가 제보 검토(코드만, 재현 전)
- 1일차 안내 쉬는 시간은 전원이 G01·G11을 봐야 진행(`WaitingRoomFirstDayFlow`: 본 사람은 teamLock으로 입력 막고 "동료 기다리는 중").
- G01·G11은 whileBlocked=false라 `InputGate.IsBlocked`인 동안 시작하지 않는다(`WaitingRoomTutorial.Pump`). 따라서 "안내 안 뜸 + 조작 불가 + 상호작용 표시 없음"은 안내가 원인이 아니라 그 PC에 남은 다른 입력 막기(커튼·공연 종료 연출·메뉴 등)의 공통 증상일 가능성이 높다. 누가 막는지는 미확인.
- 카메라 구역(CameraViewZone) 비용 수정과는 별개. 느린 전환 타이밍이 막기 해제를 놓치게 하는 계기일 수는 있음(가설).

## 2026-11-05 입력 막힘 진단 장치
- `InputGate.Describe()`(막는 요청자·파괴됨 표시·키 놓기 대기) 추가, `WaitingRoomFirstDayFlow` teamLock을 이름 있는 `TeamWaitLock` 타입으로 변경(동작 동일).
- `WaitingRoomInputDiagnostics`: 대기실에서 내 캐릭터 입력이 20초 넘게 막히면 `[InputStuck]` 경고 1회(요청자·안내 상태·캠페인 준비 수·씬), 풀리면 걸린 시간 기록. 빌드는 Player.log.
- 혼자 호스트 실제 흐름(1일차 공연1 → FinishPerformance 성공 → 결과 → 대기실): G01·G11 정상 표시, 연계 안내가 14초 입력을 막은 뒤 풀림, 준비 1/1로 진행. 여기서는 버그 재현 안 됨. 처음 임계값 10초는 정상 안내도 잡아 20초로 올림.
- 재현에는 2인 이상(느린 클라이언트)이 필요. MPPM 가상 플레이어는 공식 Scenarios 창으로 켜야 함(mppm-virtual-player-status 함정).

## 2026-11-05 MPPM 2인 재현 시도
- 사용자가 MPPM Player2 켬. 복제본 제어는 `unity command eval --pid <복제본 PID>`(포트는 도메인 리로드마다 바뀌어 --port 오지정 1회, 메인 Join 호출은 Host 상태라 무영향).
- 2인 기준(DevStart Between → 대기실 안내): 둘 다 G01·G11 표시 → teamLock → 2/2 후 해제. 정상.
- 복제본 대기실 sceneLoaded에 8초 정지 주입 + 실제 흐름(공연1 FinishPerformance → 결과 → 대기실, 안내 기록은 줄타기에서 초기화): 둘 다 안내 정상, 2/2 해제. 커튼이 전원 로드를 기다려(느린 쪽 [InputStuck] 34초는 커튼) 안내가 어긋나지 않음. **제보 버그는 재현 안 됨.**
- 무효 표본: 대기실 안에서 안내 기록을 지우면 그 자리에서 바로 소비돼 "커튼 중 본 안내"처럼 보임(테스트 준비 착시).
- 별도 재현 버그(2/2회): DevStart로 공연 시작 시 클라이언트에서 Tightrope 씬 로드가 캠페인 상태(공연 1)보다 먼저 와 `CampaignPerformanceEntry.Prepare`(sceneLoaded 1회 검사, IsRunning·GameState 조건)가 건너뜀 → 클라이언트 출발점 배치 없음, 낙하(y -60만), 호스트 공연 시작 조건(전원 출발점 1.5m) 불충족으로 공연이 열리지 않음. 수동 Prepare 호출로 우회해 테스트 진행. 정상 흐름(대기실 집결 → 공연)에서도 생기는지는 미확인.

## 2026-11-05 공연 진입 배치 누락 수정 시도 → 되돌림
- 가설(씬이 캠페인 상태보다 먼저 와 Prepare 건너뜀)대로 클라이언트 재시도(Prepare bool 반환 + SpaceLinkDirector Update 재시도)를 넣었으나 그대로 실패.
- 실제 원인: 복제본(클라이언트)에 `SpaceLinkDirector`가 없었다. 로비 씬의 `SpaceLinkHub.Awake`가 만드는데, 테스트에서 복제본이 Start 씬에서 바로 LAN 접속해 로비를 건너뜀. 다른 다인 리뷰 도구(EntranceReviewLocal, RehearsalMultiplayerReview)는 클라이언트에서 EnsureExists를 직접 호출한다.
- 복제본에 EnsureExists 후 재시도: Tightrope sceneLoaded 시점에 이미 phase=Performance·Playing(경쟁 없음), 진입 1개·배치 완료, 공연 정상 시작. 앞선 "로그 순서"는 상태 도착 순서가 아니었다.
- 결론: 정상(로비 경유) 흐름 버그 아님. 재시도 코드는 근거가 없어 git checkout으로 되돌림. 개발용 다인 테스트는 클라이언트도 로비 경유 또는 SpaceLinkDirector.EnsureExists 필요.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-05 측정 도구 폭주 사고와 CameraViewZone 수정
- 사고: LongFrameRecorder가 켜진 채 PlayMode 테스트가 돌면서, 멈춤마다 프로파일 버퍼 전체(.data, 1개 최대 3GB)를 저장했고 저장 자체가 2초 넘는 멈춤을 만들어 반복 기록. Logs/Performance에 약 255GB+ 생성(디스크 89%까지). 끄기(eval)가 한 Tick 안의 버퍼 순회를 멈추지 못했고, 테스트 중단 명령도 메인 스레드가 묶여 시간 초과됐다. 사용자 승인 후 .data 전부 삭제, 메인 Unity(PID 3836) 강제 종료 후 재실행. 디스크 61%로 복구.
- 도구 수정: .data 저장 제거, 세션당 20개 기록 후 자동 끔, Tick마다 Enabled 확인, 한 번에 1개만 기록, 시작 전 버퍼 프레임 무시, Stop 가드 제거.
- 사용자 제보 콘솔 오류 "No command named 'editor status'": 내가 상태 확인용으로 반복 호출한 `unity status`(CLI)가 Pipeline에 옛 이름으로 질의해 생김. 게임 무관, 호출 중단.
- CameraViewZone: 첫 시도(내 캐릭터 레이어만 검사)는 효과 없음 — 대기실 콜라이더 2243개와 플레이어가 모두 Default 레이어. 되돌린 뒤 트리거 이벤트에서 `other.transform.IsChildOf(local.transform)`일 때만 RefreshRequest(판정 기준은 기존 ContainsLocal과 동일, FixedUpdate 매 tick 재검사 유지).
- 재측정(DevStart Between, 같은 세션): 대기실 진입 최장 프레임 3.1~4.1s → 1.6s, 2초 이상 멈춤 0회. 해당 프레임 CameraViewZone 트리거 비용 0ms.
- CameraViewsPlayTests 17개 중 11 통과, 6 실패. 실패 6개는 모두 테스트가 `NetworkPlayer.ServerSetState`를 인자 1개로 리플렉션 호출하는데 현재 시그니처가 (PlayerState, DeathOrigin = Fall) 2개라 메서드를 못 찾는 기존 불일치(이번 변경 무관). 대기실 카메라 구역 테스트(StagePassageRetainsView, ReentryBlends 등)는 통과. 실패 테스트는 수정 전 코드로 다시 돌려 보지는 않았다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-05 telemetry-spatial-timeline 최신 반영과 텔레메트리 쓰기 확인
- Unity 종료 확인(EditorApplication.Exit) 후 작업 변경을 stash → origin/feat/telemetry-spatial-timeline(65d81b06) 병합(69b2b57a, 충돌 없음) → stash pop. 충돌 1개 `WaitingRoomFirstDayFlow.cs`: 상대의 `CampaignRun tutorialRun;` 추가와 내 `TeamWaitLock` 타입을 둘 다 살림. stash에 있던 Unity 자동 변경 글꼴 캐시 2개(Pretendard Bold/SemiBold SDF)는 복원 목록에 없어 사라짐(커밋 제외 권고 대상이었음).
- 남은 일 1(텔레메트리 쓰기 묶기): 상대 브랜치가 이미 `DurableOutbox.QueueEvent` 백그라운드 작업자(최대 64개씩 묶어 원자 쓰기)로 바꿨다. 별도 수정 불필요.
- 재측정(새 에디터 세션, 혼자 LAN 호스트, DevStart): 각 전환 최장 프레임의 메인 스레드 Telemetry 자체 시간 0ms(이전 0.3~0.8s). 줄타기 진입 12.4s·대기실 2.4s·탈출로 4.8s는 모두 EditorLoop(에디터 프로파일 끔)로, 새 세션 첫 로드의 에디터 쪽 정지(Hierarchy 아이콘 동기 로드 함정과 같은 양상 추정, 미확정). 게임 코드 상위 항목은 모두 10ms 미만.
- LongFrameRecorder: 외부에서 ClearAllFrames 하면 프레임 번호가 줄어 기록이 멈추던 문제 수정(nextFrame > last+1이면 재설정).

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
