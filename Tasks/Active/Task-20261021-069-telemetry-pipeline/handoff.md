# Handoff

## 지금 상태
- feat/telemetry-pipeline → integration/log-system, dev 12267d7 FF 수신. 기존 기반 커밋 5da374f 이후 변경은 미커밋, 이번 새 commit/push/PR/병합 없음.
- Google 동일 URL: 수신 v19(04:08 KST), 등록 멤버 대시보드 v20(04:10 KST). 게임 0.1.0과 배포 버전은 별개. Drive/시트는 비공개, 30분 트리거 유지.
- 등록 멤버 이메일 인증·인사말, 소유자 전용 멤버/원본 보관/삭제 관리 구현. 사용자 승인으로 대시보드 입구만 모든 사용자, 데이터 RPC는 등록 멤버 확인. 소유자 외 3명은 조회 전용.
- 원본 기본1,000개, 적재 영수증 hash/count 일치 파일만 휴지통 이동. 미적재 보호·영수증 유지. 삭제는 선택 환경+버전 또는 체크한 여러 세션 → 소유자 이메일 OTP → 삭제 문구. TEST+LIVE 동시 삭제 라벨/동작 제거.
- MailApp send-only 추가 권한 사용자 승인 완료. initializeDashboardManagement 실제 실행·멤버4행 확인. 실제 메일 발송/데이터 삭제는 미실행.
- Events V열 gameVersion, 5개 통계 시트 TEST/LIVE·버전별 분리·웹 동일 집계. MPPM 실제 TEST/0.1.0 486사건·런1·공연2 복구. History/008,010,011 참조.
- 런+플레이어 번호 분리·공연 도식/줌·분석 이벤트 필터·세션 검색/선택 분리, 호스트 단독 HTTPS·참가자 디스크 ACK·PC 빌드 설정 자동 포함. History/012-planner-timeline.md.
- 구역 체류/재방문·이동/집결·거래/의료18단계·미수령/미이용·접촉 연결과 이전 검증은 History/001~009. 종료 미관측은 미확정, 치료는 현재 복구 여부로 완료.
- 기획자 안내: Docs/Log-system/operator-guide.md·reading-guide.md·admin-guide.md. 관리 구현/검사는 History/013-dashboard-administration.md.

## 다음 할 일
- Docs/Log-system/remaining-work.md 기준. 통로 막힘/우회는 콘텐츠·판정 기준 미정으로 보류.
- 외줄 발판/지속 손잡기·서버 미도달 실패의 확정 원천 필요. 일반 함정 밖 벽/장식물 전체 충돌은 미연결.
- CODE 다인·재접속·실제 강제 종료/PC 전체 오프라인·물리 입력/마이크/의료·공연 공동 검증, 실제 PC 빌드/다인 호스트 전달은 보류.
- 외부 계정 이메일 인증·실제 삭제의 전체 조작은 미검증. 사용자 새로고침 후 ‘모호성님 안녕하세요’·관리자 메뉴 실제 표시 확인(2026-10-22). 소유자 getDashboard 실행도 정상.
- 이전 Unity 실행이 만든 폰트1·머티리얼3 자동 변경은 승인 없는 복원 없이 보존. 다른 Task/기존 미커밋 작업도 보존.

## 검증·증거
- 기본 check-work/저장 DryRun은 dev 수신77건 구역 오인으로 FAIL. 전체 통과 아님. origin/dev 기준 Scope 및 Fast를 별도로 사용.
- 이전 Unity Compile PASS(up_to_date), 씬 dirty0·전송/설정 EditMode7/7·큐10/10. 이번 서버/문서만 변경하여 Unity/실플레이/두 PC는 SKIPPED.
- 기존 전체 EditMode 목마 점프0.9/1.35 실패 미해결, 전체 PlayMode900초 시간 초과 이력 유지. 기본 verify-unity는 구 CLI 필드로 ENV_BLOCKED였으며 실제 editor_status/recompile_status 별도 확인.
- Scope(origin/dev)/Fast PASS(기존 문서 경고). Node73/73 PASS: 집계·접근 차단·OTP 만료/반복/횟수·원본 보호·삭제 범위/재전송·중단 복구, 타 환경 보호 포함.
- 실제 Google: 멤버4행, Code/HTML readback 일치·메일 scope5개 확인, 기존 URL 수신v19/대시보드v20 배포 완료. getDashboard 04:11:54~56 KST 정상 완료. 실제 메일/삭제를 검증이라고 실행하지 않음.
- Drive 설치 소스 code/HTML/manifest 기존3ID 내용 갱신 완료. 클라우드 화면과 로컬 예시 화면 검증을 구분한다.
- 증거: .harness/local/telemetry-management-v20-deployed.png, telemetry-management-preview.png; 이전 telemetry-final-{cannon,waiting}-20261022.json·telemetry-planner-v18-deployed.png 및 각 History.
