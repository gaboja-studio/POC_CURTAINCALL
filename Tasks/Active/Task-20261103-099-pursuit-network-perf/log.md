# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 2026-11-03 기준 측정(baseline-20261103.json, 에디터·오프라인, 씬을 이미 여러 번 연 따뜻한 상태):
  - 로드 3.5초, 최장 프레임 1,041ms(GC.Collect 286ms, LoadLevelAsync Integrate 791ms 포함).
  - 씬: GameObject 30,952 / MeshRenderer 23,299(정적 배칭 180) / LODGroup 12,579 / Collider 3,764 / 실시간 조명 268(그림자 11).
  - 렌더: Lobby batches 6,110·그림자 캐스터 4,608·삼각형 230만(기준 초과), 숙소 2,527, 연습실 151.
  - 스냅샷: 추격자 26체 JSON 17,288B(≈169KB/s/클라이언트) → 바이너리 419B(≈4KB/s), 41배 감소.
- 2026-11-03 중요: PlayMode 테스트 중 추격 대기실 로드에서 `[Session] 메인 스레드 정지 12503ms`(차가운 로드) 경고. Relay 10초 기준 초과 → 튕김 원인 재현. 측정 도구의 1.0초와 차이 = 첫 로드(셰이더·에셋 캐시) 영향으로 추정.
- 2026-11-03 PlayMode 실패 3건 처리:
  - PursuitCheats_ReceivedCancellation: 스냅샷 타입 public 변경으로 리플렉션(NonPublic) 실패 → 테스트에 Public 포함(이번 변경 영향).
  - PursuitBehaviour_CaptureFourDirections: 단독 재실행 통과(순서 의존 불안정).
  - PursuitBehaviour_GraphHearing: dev 설정 에셋 1단계 순찰 이탈 30m로 100m 이동 시 Search 대신 복귀(dev에서도 실패하는 기존 문제) → 테스트 픽스처에서 이탈 제한 0.
- 2026-11-03 테스트: Pursuit EditMode 42/42 PASS. PlayMode 전체 재실행 12/14 — 실패 2건은 순서·환경 의존(CaptureFourDirections: 카메라 테스트 직후 런처 실패, 단독 PASS / GraphHearing: Vivox `Target Object Does Not Exist` 오류 로그, 단독 PASS). 이번 변경 영향 아님으로 판단.
- 다음: 다인 실기, 차가운 로드 멈춤 분석(후속 Task).

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-03 현재 브랜치 통합
- feat/waiting-room/pursuit-system에서 90a3f05c 병합. 새 피드백/치트/배치 단계와 전등·경량 스냅샷을 함께 유지.
- v3 바이너리에 poseEpoch/보호 치트/포획 누적 추가. 상태 신뢰성(4Hz+변경), 위치 비신뢰성(20Hz) 경로 분리 유지. 두 경로 준비 알림 적용.
- 코덱 왕복은 추가 상태값까지 검증. 컴파일 PASS, Pursuit EditMode46/46 PASS. 초기 check-work는 에디터 재시작 중 ENV_BLOCKED였으며 준비 완료 후 컴파일 통과.
- PlayMode 전체16개 중14개 PASS. 실패2개: 카메라 테스트 직후 런처 시작 실패(Activation), Vivox Target Object Does Not Exist(CaptureFourDirections). 두 항목을 개별 재검증한다. 다중 이름 세미콜론 필터는 NO_PROJECT_TESTS여서 결과로 세지 않음.
- 실패2개 각각 단독1/1 PASS. 전체14/16 + 개별2/2로 기록(전체 통과 아님). 이어서 사용자 요청대로 통합 커밋/푸시 후 P1을 진행한다.
- 통합 e72ac5e0 푸시 완료. save-work의 add -A가 Play 검증에서 생성된 동적 글꼴 캐시3개를 포함해 별도 정정 커밋으로 원래 git blob을 index에 복원했다. 생성 파일은 worktree에 그대로 보존하며 이후 명시 경로만 stage한다. 기존 데이터/이력 폐기·force 없음.

