# 기획자 가이드·달력·공동 자금·도식 조작

2026-10-22 PM: 비개발 기획자용 하네스 md, Windows/Mac 각각 설명, 대시보드 여섯 항목 수정. 두 PC 검증 보류·최소 검사 유지.

## 구현
- Harness/Project/Guides/Log-system/{index,local-test,pc-build,dashboard-admin,dashboard-member,pc-files,dashboard-reading}.md. 최초 설정/재부팅/OS별 경로/빌드 4조합/TEST·LIVE/관리자 안전 삭제·멤버/분모·가설 예시. Task setup에 PM 요청 구역 추가·Domain과 기존 안내에서 링크.
- Mac 환경 설정은 Windows User 환경 변수와 다르므로 configure-client-mac.command 추가. /bin/zsh로 실행, 비표시 최초 입력·gitignored 0600 파일·설정 상속 후 설치된 프로젝트 버전 Unity 실행. 일반 플레이어 빌드는 기존 설정 자동 포함. 실제 Mac 실행/배포 검증은 안 했으며 문서에 명시.
- 공동 자금 변동 DAY/BREAK. day/rest=1/2/3 아침/오후/밤, 실제 WAITINGROOM breakId 관측만 구간 생성. 호스트 ledger transactionId 중복 제거/순서 유지. 공연 수입은 다음 관측 쉬는 시간, 구간 확정 없는 거래는 미배정. 도달 후 무변동은 이전 관측 잔액 이월·초기 잔액 추정 금지. 진행 중은 마지막 관측값 안내. 일차 기준은 보존.
- 자금 구간 시트: runId/gameDay/rest/rewards/spent/transactions/gameVersion/environment/balance/samples/carried/mode. 같은 core 투영으로 RUN/AVERAGE, 날짜로 좁힌 평균은 해당 날짜 RUN행에서 비교. 기존 자금 일계 열/의미 유지.
- 자금·타임라인·설문 각각 달력/ID 검색/select 분리. KST 최초 플레이 기록 날짜, 설문-only는 최초 초대 시각과 SURVEY 라벨. 기록 날짜만 활성·전체 날짜 해제, 빈 결과 명시·검색에 안 맞는 선택 숨김, 날짜 서버 필터는 평균/설문 통계에도 동일 적용. 상단 환경·버전 변경은 검색/날짜 초기화. 설문 시작 조회는 O(events+responses) 인덱스.
- route SVG width+height 픽셀 고정·min-width 100% 제거. 좌표 간격만 줌, 아이콘 32px 유지. pointer capture 5px threshold drag, release 클릭으로 상세가 오작동하지 않게 capture click 차단. 휠/버튼/하단 스크롤 유지.
- 이용현황 네 지표 세로 4행, 첫도착 미수집도 항상 표시. reading-note 음수 margin 제거, survey-status22px·소제목 아래12px.

## 최소 확인
- doctor 샌드박스 FAIL(submodule 권한) → actual escalation third-party Ready. Unity 연결 필요 없는 서버/문서 변경으로 에디터 수정/컴파일/게임플레이/두PC SKIPPED. 기존 Unity 미커밋·폰트/머티리얼4개 보존.
- Scope(origin/dev) PASS·Fast PASS(기존 문서 WARN). Node84/84: 기존80+구간 carry/표본/미관측·날짜 KST/평균·설문-only날짜·구간시트일치. 테스트 fixture appSessionId 누락1건 수정 후 통과. Mac zsh가 없어 실행/구문 검사 SKIPPED.
- Chrome local 예시: 자금 구간·달력 데이터 날짜만 활성·선택 성공, 이용현황4개 y좌표25px씩 증가. route 100% width1120/icon32 →150% width1680/icon32. 실제 drag scrollLeft269→569, 상세 선택 안 열림. 설문 controls→status22px, h3→설명12px. 예시는 실제 로그/설문 시트에 업로드 안 함.
- Google Code/HTML readback 전체 문자열 일치, 기존 URL·권한 유지 v23 배포 후 scheduledRefresh 실제 05:03:20~51 KST 정상 완료. 자금 구간 TEST/0.1.0 RUN/AVERAGE 실제1일차T2=20,T3=40,2일차T1=40 carry 확인. 설문 메타 인덱스 최적화 후 v24 최종 배포. 수신v19 호환 유지.
- Drive 설치 code/HTML 같은2ID 갱신. Google Forms 관리자 수정·실제 제출·메일 인증·데이터삭제 테스트 안 함.
- 실제 웹은 자동화 프레임 about:blank 증상 유지. 로컬 예시 표시와 실제 Google 집계/배포를 구분하며 실제 새 화면 최종 가시성은 확인 제한.
- 증거: .harness/local/telemetry-planner-funds-calendar.png, telemetry-planner-survey-spacing.png, telemetry-planner-v24-deployed.png.

## 저장
- 이번 요청의 문서·코드는 파일 저장, 로컬 커밋/push/PR/병합 안 함. 이전 설문 미커밋과 c17f2a1 기준 유지.
