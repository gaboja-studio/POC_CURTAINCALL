# 원본 보관·멤버·안전한 삭제

2026-10-22 사용자 요청. 조회 멤버 4개 지정, 소유자만 관리자. 실제 데이터 삭제·실제 메일 발송을 AI가 실행하지 않는다.

## 구현
- 원본 기본 1,000개. IMPORTED receipt의 hash/count가 일치하는 파일만 오래된 순으로 휴지통 이동(갱신당 최대100개·60초). 미적재·검증 불가 보호. Batches 영수증은 보존, 재POST 시 원본 재생성 없이 ACK.
- 이메일 OTP 접속: 8자리/10분/5회/1회 사용, 발송 간격60초·전역10회/분·무료 잔여 한도 확인. 인증 정보는 cache hash, 접속은 최대6시간. 조회 요청마다 최신 멤버 enabled 확인. 효과적 사용자(owner) fallback 금지. owner만 실제 activeUser 또는 소유자 이메일 인증으로 관리.
- 지정 이름 인사말, 관리자 멤버 추가/수정/비활성화 및 보관 한도 설정. Drive/Sheet 직접 공유 없음.
- 버전 클리어는 선택 환경·버전 기존 사건, 세션 삭제는 현재 환경/버전의 체크된 세션(최대500). 미리보기 → 관리자 이메일 OTP → ‘삭제’ 입력. 서버가 범위·이벤트ID fingerprint를 보관하며 원격 파라미터로 바꿀 수 없음.
- 삭제 규칙을 먼저 영속 기록하고 Events 행을 아래에서부터 제거, 통계 재게시. 실패해도 규칙으로 조회 제외·다음 갱신에 정리 재개. 늦은 재전송 및 휴지통 원본 복원으로 부활 방지. 버전은 확인시각 cutoff로 이후 신규 플레이 허용, 세션은 영구 차단.
- 수신 배포는 dashboardEnabled=false로 GET 차단 유지. 웹 배포만 true. prepare-deployment.cjs가 core/management/server 결합·수신/웹 소스·HTML·manifest 준비.
- MailApp send_mail만 추가, 메일함 읽기 없음. 사용자가 외부 로그인 화면 접근 확대를 별도로 명시 승인(등록 이메일 인증 후에만 조회). 실제 Google 추가 OAuth 승인은 별도.

## 검사
- Node73/73: 기존계약/집계+접근/멤버차단/코드만료·반복·횟수/한도·미적재·해시보호/혼합버전파일·복수세션·재전송·물리삭제 중단 회귀.
- Chrome 예시 관리자 메뉴·보관 한도·멤버 편집·2세션 전체선택 확인. .harness/local/telemetry-management-preview.png.
- Scope(origin/dev)/Fast PASS(기존 경고), Unity 수정/실플레이/두 PC SKIPPED. 이전 자동 에셋4개·기존 미커밋 내용 보존. 커밋/push 없음.

- 후속 사용자 질문 반영: TEST+LIVE 라벨은 혼재가 아니라 초기 양쪽 삭제 범위였다. 화면 조건과 일치하도록 클리어도 선택 환경·버전만 적용, 버튼 라벨 간소화. 타 환경 보호 회귀 갱신.

## 실제 반영 (04:12 KST)
- 사용자 메일 권한 승인 후 initializeDashboardManagement 완료(04:04:24~27), 멤버4행 connector readback. 수신 v19 04:08 / 대시보드 v20 04:10, 기존 URL 유지. 승인된 입구 공개만 적용, 원본/시트 직접 공유 없음.
- Code.gs 수신 모드 및 최종 웹 모드·HTML 로컬 원문 일치 확인. TEST+LIVE 라벨 없음, 선택 환경만 요청. manifest send_mail 포함5scope 확인. 최종 편집기 소스는 웹 모드. Drive 설치소스3개 같은 ID 갱신.
- getDashboard 실제 실행 04:11:54~56 정상 완료. 실제 메일 발송/실제 삭제/외부 계정 접속은 미검증. 에이전트 Chrome은 sandboxFrame about:blank로 화면 확인 불가, 사용자 직접 새로고침 확인 요청. .harness/local/telemetry-management-v20-deployed.png.
