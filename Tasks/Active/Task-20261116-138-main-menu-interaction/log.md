# Log

- 2026-11-16: 요청 범위로 Task 생성. 담당 @MoHoDu, 기존 fix/main-menu/interaction-effect 브랜치 사용. 초기 변경 없음, origin/dev fetch 성공 및 HEAD 동일(0/0).
- doctor 1회 실행: third-party 미초기화, 기존 Task ID 중복, Unity 설치 감지 실패. Windows 설치 경로를 직접 확인했고 DLL/실행 파일 존재. CLI 업데이트 확인은 DNS ENOTFOUND. 기존 인증 설정은 변경하지 않음.
- start-work.ps1은 중첩 브랜치 이름을 거부했으나 new-task -UseCurrentBranch는 수용. 브랜치 이름을 변경하지 않고 작업을 이어감.
- 실제 시작 씬은 Assets/Scenes/InGame/Start.unity. 기존 지도/빌더는 Start_Lobby 경로를 언급하므로 재생성하지 않고 StartMenu에서 같은 씬의 Start Stage를 연결.
- 커튼 3폭의 주름 메시를 런타임에만 교체하고 상단 고정/높이별 국소 스프링을 적용. 의자는 볼록 메시 충돌체와 Rigidbody로 클릭 위치에 Impulse를 가하며 0.65초 이내 연속 입력/0.14초 홀드 반복으로 강화. 바닥/무대 경계 충돌체 생성.
- Pin Spot만 최초 18~35초, 이후 25~55초 간격으로 0.28초 이내 두 번 감광. 완전 소등하지 않음. UI 위에서 시작한 누름은 배경으로 이어지지 않으며 설정/접속/씬 전환 중 입력 차단.

- 2026-11-16 검사: 안전성=FAIL; 문서/규칙=SKIPPED(선행 실패); 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 첫 check-work는 새 .meta가 생성되기 전 파일 목록으로 실행되어 안전성 실패. StartMenuBackdrop.cs.meta를 보완한 뒤 재검사하여 안전성/문서 규칙 통과. 에디터 부재로 정식 Unity 컴파일은 ENV_BLOCKED.
- 설치된 Unity 6000.3.25f1 Roslyn 및 기존 프로젝트 참조 DLL로 StartMenu.cs + StartMenuBackdrop.cs를 함께 오프라인 컴파일: exit 0. 기존 Assembly-CSharp 참조와 같은 타입의 소스 재정의 경고(CS0436)만 숨김. 이는 Unity 전체 재임포트/Play Mode 성공을 뜻하지 않는다.
- 캐시된 전체 Assembly-CSharp.rsp를 이용한 추가 컴파일은 현재 소스 목록과 불일치하여 실패(추격자 partial/OfflineLocalPlayer 등 누락). 다른 기능을 수정하지 않았으며 전체 프로젝트 컴파일 통과로 기록하지 않는다.
- unity open --batchmode로 검사용 에디터 실행을 시도했으나 LicenseClient 채널 연결 단계에서 Pipeline 연결이 생성되지 않음. 계정/라이선스는 변경하지 않았다. 최종 동기화 전 Unity 프로세스 없음 확인.
- verify-knowledge: OK(20 folders), git diff --check(ignoreSubmodules): 통과. 제출 직전 origin/dev fetch 성공, HEAD와 동일(0/0). commit/push 없음.
- 다음 확인: Start 씬 Play Mode에서 커튼 클릭 높이의 국소 반응·상단 고정, 의자 한 번 밀기/연속 클릭·길게 눌러 넘어뜨리기, 무대 경계, UI 클릭 누출 차단, 조명의 드문 이중 감광을 확인한다. 실제 넘어짐 강도·최종 시각 품질은 미검증.