## P1 구현·측정 (2026-11-03)
- baseline-local-before.json: 같은 PC·에디터 반복 로드1790ms, 최장1273ms(GC451ms, Integrate1117ms 포함),26체 상태529B. 이전 Probe의5.17KB/s는10Hz 상태만 계산한 값으로 통합 후 실제 위치20Hz 경로를 누락했다.
- Awake는 생성 요청만 수집하고, Update에서 배치 순서를 유지해1체/프레임 생성+그래프 초기화. 마지막 Start가 끝난 다음 프레임 IsInitialized 완료. 완료 전 Bind/Ready/판정/스냅샷 적용 차단, 로컬 커튼 Reveal도 완료를 기다림.
- 생성 작업 합계/최장 단일 생성/분산 프레임/준비시간을 Probe에 추가. 첫 씬 로드 전용 메뉴는 같은 에디터 세션에서 이미 추격 씬을 연 경우 거부. OS/GPU 캐시 전체 초기화라는 뜻은 아님.
- 상태4Hz+위치20Hz payload 합산(상태 변경 추가 전송·NGO/UTP 헤더 제외). 조명은 동일한 결과값 쓰기를 생략하며 외부 WaitingRoomFlickerLight 변경은 실제 Light 값을 검사해 기존대로 덮는다.
- 새 초기화 단독 Play1/1 PASS, 추가 수신 방어/조기 snapshot 검사 포함 최종 코드 컴파일 PASS·Pursuit Edit46/46 PASS. 전체 Play/전후 측정 진행.
- 중간 실측(1체/프레임, after-warm): 로드1621ms, 준비3069ms, 최장1076ms, 초기화1952ms/실제작업21.8ms/단일최대1.47ms. 단일 전후 측정이며 GC 변동이 있어 인과/개선율 확정 불가. 대기 비용을 줄이기 위해 최종 구현은2ms 예산+최대4체/프레임으로 조정(단일 Instantiate/Init 자체는 선점 불가). 전체Play14/17의 메뉴 실패도 있어 새 fixture의 live singleton/handler 간섭을 방지하고 dev 통합 뒤 재검증한다.
- 최종2ms/최대4체 방식 컴파일 PASS·초기화 단독Play1/1 PASS. 중간 제출 전 check-work: 안전성/문서·규칙/컴파일 PASS, 테스트는 별도 기록. Task중복 매핑을 해소했고 로컬 동적 글꼴3개는 커밋 대상에서 제외한다.
- PR 직전 Unity 정상 종료·origin fetch. 최신 dev bf0b88a5 수신 및 병합(사람 대포/탈출로/설정 최신 변경 포함), 충돌0. 로컬 글꼴 캐시는 에디터 종료 시 원래 상태로 돌아옴. 통합 후 컴파일/추격 회귀 및 최종 예산 방식 측정 후 제출한다.
- dev bf0b88a5 통합 후 컴파일 PASS·Edit46/46 PASS. 최종 전체Play17개 중15 PASS/2 FAIL(초기화 신규·메뉴/재로드 포함15개 통과). 실패는 통합 전과 동일한 Activation 런처 순서 의존·CaptureFourDirections Vivox1001 로그. 전체결과 final-dev-play-tests.json 보존, 개별 재확인 및 최종 예산 방식 측정 진행.
- 최종 전체Play15/17 + 실패2개 각각 단독1/1 PASS(전체PASS로 표기하지 않음). final-dev-*-tests.json 보존. 최종2ms/최대4체 측정: 26체7프레임, 생성작업13.65ms/단일최대1.03ms, 로드1806ms/준비2781ms/최장1250ms. 기준1273ms와 유사해 전체 로드 개선으로 확정하지 않음. 남은 주요 비용 GC441ms/Integrate1112ms, 로비6763 batches/그림자4699.
- 최종 check-work 첫 시도는 최신dev에서 들어온 Task-081 log227줄 HARD로 중단(컴파일은 선행 규칙 때문에 SKIPPED). compact-docs로 완료된 과거 접두부 원문을 History/002-log-before-dev-integration.md에 내용 일치 확인 후 이동, 현재 로그66줄. 게임 코드/결정은 수정하지 않았다.
- 최종 검사 재실행: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 자동 테스트는 별도 결과(최신dev Edit46/46, 전체Play15/17·개별2/2). 이번 묶음을 중간 PR로 제출하며 남은 로드/렌더/Relay 실기를 완료로 표시하지 않는다.
- 중간 PR 제출: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/276 (feat/waiting-room/pursuit-system → dev, 최신dev bf0b88a5 포함). 미병합. 다음 최적화는 별도 브랜치에서 진행한다.
- PR #276은 GitHub 조회에서 MoHoDu 계정으로2026-11-03 18:31:20 KST dev852e9f18에 병합됨 확인. 이 작업에서는 PR 병합 명령을 실행하지 않았다. PR 링크 기록 daf55cbc는 원래 작업 브랜치에 push. 후속 fix/pursuit/render-perf 생성 후 Unity 종료 확인·fetch·dev852e9f18을 d066feb5로 병합(충돌0/런타임 코드 차이0). 이후 변경은 기존 PR과 분리.
- 후속 읽기 전용 렌더 조사(render-audit-local.json/eval.cs): 실제Play 프레임845/26체 초기화 완료 후 집계. 활성 MeshRenderer14729 중 일반 static13452, BatchingStatic180/실제staticBatched180. 로비(bounds center 기준)1793 중static1637/BatchingStatic44/실제배칭44. V4_Black504·V4_Iron_Dark366개, Cube799/Cylinder614/Sphere226개 반복, 주요 재질GPU instancing 꺼짐. 활성그림자조명11개(이번 조사에서는 directional1/spot10, point0). 초기화 이전 기준점/실제 visible draw call과 단순 개수는 구분한다.
- 이 단계는 씬/재질/조명 값을 변경하지 않은 조사이며 후속 브랜치의 문서/조사 결과는 아직 커밋·푸시하지 않았다. 다음 후보: 동적/문/기물·애니메이션/네트워크 물체 제외 규칙 → 방별 build-time 자동 정적 배칭 비교. 전체 플래그 일괄 변경이나 런타임 대량 Combine은 로드 멈춤/메모리 위험을 함께 측정한 뒤 판단.

