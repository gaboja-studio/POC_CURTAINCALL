# 세션 보관·증분 갱신·참가자 표시 (2026-10-26)

- PM 요청: LIVE30/TEST7일 상세 자동 정리, 세션 요약·기존 통계·설문 보존, 새 기록 계산, 처리 상태, 3·4번 참가자 누락 조사. Unity 검증 제외, 최소 검사·Google 배포까지. commit/push 요청 없음.
- 브랜치 fix/dashboard-refresh-cache. 기존 다른 작업의 변경은 보존했다. 현재 Google 대시보드 v34, 기존 URL/접속 권한·로그 수신 v19 유지.
- 신규 session-store.gs: 환경/버전/런(런 이전은 앱)별 상세 JSON·요약 체크포인트. 중단 복구·마지막 행 이후 읽기·변경 세션만 계산. 기간 정리 전 요약 저장/읽기 확인, 상세 행 이동은 전체 내용 대조. 설문 최소 연결 정보·집계 분모/표본 보존.
- 수신 대기·최근48시간 수신은 정리 유예. 늦게 도착한 정리 완료 세션의 사건은 Events에 남겨 별도 확인 안내. 무제한 용량이나 모든 처리의 증분화를 주장하지 않는다.
- 타임라인: 참가자 명단/표시 사건 분리, 400개 플레이어별 샘플링, 모호하지 않은 networkClientId 매칭, 공연 시작 일차 기준. 시작 인원/기록 확인 인원 표시.
- 실제 LIVE b15c1422… / D1P2 수신 기록을 새 계산기로 조회: 시작4명, 참가자1·2·3·4, 도식16건. 59d9af61…의 일부 과거 공연은 원본 시작2명·슬롯0/1이므로 임의 복구 불가. 원본 적재 대기도 확인됨.

## 최소 검사와 실제 반영

- Node 전체118 PASS; 시간 분할 수정 후 관련37 PASS. 보관경계·통계 보존·변경 세션·정리 실패 복구·늦은 기록·이용률 분모·4인 표시·전체행 대조·전환 중단 복구.
- 최초255세션 전환 완료. 체크포인트 cursor60456에서 검증된 상세 이동 후36611 확인. 원문은 비공개 Drive 상세 파일에 남는다.
- 14:28:31 KST 완료 캐시: LIVE1126건/7런/공연8회(외줄6·대포2), TEST4281건. 묘기 통계 시트의 LIVE 수치·평균·사망과 캐시 일치. LIVE 설문 NIGHT1=18/EXIT=11 유지, 원본 폼/응답 삭제 없음.
- 해당 갱신 IMPORT 단계는 일시 REFRESH_ERROR로 PARTIAL 게시. 별도 재시도14:36:07~14:37:56에서 원본100묶음 정상 적재. 전체 backlog 소진으로 보고하지 않는다.
- 최종14:44:55 점검: 최근 완료14:41:55 KST, refreshing=false/lastError빈값. LIVE1377·TEST4281, 세션259개 중 이번 변경7개만 계산, 기간 정리0개(아직 보관 기간 내). 대기 최소625묶음이며 수동5분/자동30분 갱신으로 계속 처리한다. 최종v34는14:44 배포 확인.
- 전체 doctor/verify-fast: 외부 submodule Git 접근 제한으로 FAIL, 문서 hard0. 대상 경로 diff --check PASS. Unity 코드 변경·검증·빌드 SKIPPED(PM 명시).
- 증거: .harness/local/session-store-tests.txt, session-store-final-targeted.txt, session-store-deployed.png. 배포 증거v34 14:44 KST. 브라우저 자동 새로고침 후 Google iframe about:blank 제약; 실제 Apps Script 완료 캐시·시트 수치 읽기로 확인.

## 운영

- [정책/제약](../../../../Docs/Log-system/session-retention.md), [관리자 안내](../../../../Harness/Project/Guides/Log-system/dashboard-admin.md).
- 첫 전환 이후 원본 묶음 대기는 수동5분/자동30분 갱신으로 이어간다. 누락된 원본을 만들거나 참가자 수를 추측하지 않는다.