## 후속 요청 · 타이틀과 크레딧
- 2026-11-16: Unity 연결됨. 저장된 Start 씬/Play 중지를 확인하고 에디터 종료 후 dev 11커밋 fast-forward. 원격 Task-134와 충돌하여 이 미커밋 Task만 137로 재번호화.
- 의자 뒤·양옆 경계를 커튼 원래 bounds와 최대 변형 여유로 계산하고, 회전한 의자 전체 bounds를 매 프레임 보정. 강한 90 impulse 후 넘어짐과 커튼 영역 미침범 확인.
- 타이틀 1020px 기준 확대/그림자 강화. 새 TitleSuspensionRopes 프리팹은 꼬인 섬유·결속·금속 고리의 해상도 독립 UI 메시. 미세한 앞뒤/좌우 흔들림, 좌우 6회 입력 후 먼저 누른 쪽 줄만 한 번 늘어남. 실제 낙하 25.2px, 메뉴 간격 34.85px 확인.
- 개발 및 라이선스 버튼/모달/스크롤바/전문 보기/원문 링크 구현. 네 GitHub 닉네임과 초기 라이선스 5개. Settings 에셋 및 Project Settings UI로 추가/삭제. 글리프는 전용 런타임 TMP 폰트에 생성.
- Unity 컴파일과 실제 Play Mode 검증 성공: 좌/우 임계치, 추가 30회 입력의 중복 낙하 방지, 모달 입력 차단, 설정 복제로 개발자 추가/삭제 및 전문 표시. 검사 스크립트와 Captures/ 및 verification.md에 근거 보존.
- 최초 TMP 폰트 생성 오버로드/커스텀 Graphic CanvasRenderer 누락을 수정하고 재검증. 최종 런타임 오류 0. 기존 패키지 목록 갱신 오류는 이번 코드와 무관.
- 전체 SaveAssets를 제거하고 설정만 저장. 이번 에디터 임포트/저장 과정에서 자동으로 비워진 기존 동적 폰트 4개는 .harness/local/font-import-backup에 변경본을 보관한 뒤 작업 시작 내용으로 복구. 기존 사용자 변경은 건드리지 않음.
- 최종 origin/dev fetch/fast-forward 확인: 이미 최신(0/0). commit/push 없음.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 실제 GraphicRaycaster 검사: 타이틀 SignHitArea 선택, 렌더된 크레딧 모달의 Inset/Programme이 뒤 타이틀보다 우선하는 것 확인. 모달 생성과 같은 프레임의 depth 검사만 다음 프레임으로 분리. Play Mode 종료 후 Start 씬을 유지했다.

## 후속 요청 · 버전 0.8.1 및 전 씬 표시
- 제출 요청: 사용자가 실제 LIVE/TEST Player 빌드 확인은 추후 직접 진행하고 현재 변경 저장 및 dev PR 제출을 승인했다. 제출 전 Editor 저장/종료와 origin/dev 최신 상태를 확인했다. doctor의 기존 환경 감지 경고와 달리 gh 계정 MoHoDu 및 실제 Editor 연결은 정상이다.
- doctor 1회 실행. 기존 third-party/Task 중복/Windows Editor 경로 감지 경고 유지. 실제 Unity 6000.3.25f1 Pipeline 연결로 작업 가능.
- 작업 시작 전 저장된 Start 씬과 Play 중지 확인, Editor 종료 후 origin/dev를 6a841241로 fast-forward. 원격 Task137 추가와 충돌하여 이 미커밋 Task만 138로 재번호화.
- PlayerSettings.bundleVersion=0.8.1. Editor API로 설정한 후 정상 종료하여 디스크 저장 확인(기존 0.2.1에서 해당 한 줄만 변경).
- BuildVersionUI는 BeforeSceneLoad에서 생성하고 DontDestroyOnLoad로 유지. 어떤 씬에서 직접 Play해도 생성하며 1920x1080 기준 (24,18), 16pt. 마이크 (24,70) 아래에 배치. GraphicRaycaster 없음, 텍스트 raycastTarget=false.
- 환경 판정은 기존 TelemetryRuntime/PlaytestSurveyRuntime과 동일한 Application.isEditor || Debug.isDebugBuild. Editor/Development Build=TEST, 일반 배포=LIVE. 버전 문자열은 Application.version에서 읽음.
- Unity 재컴파일 성공. 실제 Start 및 빈 씬 직접 Play에서 0.8.1_TEST/단일 인스턴스/영속 씬/마이크와 좌표 비중첩/입력 비차단 검증 성공. 새 런타임 씬으로 활성 씬 변경 후 동일 인스턴스 유지 확인. LIVE Player 실행은 별도 빌드하지 않음.
- Play 종료 후 저장된 Start 씬 복귀. Editor 정상 종료로 생긴 기존 TMP 폰트 4개의 동적 아틀라스 변경만 .harness/local/version-font-backup에 보관 후 원래 내용 복구. 제출 전 origin/dev fetch 및 ff-only: 이미 최신. Editor 재실행. commit/push 없음.

- 2026-11-16 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-16 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/394 (fix/main-menu/interaction-effect → dev)
