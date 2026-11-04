# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

- 이전 측정·통합·피해자 접촉 검증 원문: [보관 기록](History/001-network-perf-before-return-fix.md). 남은 Relay/저사양 검증은 todo와 roadmap 유지.

## 2026-11-05 복귀 중 포획·직접 추격 눈
- 사용자 보고: 순찰 제한으로 복귀하는 추격자가 눈앞 플레이어도 잡지 않음. 흰색/숨김에서 직접 추격 시 즉시 빨간 뜬 눈 요청.
- Unity 프로세스 없음 확인. doctor 1회 FAIL: 외부 에셋 초기화/기존 Task ID 중복/Unity6000.3.25f1 미설치, editor-link 없음. CLI 버전 차이·스킬 링크·gh 로그인 경고. 코드 수정은 가능하나 Unity 컴파일/실기 환경은 없음.
- origin/dev15702992 fetch/fast-forward. 기존 머티리얼3개는 upstream과 동일한 변경이라 보존 stash 후 적용(충돌0), 새 dev 내용으로 보존됨. 보관 stash codex-preserve-before-pursuit-return-fix 유지. 기존 Task083 PNG14개 및 private 에셋 변경 보존. 새 커밋/push 없음.
- 원인: TickBrain의 returningToPatrol ? null 분기가 복귀 내내 시야를 차단. 이를 제거하고 재획득 시 복귀/기존 내비 상태 해제. CanAcquireTarget은 제한 안 재발견, 제한 밖 포획 거리의 근접 위협을 허용. 제한 밖 먼 대상은 복귀 유지, 근접 대상이 벗어나면 다시 복귀. 시야각·벽·방·치트·원격 소유자 접촉 검증 유지. 눈 범위도 근접 예외와 맞춤.
- 흰색 또는 Alpha=0 상태에서 visible Chase 첫 Tick(delta=0 포함)에 Alpha/Openness/ChaseColor/ChaseEmission 즉시 적용. 표시 중 Alert→Chase·페이드/닫힘은 기존 시간 유지.
- 기존 눈 EditMode 테스트에 직접 전환3조건/숨겨진 논리 상태2조건 추가, 기존 PlayMode 복귀 테스트를 재발견·반복 방지·뒤쪽 인식 제외·제한 밖 실제 근접 포획으로 보완. Unity에서는 아직 실행하지 않음.
- 현재 C# 눈 전환/설정 소스를 Add-Type으로 컴파일한 독립 probe9조건 통과. 최소 Unity 수학/속성 대역을 사용하므로 Unity 컴파일·렌더·AI 검증과 구분한다. 초기 probe의 완료 보간 색상 exact 비교 실패는 부동소수점 허용오차로 검사 도구만 보정 후 통과. probe는 무시되는 .harness/local/pursuit-eye-probe에 보관.
- record-knowledge로 기존 pursuit-eyes/runtime 사실·추격 결정 문서를 갱신. 다음 확인: Unity6000.3.25f1/외부 에셋 준비 후 눈 EditMode·복귀 PlayMode·실제 복귀 접근/직접 추격 화면 및 원격 포획 확인.
- 이번 저장 범위: 기존 대포 착지·그네/매달리기 지연 보완·입력 회귀·추격 시야 버퍼·일반 대포 장애물 접촉 보고. Unity 정상 종료 후 최신dev4be086c6 이미 포함 확인, 충돌 없음. 실제4인 전후 비교와 별도 외줄/장애물 시스템 검증이 남아 있어 완료 PR 대신 드래프트로 제출한다. 기존 GripYellow.mat/Task083 preview 변경은 포함하지 않는다.
- 드래프트 PR #279 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/279 제출, 구현 커밋8c17574c push. 실제4인/Relay·성능 전후·별도 장애물 시스템은 PR 설명과 todo에 미완료로 명시했다.

