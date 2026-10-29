# 독립 빌드 태그·0.1.0 설문

2026-10-22 PM 요청: 이전 변경 저장 후 두 폼 자동 열기·응답 시트/대시보드 연결. 폼 관리자 편집은 사용자만. 두 PC 검증 보류·최소 검사 유지.

## 기존 작업 저장
- c17f2a1 `feat: 로그 분석 개선과 대시보드 멤버·보관·삭제 관리`, 작성자 MoHoDu <62794882+MoHoDu@users.noreply.github.com>. 승인된 로그/문서/서버40파일만 로컬 커밋. 기존 폰트1/머티리얼3 자동 변경 보존. push/PR/병합 없음.
- 기본 save-work DryRun은 기존 dev 수신77건을 구역 위반으로 판단해 FAIL. origin/dev 기준 scope·Fast 및 실제 editor_status/recompile up_to_date 확인 후 명시 경로만 저장. 검사 훅/작성자 확인 유지.

## 구현
- LOGS / SURVEY_010 독립 manifest. 새 빌드 기본 둘 다 off, 메뉴에서 프로젝트별 EditorPrefs 선택. MPPM은 부모 프로젝트의 태그 키를 공유. runtime 로그 bootstrap/Awake가 LOGS 없으면 디스크/HTTPS/relay를 시작하지 않음. survey-only는 endpoint/token 불필요. 기존 빌드에는 소급 적용 안 됨.
- BuildPlayerProcessor가 Library 생성 manifest를 StreamingAssets에 추가. LOGS만 설정 검사·전송 설정 포함. SURVEY_010은 정확한0.1.0+두 entry 설정이 아니면 빌드 차단. Unity ProjectSettings/씬/프리팹 미수정.
- PlaytestSurveyRuntime은 CampaignSnapshot/보상 공유 SessionId+Generation/본인 slot을 관측. 밤1 대기실 도착 시 본인 브라우저, CommonMenuUI.Menu.OnLeaveConfirmed 뒤 Start+Offline에 도착했을 때 종료 설문. 호스트 강제 종료·연결 끊김은 종료 설문 대상 아님. 로그 런타임과 독립.
- 사용자 계정(조회 가능 시) 또는 설치 fallback 해시·폼별 SurveyCache marker, CreateNew 원자 선점. 다음 런/재시작에 재열기 금지, 브라우저 호출 예외면 marker 회수. 제출완료가 아니라 브라우저 호출을 1회로 처리, 캐시 삭제/다른 PC는 재열기 가능.
- 사용자가 직접 연결 질문 추가 및 prefill 제공: NIGHT1 entry.280071335 / EXIT entry.809038892. 제목 공백 차이는 정규화. 코드 CCS1.0_1_0.TEST|LIVE.NIGHT1|EXIT.runN.slot.invitationN.openedUnix. 서비스 로그인ID/방 비밀번호/토큰은 URL에 넣지 않음. 편집 가능한 분석 연결 키로 신원 인증 아님.
- 설문 응답2시트는 읽기만, Google Forms 제출은 미실행. 중앙 설문 응답/설문 통계/설문 동기화 생성·스타일/필터. 30분/수동 refresh에 두 원본 읽은 뒤 snapshot 교체. 연결 질문 없는 과거응답 제외·안내, 같은 invitation 첫행만. TEST/LIVE·버전·run/slot 구분, survey-only도 집계. 읽기 실패 시 이전 snapshot 유지+error 안내. 폼별5,000건 상한, 100개 페이지·긴 자유응답 일부표시/원본안내.
- 문항분포·복수선택(응답자 분모)·서술형/응답 상세·설문 열 때까지 host확정 사망/절단/협동. run+slot으로 연결, 다른계정/색으로 추정 안 함. 응답 증가 시 eventIndex·삭제규칙1회조회로 비용 제한.
- 관리자 삭제 미리보기/서버fingerprint에 설문 포함, 설문-only 세션 목록 선택 가능. 삭제규칙을 중앙조회/compaction/reimport에 적용, 원본 폼응답은 유지. 기존 수신v19 삭제규칙 호환이므로 변경 불필요.

## 최소 검사
- doctor 샌드박스 FAIL은 실제환경 submodule Ready·gh MoHoDu 확인 후 WARN으로 정정. Pipeline 재시작 요청 후 ready, Unity6000.3.25f1 확인.
- Unity compile PASS. 설문 캐시/태그/URL EditMode7/7, 실제 Start survey-only bootstrap/menu binding PlayMode1/1 PASS(1.87초). 브라우저 열기/설문제출 미실행. 기존 telemetry PlayMode fixture에 tag setup/restore 추가, 해당 전체테스트 재실행 안 함.
- Node80/80 PASS(기존73+설문7): 코드 오류/버전/환경/form/slot, 중복, title공백, 설문-only, 문항분모, 자유응답, timestamp cutoff, pagination, 삭제/reimport.
- 실제 source2시트 header readback, 중앙 설문 동기화 NIGHT1/EXIT 각 linked0/unlinked0/duplicates0/OK. scheduledRefresh 04:37:37~56 KST 정상. 이후 자동갱신도04:40:38~39 OK.
- Code/HTML 로컬 readback 일치, 대시보드v21 04:39 → eventIndex 최적화v22 04:42 배포(동일 URL/권한/메일scope/수신v19 유지). Drive 설치 code/HTML 같은2ID 갱신. 실제3개 시트 헤더/필터/열너비 확인.
- Chrome local TEST 예시 문항3개/복수선택100%/응답상세 PASS. 실제응답0이라 실제값 검증 아님. 사용자가 실제 웹에서 설문 통계·두 탭 표시 확인(내용은0이라 미검증).
- 실제 PC 빌드·밤/ESC 브라우저 자동열기·캐시초기화·실제제출→중앙집계·두PC 미검증. Docs/Log-system/survey-guide.md에 운영 순서. 새 동작은 구현완료/사람검증대기이지 전체 Task done 아님.
- 증거: .harness/local/telemetry-surveys-preview.png, telemetry-surveys-v22-deployed.png. 기존4개 자동에셋 외 새로운 추적 에셋 변경 없음.
