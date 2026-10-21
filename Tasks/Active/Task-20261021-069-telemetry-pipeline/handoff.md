# Handoff

## 지금 상태
- feat/telemetry-pipeline → dev. PM 단일 Task 직접 제출 승인(meta). dev84517ba(PR207 공연 수정)를 76eb3e8로 충돌 없이 병합. b6ae6f0 로그·설문·문서 로컬 저장. 레그돌 후속은 미커밋. PR 미등록, dev 병합 없음.
- Google 동일 URL: 수신v19·등록 멤버 대시보드v24. Drive/시트 비공개, 수동/30분 갱신 유지. 등록 멤버4명·메일 인증/관리자 보관·삭제 구현, 실제 삭제/외부 계정 조작은 미검증.
- 호스트 단독 HTTPS·참가자 디스크 ACK·플레이어 번호/버전/TEST·LIVE 분리. 시간18단계·공연 좌표 도식·분석 필터·자금DAY/BREAK·날짜/검색/select·설문 통계 구현. 이전 세부 증거는 History/001~015.
- 독립 LOGS/SURVEY_010 태그, 0.1.0 밤1/ESC 나가기 최초 브라우저·캐시·응답 시트 연결. 새 빌드 기본off/off. 폼 관리자는 PM만 수정. 설문-only는 플레이 로그 없이 응답만 집계.
- [기획자 안내](../../../../Harness/Project/Guides/Log-system/index.md): Windows/Mac 검증·빌드, 관리자/팀원 접속, 통계 해석·캐시·버전 변경.
- Windows64 Development+두 태그 빌드 성공. 최종: Builds/Telemetry-0.1.0-Windows-TEST-ragdoll/CURTAINCALL.exe. 레그돌 긴급 수정 포함, 이전 final 출력과 구분.
- 빌드 차단 외부 에셋3파일의 UNITY_EDITOR 경계만 PM 승인 수정. private PR2 main 병합, gitlink1a619e83은 origin/main 조상. 외부 원문 공개 반출 없음.
- 승인된 빌드 자동 설정/폰트6개·에디터 태그off/off 복원. 기존 폰트1·머티리얼3 SHA256 동일 보존·커밋 제외. 테스트 SurveyCache2개는 보존, 재검증 초기화는 pc-files.md.

## Verification
- 구역 검사: PASS (origin/dev, PM; 기존 자동 에셋4개 제출 제외).
- 1 문서/규칙: PASS (기존 크기 WARN만, hard0).
- 2 컴파일: PASS (최신 dev 병합 후 completed/failed=false/errors0). 최종 복원 후 저장 직전 up_to_date 재확인.
- 3 테스트: PASS (이번 Node84/84). 캐시 수정 EditMode8/8 PASS(이번). Start bootstrap PlayMode1/1은 동일 연결 코드의 직전 증거(History/014). 전체 Unity 테스트는 최소 검사 요청으로 SKIPPED, 기존 전체 목마 점프 실패/PlayMode 시간 초과 미해결.
- 4 실행 로그: PASS (Windows exe 개발 메뉴 밤1→설문 자동 열기, PM 직접 ESC 나가기→종료 설문). 두 연결 코드 TEST/0.1.0/동일run/slot0 확인. 응답 제출 없음. Drive ACK 및 Events1585 RUN_STARTED 적재 확인.
- 최신 dev+캐시 수정 최종 빌드: PASS (build_2c8ce100d5d9, errors0/warnings8, 24.528초). 새 exe 실제 실행·Drive ACK·내장 태그 확인. PM 밤1→ESC 재실행에서 설문 재열림 없음 확인. 일반 첫날 완주/두PC와 구분.
- 레그돌 후속: 외줄 사망 후 ReleaseControls의 BalanceReset이 모델 복구하는 결함 재현·수정. 두 실제 공연 씬 PlayMode 회귀 PASS(외줄6.15초·대포6.79초, 공개 API 합성/중력 실측), 반복 사망·전체절단·균형추락·정상부활 확인. Windows 재빌드 PASS(errors0/warnings12/42.4초)·Drive ACK, PM 사망 화면 확인 대기. [History/017](History/017-death-ragdoll.md).
- 증거: .harness/local/telemetry-windows-build-{latest,dev}.json, telemetry-windows-{player,dev-player}.log, telemetry-submit-node.txt. 상세 [History/016](History/016-windows-build-submission.md).

## 남은 확인
- [남은 작업](../../../../Docs/Log-system/remaining-work.md): 통로 막힘/우회·외줄 발판/지속 손잡기 의미는 기획 보류. 새 원천이 필요한 미도달 실패/함정 밖 충돌 미연결.
- CODE 두PC·재접속·강제 종료/PC 전체 오프라인·정상 공연/구매/의료 공동 플레이, Mac 빌드 보류.
- 실제 설문 답변→시트/대시보드 수치 대조·다른 계정 인증·실제 삭제 미실행. 응답0은 표시 검증이며 실제 응답 검증이 아님.
- Task는 공동 검증 완료/done으로 처리하지 않는다. PR 생성 후 링크·Status·todo 기록.