## 2026-11-03 피해자 접촉 보고
- 사용자 승인 설계대로 원격 피해자 자신의 표시 위치 접촉 → 호스트 검증/최종 확정으로 변경. 기존 render 감사 변경 보존, Unity dirty0/Play 중지 확인·정상 종료·fetch 후 dev852e9f18 이미 포함 확인. fix/pursuit/capture-contact에서 진행. 동일Task doctor는 기존 실행 결과를 재사용.
- 57B 신뢰성 contact(sender=피해자), 캠페인/epoch/sequence/pose시각·좌표 검증,64개 고정20Hz 추격 경로 이력. Host 거리만으로 remote BeginCapture하는 경로 차단. client 확정 전 입력·부상·연출 변경 없음. 기존 CaptureRadius/벽/시야 조건 유지, age1.25초·fresh pose350ms 초과 및 오래된/역순/중복 요청 거부. v4 혼용 불가.
- CommitCapture는 피해자가 보고한 위치+모델offset의 anchor/방을 사용하며 기존 victims/snapshot·절단/Retired/관전/조명 흐름 재사용. 같은 actor/피해자 중복 포획을 막고 ResetStep/Unbind에서 이력 정리.
- 최초 core Edit13/13·Play2/2 PASS, remote owner777 실제스폰의 host-only 포획 차단 추가 후 core Play3/3 PASS. 확장 Edit60/60 PASS. 전체 Play20개 중17 PASS/3 FAIL: 이전 런처/Vivox 환경 문제와 배치 창 드래그(follow 전환) — 각각 따로 재확인 예정. 신규 접촉3개는 모두 통과.
- 전체 검사 후 시야 내 추격 이력과 authoritative Timer/캠페인 교차 확인을 추가해 초기화 경계의 이전active 캐시도 차단. 최종 Edit60/60·핵심 Play3/3 PASS(capture-contact-edit-tests.json/core-play-tests.json). 실제 여러PC Relay 지연 실기는 수행하지 않았으며 자동 시간축/유실 시뮬레이션·NGO 소유자 fixture 검사와 구분한다.
- 전체Play 실패3개 단독 재실행: Activation1/1·CaptureFourDirections1/1 PASS, Designer_LiveMap0/1 FAIL(중간버튼 SendEvent 뒤 liveFollowPlayer가 true). 해당 에디터/검사 코드는 이번 변경에서 수정하지 않았고 UI 이벤트 실패 원인은 미확정. 전체20/20 PASS로 표기하지 않는다. regression별 JSON 보존.
- 지연 검증 범위: 보고 시각 age50/150/300/600/1000ms 허용, 피해자 화면에서 벗어난 접촉은 거부. 실제 pose buffer480프레임·20% 유실·최대200ms 도착 지터·350ms 단절에서 이전 호스트 방식은400프레임 이상 잡힘 조건이나 새 owner 접촉0건. frozen/역순 pose·중복·벽·모르는 소유자·옛 epoch·타이머 초기화도 거부.
- 4인 후속 우선순위는 이동 최신좌표 우선 전송 A/B → 반복 소품 GPU Resident Drawer A/B → 도착간격 기반 보간/원거리 그림자/분산 탐지. 현재30Hz 유지, 프레임 급정지와 네트워크 지연을 따로 측정. rendering/전송 설정은 이번 묶음에서 변경하지 않았다.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 위 실패는 새 regression 결과 JSON3개의 대문자 이름 때문. 원문 결과를 유지하고 소문자로 이름을 고친 뒤 재검사한다.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 접촉 수정 최종 check-work 통과. 자동 테스트는 별도 저장한 Edit60/60·core Play3/3·기존 capture/activation 단독2/2이며 전체Play17/20 및 지도UI 단독 실패도 보존. 검증용 Unity 정상 종료 후 생성 폰트/라벨 캐시 변경은 자동으로 없어졌고 씬/프리팹 변경 없음. 이번 포획 수정/조사 문서는 아직 커밋·푸시하지 않았다.
- 포획 수정 PR #277 제출 후 dev9949880a에 병합됨 확인. 후속 fix/pursuit/four-player-perf에 최신 dev fast-forward 반영. 사용자가 외줄 균형·대포 착지·그네·장애물 판정 일치 추가 요청. 착지/그네에는 각각 별도의 호스트 처리와 대기 로직이 있고 불꽃·톱날 등은 호스트가 피해를 확정하는 경로를 확인했다. 실제 화면/시각 기준을 각 경로에서 조사하고 지연·유실 회귀를 만들기 전에는 일괄 권한 변경하지 않는다. 최적화 변경/비교는 아직 미착수.
- 추격 시야 RaycastAll의 상시 배열 할당을 32개 재사용 버퍼로 변경. 가득 차면 기존 전체 결과로 폴백해 벽 누락을 방지한다. 새 물리 회귀 포함 추격 EditMode61/61 통과. 에디터 8개 콜라이더 1만회 단일 샘플은 구81.71ms/신69.60ms이나 할당 측정이 양쪽 0으로 나와 신뢰 불가, 게임 프레임/4인 향상으로 해석하지 않는다. 나머지 최적화·판정 및 실제 4인 비교는 미완료.
- 달리기 중 동작 실패 핫픽스 요청: 실제 Input System 키보드 W+Shift+Space 조합을 기존 플레이 회귀에 추가해 평지에서 이동 중 점프 상태 진입 PASS. 착지 연출 후 나머지 원래 검사도 PASS. 동일 증상은 이 조건으로 재현되지 않아 장소·조건 문의, 경사/코스 조작 전환 조사 중. 아직 핫픽스 완료로 보지 않는다.
- 사용자는 탈출로를 제외한 대부분 장소에서 혼자 플레이할 때도 발생한다고 설명. 실제 대기실 단독 호스트 씬을 연 뒤 합성 W+LeftShift+Space 입력으로 달리기·점프 테스트도 1/1 PASS. 키 조합과 점프 외 증상 확인 요청. 통과 결과만으로 실기 버그가 없다고 판단하지 않는다.
- 사용자가 달리기 중 점프가 다시 정상 동작한다고 확인. 관련 두 입력 회귀는 유지한다.
- 대포 착지: 신뢰성 착지 요청이 소유자의 위치보다 먼저 도착해 5m 초과할 때 기존 코드가 즉시 위험 착지/부상으로 확정하던 경로를 동일 비행 위치 도착까지 보류한다. 비행 식별자/생존 여부 확인, 재시작 정리, 유한 좌표 검증, 안전 판정·낭떠러지·이벤트에 보고된 착지 좌표 사용. 위치가 계속 도착하지 않으면 확정도 대기한다. 시뮬레이션 Play1/1·착지 Edit20/20 PASS, 실제 Relay 미검증.
- 착지 PlayMode 필터 13개 중9 PASS/4 FAIL. ActivePoleFlightSnapshot의 Fall/None, ActiveCatchTwoSpawnedPlayers의 옛 수동 준비, LandingWave 지원 판정, SwingPole의 옛 수동 준비 단계 실패. 이전 기준/단독 재현 및 원인 확인 필요. 아직 전체 회귀 통과 아님.
- 최신 dev4be086c6을 Unity 정상 종료 후 충돌 없이 반영. 그네의 owner 착지 보고가 먼저 도착하면 위치 동기화를 기다리는 기한을350ms→1.25초로 늘림. 기존 라운드·동일 비행·실제 줄 지지면 검증 및 6초 비행 만료는 유지. 실제 씬 Play 검사는500ms 뒤 지지면 위치 도착 시 확정과 취소된 비행 보고 거부 1/1 PASS. SwingPole 기존 Play2개 중1 PASS/1 FAIL(폐지된 수동 Prepare 기대), 대포 착지 Edit20/20 PASS. 실제 Relay는 미검증.
- 외줄 톱·불꽃과 대포 장애물은 호스트가 늦은 소유자 위치를 현재 장애물 시각에 대입한다. 각 시각/소유자/라운드를 검증하는 표시 접촉 보고가 필요하며 아직 적용하지 않았다. 외줄 균형·장애물 지연 개선 및 네트워크/렌더 최적화의 4인 전후 비교는 미완료.
- 외줄 균형 매달리기: 소유자의 요청이 호스트 위치보다 먼저 오면 같은 줄·거리의 기존 호스트 검증을 최대1.25초 재시도. 라운드 변경·비활성·접속 해제·사망·재시작 시 보류를 지운다. 실제 공연 씬에서 위치500ms 지연 후 승인, 재시작 시 보류 제거 Play1/1 PASS. 아직 실제 Relay·장애물 검증은 하지 않았다.
- 기존 HangRescue Play4개 중3 PASS/1 FAIL(구조 시작 프레임 이동량 0.08m, 기대 0.05m 미만). 실패 항목 단독 재실행은 Unity Pipeline main-thread timeout으로 시작되지 않아 ENV_BLOCKED. 그 이후 editor_status도 30초 timeout; 강제 종료하지 않음. 원인과 이전 기준 미확인.
- 2026-11-04 Unity 응답 복구 확인. 일반 대포 HumanCannonHazardCourse의 원격 판정을 피해자의 표시 접촉 sweep→41B reliable 시작/해제 보고→호스트 epoch/RestartVersion/순서/시각/좌표·형상 검증으로 구현. 같은 접촉 중복절단과 동시다중접촉 제한, 기존 확정 이벤트 보존. v2 전원 동일 빌드 필요. 별도 외줄 톱/화염/FinalSaws/SawGate/Route는 아직 미변경.
- 신규 Play: 움직이는 장애물300ms 지연·회피/중복/옛코스/재시작 거부1/1 PASS, 실제스폰 원격owner777에 대해 HostContacts만으로 절단 안 됨1/1 PASS. hazard-owner-report-tests.json/hazard-host-gate-tests.json 보존. 테스트용 예전 authored pole fixture가 최신씬에 없어 최초 실행 실패했고 독립 fixture로 교체했다. 이 검사는 실제네트워크/4PC가 아니다.
- 실제4인 시도: computer-use로 공식 MPPM Default/EscapeEntryFourEditors 메뉴·Scenario 설정 창 확인. 설정 창 접근성에 실행 구성요소가 없고 화면 캡처와 설정 창이 일치하지 않아 4인 세션을 시작하지 못함. 외부 POC_CURTAINCALL 에디터는 조작하지 않았다. 실제 Relay·유실 지터·4인 FPS/위치 도착간격 비교는 미완료이며 현재 개선률을 주장하지 않는다.
- 기존 대포 착지 EditMode20/20 PASS. SwingPole PlayMode2개 중1 PASS/1 FAIL(구조 도우미 IsReady, line126), 실패 항목 단독 재실행도 FAIL. 해당 검사는 차단된 옛 수동 Prepare 요청을 사용해 그네 동작 전에 실패한다. 잘못된 필터로 실행된 NO_PROJECT_TESTS는 PASS가 아니다. check-work 안전성/문서/컴파일 PASS.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 이번 저장 범위: 기존 대포 착지·그네/매달리기 지연 보완·입력 회귀·추격 시야 버퍼·일반 대포 장애물 접촉 보고. Unity 정상 종료 후 최신dev4be086c6 이미 포함 확인, 충돌 없음. 실제4인 전후 비교와 별도 외줄/장애물 시스템 검증이 남아 있어 완료 PR 대신 드래프트로 제출한다. 기존 GripYellow.mat/Task083 preview 변경은 포함하지 않는다.
- 드래프트 PR #279 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/279 제출, 구현 커밋8c17574c push. 실제4인/Relay·성능 전후·별도 장애물 시스템은 PR 설명과 todo에 미완료로 명시했다.
