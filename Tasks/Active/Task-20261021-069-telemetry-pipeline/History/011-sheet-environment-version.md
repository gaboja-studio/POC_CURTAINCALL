# 통계 시트 환경·버전 분리

2026-10-22 사용자 요청. LIVE 전용 5개 요약 시트를 TEST/LIVE·gameVersion별로 모두 게시하고 웹과 같은 조건의 수치를 맞춘다.

## 변경

- ccPublish_가 TEST/LIVE 각각 관측 버전과 UNVERSIONED를 TelemetryCore.aggregate로 집계한다. 기존 열 순서·gameVersion 위치는 유지하고 environment를 뒤에 추가한다.
- 자금 일계는 dashboardView의 동일 ledger 정렬·관측 잔액 이월을 사용한다. 기존 거래 열 뒤에 balance/samples/carried/mode 추가. RUN은 런별, AVERAGE는 웹 기본 평균이다. 두 행 종류를 합산하지 않는다.
- 헤더 필터·1행 고정·새 열 메모 적용. 재갱신 시 기존 필터 조건 보존, 새 열 너비 130px로 필터 아이콘/버전 문자열 잘림 보완.
- Events·Batches·원본 JSON 및 과거 누락 버전은 수정하지 않는다. 수신 배포 v13·기존 권한/주소·30분 트리거 유지.

## 최소 검증

- Node 57/57. 신규 회귀는 TEST/LIVE × 0.1.0/0.2.0/UNVERSIONED의 4개 통계와 평균/개별 런 자금 일치·반복 게시 시 중복 없음 확인. 열 너비 보완 후 서버 전용 10/10만 재실행.
- 실제 Google Events 1,500개와 통계 5개 시트를 같은 ccDashboard_ 집계·자금 projection과 대조 PASS. Google 부동소수 반올림 오차만 허용(상대 1e-10), 난이도 텍스트/숫자는 명시 변환. 빈칸을 0으로 바꾸지 않음.
- TEST/0.1.0: 사건486·런1·완료공연2. 외줄/대포 각 1회, 실패 각 1회. 자금은 1·2일차 모두 잔액40, 2일차는 관측된 도달 일차의 이월. TEST/UNVERSIONED 사건1014·런25·완료공연10. LIVE/0.1.0은 미수집(가짜 행 미생성).
- Google 편집기 scheduledRefresh 완료·5개 실제 시트 화면 확인. 본인 전용 같은 URL v16(03:26 KST) 배포 완료. Drive code.txt 같은 ID로 최종 소스 갱신. 브라우저 대시보드 재로드는 기존처럼 프레임 blank여서 새 화면 수치 검사는 실제 시트/동일 집계 함수 대조로 수행했다.
- Unity 코드 수정·컴파일·플레이·두 PC 검사는 이번 범위에서 SKIPPED. 이전 MPPM 수정은 미커밋 그대로 보존. 이번 커밋/push/PR 없음.

## 증거

- .harness/local/telemetry-summary-parity-proof.json, telemetry-summary-parity-input.json, verify-summary-parity.cjs.
- .harness/local/telemetry-code-v16.txt. 읽는 법은 Docs/Log-system/reading-guide.md 7절.
- .harness/local/telemetry-sheet-env-version.png: gameVersion/environment/balance/mode 및 필터 실제 표시.