## 2026-11-04 통합 대기실 제출 뒤 4인 LAN과 외줄 장애물
- 통합 대기실은 Task100의 별도 PR280 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/280 제출 완료. 해당 씬 변경을 PR279에 섞지 않고 현재 브랜치로 복귀했다. 시작 당시 origin/dev4be086c6 이미 포함.
- 공식 MPPM Player2/3/4 실행에 성공해 메인 포함4개 실제 LAN 프로세스 연결. 100ms 송신 지연/30ms 지터/3% 유실을 UTP 드라이버에 적용해 RTT238~240ms 확인. 기존 reliable와 unreliable 위치 델타를 A→B→B→A, 각15초 정상 외줄 왕복으로 측정. p95 위치 오차 반복 평균1.916→1.329m(약31%); 프레임p95 약23.2ms로 개선 근거 없음. Player.prefab의 UseUnreliableDeltas만 적용. 원본ZIP·분석기·보고서 보관.
- 첫 측정은 추락 상태여서 폐기, 두 번째는 정지 구간이 섞여 제외. 정상4인/높이/이동거리 검사를 통과한 마지막4회만 최종통계. 서로 다른4PC/Relay/출시FPS/GC/대역폭 측정으로 주장하지 않는다.
- 외줄 불꽃: 37B 피해자 접촉, 호스트 라운드/재시작/순번/시간/좌표·발화시각 검증. 원격 host-only 사망 차단, 기존 Kill·구조해제·이벤트 유지. 신규3+기존화재경고1 Play4/4 PASS. 테스트용 설정 배열 변경으로 FireView 캐시 예외가 난 최초회귀는 새 snapshot 인스턴스로 고쳐 재검증.
- 실제 지연 불꽃: 피해자12m 안전/호스트복제6m 접촉은 무사, 실제 피해자6m 접촉은138ms 뒤1회사망확정, 양쪽일치. 설정 조작 초기 시도의 표시 캐시 예외와 판정검증을 구분해 보고서에 남김.
- 외줄 톱(가로/수직/협동/해제관문): 42B 피해자 진입/해제·재전송, 호스트30Hz/48칸 형상이력·소유자/시간/회차/재시작 검증, 지속 접촉 중복절단 차단. 기존 ServerTouch/Hit/절단순서 재사용, 메시지v2. 신규2+경로표시1 Play3/3 PASS. 초기 실패는 시작선 통과 전 빈 이력을 읽던 fixture였고 실제 시작조건을 포함해 수정.
- 실제 지연 가로톱: 안전한 owner2의 호스트복제만 톱에 놓아도 무부상. 호스트 StopHorizontal 동기화로 정지한 톱의 새 접촉은173ms 뒤1부위 절단,30초 지속접촉에서도 추가절단0, 다른3명무부상, 양쪽상태일치. 움직이는 톱의 최초 순간이동 접촉이 즉시 승인되지 않은 시도는 빠른회피/완전접촉검출 통과로 취급하지 않음.
- 남은 범위: 수직/협동/해제관문의 실제다인 경계회피, 별도 FinalSaws/SawGate/Route,4대Relay·저사양·대기실렌더. PR279는 이 미완료 범위를 명시한 draft 유지.
- 종료 전 check-work 안전성/규칙/컴파일 PASS. 가상 플레이어를 UI로 정상 종료하고 미저장 씬0 확인 후 작업 Editor 종료. 최종 fetch origin/dev4be086c6, Already up to date/충돌0. 종료 때만 생긴 TightropeCourse.prefab 자동 직렬화/출발점 변경은 .harness/local 백업 후 정확한 역패치로 복구, diff0 확인. 기존 GripYellow/Task083 preview 및 두 재질 소수점 변경은 보존·커밋 제외.

## 2026-11-04 PR280 병합 후 후속
- 재개 doctor1회 실행. sandbox의 외부 에셋/Editor/gh 경고는 실제 권한 환경의 verify-third-party와 명시 경로 Unity 연결로 구분했다. 중복 Task ID는 기존 문서 문제, start-work의 기존 slash 브랜치 정규식 거부는 수동으로 기존 Task/브랜치를 유지해 진행했다. 이번 재개에서 doctor 반복 안 함.
- 작업 Editor 종료 상태에서 origin/dev8b8910da(PR280)를 받아 충돌 없이 병합(c6da5ee6). 통합 WaitingRoom 회귀2/2, HangRescue4/4 통과. 이전0.08m/0.05m 실패는 재현되지 않았다. 옛 수동 준비 F fixture를 현행 장갑/자동 지원으로 수정해 그네2/2·장갑 지원1/1·착지파1/1·부상4단계 최종 비행1/1 통과. 최종 비행은 현재 station의 발사 속도/조절 속도와 실제 지원 landing zone을 사용한다.
- 별도 HumanCannonFinalSaws/Route 불꽃/SawGate에45B OwnerContactChannel 추가. reliable 진입/해제·150ms 재시도, 호스트 소유자/장치 epoch/RestartVersion/순번/유효 시각/좌표 envelope/형상 검증 뒤 기존 ServerCut/ServerKill 확정. 초기화/해제 정리, 짧은 sweep과 Teleported 경계 보호. 현재 씬에 없는 구형 FinalSaws는 독립 fixture로 검사했다. 수신/요청 프로토콜v2, 동일 빌드 필요.
- SawGate는 합쳐진 외곽 사각형 대신 개별 렌더 형상으로 빈틈을 유지한다. 최종 후속은 피해자 현재 표시 형상과 호스트30Hz/48칸 검증 이력을 분리하고 원격 호스트 형상 중복 검사를 생략한다. 콜백이 최초 세션 객체를 캡처하지 않도록 현재 세션을 매번 조회한다. 원래 운반자 관문 통과/기존 절단·사망 경로 유지.
- 실제 독립4프로세스 LAN: cannon-gate-v2/fire-v2 각각90초, 송신100ms/지터30ms/유실3%, 모든 샘플4인 연결. 피해자 안전/호스트복제만 접촉은 무사. 실제 접촉은 톱158ms 뒤1부위, 불꽃134ms 뒤 Fallen, 양쪽 상태 일치. 지속 접촉 중 추가 절단0. 이는 최종 현재형상 분리 직전 버전이며 후속 자동 회귀와 구분한다. 원본8개ZIP·요약·이벤트시각 보관. 다른4PC/Relay·사람 회피로 주장하지 않는다.
- 소유자 접촉6/6, 빠른 톱4/4 통과. 삭제된 PoleSaw6 fixture를 독립 장치로 교체했다. 선택형 수직·협동·해제 관문300ms 지연/회피/중복 회귀1/1 통과: 현재 비활성 설정은 유지하고 검사 메모리 복사본만 사용한다. fixture의 static 설정 접근/1초 피해 간격/옛158m 배치/동일 프레임 중복 시각을 수정했다. 같은 시각의 활성화 전 이력과 섞지 않고 정상 Update 후 보고한다.
- PursuitPerformanceProbe는 선택된 통합 WaitingRoom을 측정하고 씬별 최초 로드를 구분하도록 변경. 통합씬 세션 첫 로드3.42초/준비4.55초/최장947ms/GC328ms. 로비7,232 batches/4,741 shadow casters. 저사양/캐시 초기화/출시 빌드 결과 아님.
- 임시 GRD 렌더 A/B: 첫4회는 필수 KeepAll 설정 누락으로 기능 비활성, 무효로 구분. 읽기 전용 EditorGraphicsSettings 대입 컴파일 실패는 SerializedObject로 수정. 보완 후 actual instance 확인, valid-b1은 셰이더 준비로 첫 방 누락/complete=false, valid-b2는3방 완전. 로비 요청 약6,322→1,455지만 프레임 중앙값14.21→15.80ms로 이득 미확인, 기본 에셋 적용 안 함. 긴 셰이더 준비 뒤 정상 렌더 프레임부터 표본 수집하도록 도구 보완, 모든 임시 설정 정상 복구/GraphicsSettings diff0 확인.
- 대포 전체 최초58=41통과/15실패/2생략, 현재 씬 fixture 정리 후58=49통과/7실패/2생략. 단독 철망 위 비행도 통과. 나머지 검사는 현행 리허설·일정 난도/연속 반동·관성 보정/검사 객체 참조와 대조 중이다. 전체 통과로 기록하지 않는다. PracticeBot 실행파일을 요구하는2개는 생략이다.
- 상세 근거: followup-verification-20261104.md와 Verification-20261104/. Knowledge remote-player-judged-before-placement 보완·verify-knowledge OK.
- 최종 소유자 접촉7/7과 봉 UI 개별 회귀 통과. C6 실패는 InputGate.Register 직후 같은 프레임의 중립 대기와 alpha0 진입을 가시성 실패로 보던 검사 조건이었다. 실제 입력 리더를 중립화한 뒤 현재 봉 거리/중심·F→Space 안내 수락을 검증한다. 비행 수신 fixture에는 station placeholder 배열, 현재 봉에는 optional LinkedGate null 조건, 정식 공연에는 리허설/결과 전환/발견·일정 난도를 반영했다. 현행 연속 반동·0.1초 관성 보정과 재시작 입력은 실제 몸 이동으로 개별 통과했다.
- 최종 코드 실제4인 재검사: gate-final-v2 90초, 별도 gate-final-hold-v2 60초 모두 송신100ms/지터30ms/유실3%·전체 프레임4인 연결 유지. owner1 host-only 접촉은 무부상, owner3 실제 접촉01:46:45.1659372Z→확정01:46:45.3526210Z=약187ms. 추가60초 모든 화면 owner3 LostParts min/max=4(LeftLeg1개), 마지막 contact=true/Normal. raw8개ZIP·프로세스별 상태/통계/시각 보관. 가상 Editor 시작/갱신의 기존 MPPM readonly AssetDatabase Assert2개를 게임 예외와 구분했고 Console 전체 오류0으로 주장하지 않는다. 이후 세 가상 플레이어 정상 종료.
- 최종 대포 전체58=55통과/1실패/2생략. 철망 위 비행·지상 톱 점프·빠른 톱·접촉·부상별 비행·현행 봉 조작/UI·정식 공연 잠금 통과. 실패1개는 기존 원본 운송판 폭4m와 트램폴린 지름4.95m/손잡이2.8m 적재 치수 불일치. 씬과 HumanCannonTransport.cs는 dev8b8910da diff0, 제품 배치/기대값을 바꿔 숨기지 않는다. PracticeBot 실행 파일 없는2개 생략. cannon-full-final.json 보존, 전체 PASS 아님.
- 전체 회귀가 Task083의 다른 미리보기 PNG도 재생성했다. 기존 play-land/재질 변경과 함께 다른 Task 출력·외부 에셋 변경을 이번 커밋에서 제외해 보존한다. 남은 성능 범위는 추격자 보간·첫 로드/GC·그림자·정적 배칭 후보와 실제4PC/Relay·저사양·사람 경계 회피다.
- 첫 최종 check-work는 새 결과 파일7개의 대문자 이름 때문에 문서/규칙 FAIL, 컴파일은 선행 실패로 SKIPPED였다. 해당 파일을 소문자 kebab 이름으로 정리했다. 검토에서 같은 씬 서버 시각 되감김의 샘플 예약/수신 하한 문제도 좁게 보완:1000→1초 합성 시각에서 새 형상 즉시 기록/옛 형상 거절 Gate Play1/1 PASS, 이후 현재 소스 컴파일 PASS. 정상4인 경로는 앞선 실측, 시각 경계 추가는 개별 회귀로 구분한다.
- 보완 후 최종 check-work 안전성/문서규칙/Unity컴파일 PASS, 테스트는 위 별도 결과 재사용. 미저장 씬0 확인·재개 때의 Start 씬으로 복원 후 작업 Editor만 정상 종료. 최종 fetch origin/dev8b8910da, Already up to date/충돌0. 폰트/라벨 자동 캐시 변경은 종료 후 사라졌고 TightropeCourse.prefab/GraphicsSettings diff0. 기존3재질·Task083 미리보기·private 에셋 변경은 보존하고 커밋 제외한다.
- 구현·회귀·원본 기록 ef06f889 커밋/작업 브랜치 push 완료. dev 드래프트 PR279 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/279 설명/제목 갱신·원격 head ef06f889 확인. GitHub 연결 도구 수정은403(Resource not accessible by integration)으로 실패해 gh 로그인 MoHoDu로 정상 갱신했다. PR280은 이미 dev8b8910da에 병합됨. 기존 적재 치수 실패·출시/4PC/Relay·FPS/로딩 후속을 PR에 명시하고 draft 유지, 병합하지 않았다.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(선행 실패·생략)

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(선행 실패·생략)
- 최초 검사에서 log137줄 HARD로 중단되어 compact-docs에 따라 과거 기록85줄을 History/001-network-perf-before-return-fix.md로 원문 일치 확인 후 보관했다. 재검사 안전성·문서/규칙 PASS(hard0), Pipeline 인스턴스 미발견으로 Unity 컴파일 ENV_BLOCKED·PlayMode SKIPPED. 독립 눈 probe9조건 통과와 구분한다. HEAD/origin/dev 차이0, 최종 실제 플레이 검증은 미완료.

## 2026-11-05 통로 재현에 따른 후속 수정
- 사용자 실기에서 로비→공연장 통로 복귀 무시가 지속. 앞선 CaptureRadius1.15m 예외는 불충분했고 원래 버그 해결 완료로 볼 수 없음. 실제 씬을 읽어 방 밖 추격 OFF는 B1_MachineRoom뿐임을 확인, 씬/방 제한을 임의 변경하지 않음.
- HasVisualContact로 정상 시야 조건(거리/FOV/단계 벽/방/생존/인식)을 공유하고 TickBrain에서 복귀보다 먼저 평가. 순찰 제한 밖에서도 직접 시야 접촉은 복귀 취소·공격 준비 유지. 놓치면 기존 이탈 복귀, 비시각 원거리 AlwaysNearest/청각만으로 복귀를 중단하지 않음. 눈의 순찰 범위 숨김도 제거, 유한 인식 반경·방·로컬 대상 조건 유지.
- 색 지연 원인: 바닥은 Prepare부터 빨강, 눈은 Prepare 노랑 후 Chase에서도0.5초 보간. Resolve를 공통 상태 정의로 사용해 자기 대상 Prepare/Chase/Capture의 눈을 전부 즉시 빨간 뜬 눈으로 적용. 바닥도 상태색 변경을 지면 투영10Hz와 분리해 같은 프레임 갱신. 일반/경계 열림·닫힘/이탈 페이드와 색 설정 유지, 배치 창 설명/tooltip 정정.
- 기존 PlayMode를 실제 Lobby Actor 선택·제한 밖3m(포획 거리 초과) 재발견·prepareUntil 불변·추격·시야 뒤쪽 복귀·근접 포획으로 보완. 눈은 Prepare 및 Search→Prepare에서 다음 렌더 프레임 즉시 빨강/뜬 눈 확인. 아직 Unity에서 실행하지 않음.
- 현재 소스 TickBrain/NearestVisible/HasVisualContact/NearestByPath/CueState를 그대로 추출하고 Leash/EyeTransition/EyeSettings 전체를 컴파일한 독립 C# probe: AI8조건+눈/상태9조건 통과. 벽/방/원거리/인식OFF·12틱 준비 타이머 불변 포함. native Physics/NavMesh/렌더는 대역이므로 Unity 컴파일/실기 PASS가 아님. .harness/local/pursuit-return-probe/probe.cs에 무시 파일로 보관. 초기 컴파일의 대역 누락 필드2개/미사용 경고는 probe만 보완.
- 재개 전 Unity 프로세스 없음·fetch origin/dev15702992, HEAD와 차이0. 같은 작업 doctor는 앞선1회 결과 재사용. 기존 미저장 코드/에셋·Task083 PNG 보존, commit/push 없음. record-knowledge로 앞선 좁은 예외 설명을 현행 동작으로 교체. 최종 검사는 아래에 기록하고 실제 통로/원격 포획은 Unity 환경에서 확인 필요.

## 2026-11-05 눈 설정 한글 안내
- 사용자 질문: 설정이 영어라 이해하기 어려움, Fade/Transition 및 상태별 Emission 차이. ui-imgui 스킬로 기존 설정 UI 보완, 눈 설정12개에 InspectorName/Tooltip을 추가해 설정창·Inspector 모두 한글 표시. Fade=전체 투명도, Transition=눈 모양/일반·경계 색 전환이며 빨간 공격 진입은 즉시 적용. Emission=아이콘 자체의 상태별 추가 밝기, 0은 기본 밝기이며 주변 조명이 아님. GlowOpacity와의 관계도 설명.
- 펼친 설정 아래 시간 차이·추가 밝기 안내를 추가. 원래 직렬화 필드명/기본값·설정 에셋 보존, 상태 판정/셰이더 동작 수정 없음. 같은 Task doctor 결과 재사용, Unity 프로세스 없음, fetch dev15702992/HEAD 차이0. 기존 미저장 작업 보존, commit/push 없음.
- 표시 문구/속성만 바꾼 낮은 영향의 수정이라 새 자동 테스트를 만들지 않음. 코드에서 설정12개의 이름·기본값 동일 여부 확인 및 check-work 결과 기록. 실제 에디터 표시 확인은 Unity 미연결로 남음.

## 2026-11-05 전체 저장·dev PR 요청
- 사용자가 브랜치의 모든 작업 commit/push 및 dev PR 제출을 요청. 기존 PR279는 MERGED라 새 PR을 만든다. gh 로그인/작성자 MoHoDu(62794882 noreply) 확인, origin/dev15702992와 HEAD 차이0, Unity 프로세스 없음. save-work/submit-work 절차 적용, 이전 동일 코드 검사(안전성·문서규칙 PASS/컴파일 ENV_BLOCKED/실기 미검증)와 독립 AI8·눈9조건 결과 재사용.
- 요청에 따라 재생성된 Task083 미리보기 PNG14개도 함께 저장. private FX_Sparks.mat 변경은 이미11a5483b로 main/dev 지정6d3fd381에 반영돼 파일 diff0임을 확인. 전체 tracked/staged/untracked 조회·stash 보존 후 ignored 덮어쓰기 금지 checkout으로 private HEAD를 dev 지정6d3fd381에 맞추고 재적용/파일 해시 일치·clean 확인. 공개 포인터를 과거4c45f11a로 되돌리지 않고 유지, private 신규 게시 불필요.
- Unity6000.3.25f1/Pipeline 미연결로 컴파일·실제 통로/원격 포획/설정창 실기 미검증을 PR에 명시하고 사용자 요청대로 제출. 새 회귀는 실행 통과로 표기하지 않으며, 기존 운송판/트램폴린 적재 치수·4PC/Relay 후속도 해당 todo에 남긴다. PR은 지정 팀원 검토 대상으로 제출하며 병합하지 않는다.

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(선행 실패·생략)

- 2026-11-05 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)
