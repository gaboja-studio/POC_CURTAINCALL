# Handoff

## 지금 상태
- 2026-10-26 저장·dev 반영 요청: 현재 로그·설문·버전 변경만 제출한다. 아래 과거 단계의 미배포/미커밋 표기는 당시 이력이며, 최신 배포·빌드 상태는 첫 빌드 인계 행과 History/021 기준이다. 다른 Task 문서 변경은 이번 제출에서 제외한다.
- 2026-10-26 M2 빌드 인계: Windows0.2.0 Development+LOGS/설문끔, Dev Mode 실행 바로가기 생성. 빌드 오류0·수집 시작 확인. Google 수신v35/대시보드v36 기존 URL 반영 완료. 직접 플레이는 PM 진행. [빌드 위치·증거](History/021-m2-windows-build.md).
- 2026-10-26 M2 후속: 게임 0.2.0, 로그/설문별 정확 버전·패치 범위·전체 선택, 밤 정비/퇴장 폼·회차 설정 화면 추가. PM은 새 빌드 적용 방식 선택. M1은 0.1.x, 이 PC 로그 켬·설문 끔. [안내](../../../../Harness/Project/Guides/Log-system/versioning.md). Google 재배포 전으로 새 계약 수집 테스트 보류.
- 2026-10-26 참가자 누락 수정: 최신 dev 7d87630 FF 반영, 기존 변경 백업·복원. 공식 명단과 호스트의 플레이어별 시작/구간/진행/도착 관측, 타임라인 미수집 구분·중요 사건 보존. 현재 브랜치 로컬 수정이며 Google 재배포·빌드·commit/push 없음. [검증·적용 순서](History/019-host-timeline.md).
- 2026-10-23 사용자 LIVE 빌드 후속: BaseGameSettings의 프레임 제한(기본 켬·60FPS), 자동 FrameRateController 추가. 해제 시 targetFrameRate=-1, 실행 중 VSync=0, Play 종료 시 이전 엔진값 복원. 씬 저장·빌드·커밋·push 없음. 사용법은 service-build.md.
- 2026-10-26 후속: fix/dashboard-refresh-cache에서 세션 요약·LIVE30/TEST7 상세 보관·새 세션 기록만 재계산·처리 상태·4인 타임라인 수정, Google 동일 URL v34 배포. 최초255세션 전환, 시트/완료 캐시 일치·설문 보존 확인. 이번 Unity 변경/검증 없음, commit/push 요청 없음. 기존 원본 backlog는 다음 갱신으로 계속. [현재 증거/제약](History/018-session-retention.md).
- feat/telemetry-pipeline → dev. PM 단일 Task 직접 제출 승인(meta). dev84517ba(PR207 공연 수정)를 76eb3e8로 충돌 없이 병합. b6ae6f0 로그·설문·문서 로컬 저장. 레그돌 후속034a45e 저장. PR208 제출, dev 병합 없음.
- Google 동일 URL: 수신v19·등록 멤버 대시보드v24. Drive/시트 비공개, 수동/30분 갱신 유지. 등록 멤버4명·메일 인증/관리자 보관·삭제 구현, 실제 삭제/외부 계정 조작은 미검증.
- 호스트 단독 HTTPS·참가자 디스크 ACK·플레이어 번호/버전/TEST·LIVE 분리. 시간18단계·공연 좌표 도식·분석 필터·자금DAY/BREAK·날짜/검색/select·설문 통계 구현. 이전 세부 증거는 History/001~015.
- 독립 LOGS/SURVEY_010 태그, 0.1.0 밤1/ESC 나가기 최초 브라우저·캐시·응답 시트 연결. 새 빌드 기본off/off. 폼 관리자는 PM만 수정. 설문-only는 플레이 로그 없이 응답만 집계.
- [기획자 안내](../../../../Harness/Project/Guides/Log-system/index.md): Windows/Mac 검증·빌드, 관리자/팀원 접속, 통계 해석·캐시·버전 변경.
- Windows64 Development+두 태그 빌드 성공. 최종: Builds/Telemetry-0.1.0-Windows-TEST-ragdoll/CURTAINCALL.exe. 레그돌 긴급 수정 포함, 이전 final 출력과 구분.
- 빌드 차단 외부 에셋3파일의 UNITY_EDITOR 경계만 PM 승인 수정. private PR2 main 병합, gitlink1a619e83은 origin/main 조상. 외부 원문 공개 반출 없음.
- 승인된 빌드 자동 설정/폰트6개·에디터 태그off/off 복원. 기존 폰트1·머티리얼3 SHA256 동일 보존·커밋 제외. 테스트 SurveyCache2개는 보존, 재검증 초기화는 pc-files.md.

## Verification
- 참가자 누락 후속: 컴파일 PASS, EditMode EventContractTests 10/10, 단일 외줄 PlayMode 1/1(13.49초), Node 71/71. 공식 명단 4인·일차별 인원 분리·중복·중요 사건 보존 회귀 포함. 실제 원격 PC/대포 플레이와 구분한다. 작업 폴더 전체 구역 검사는 이전부터 존재한 다른 Task 문서 이동으로 FAIL이며, 이번 Unity 수정 구역 위반은 없다.
- 2026-10-23 FPS 후속 별도 검사: 문서 PASS(기존 크기 WARN), 컴파일 PASS(up_to_date), PlayMode FrameRateSettingsTests 1/1 PASS(2.14초: 부트스트랩·120 변경·해제·60 복원·씬 이동 유지·0 입력 방어). 최초 테스트는 DontSave 객체를 일반 검색해 실패했고 인스턴스 확인으로 수정 후 통과. 빌드 실행·실측 FPS는 미검증. check-work 전체는 작업 전부터 있던 ProjectSettings.asset·UnityConnectSettings.asset의 다른 Task 구역 변경 때문에 FAIL; 기존 변경은 보존. 아래 자동 검사 행은 전체 작업 폴더 결과이며 이번 코드의 컴파일 결과와 구분.
- 구역 검사: PASS — 2026-10-26 제출 대상 58파일. 다른 Task 문서179경로는 별도 stash로 보존·제출 제외. 이전 전체 작업 폴더 FAIL과 구분.
- 1 문서/규칙: PASS — Fast OK, hard 0·기존 문서 길이 WARN22.
- 2 컴파일: PASS — Editor ready, dirty scene0, up_to_date. Windows0.2.0 Development 빌드 오류0.
- 3 테스트: PASS — 기존 최소 회귀 Node84/84, SurveyPolicy20/20·EventContract10/10, SurveyBootstrap4/4·호스트 관측1/1. 저장 단계에서는 코드 변화가 없어 반복하지 않음. 두PC/실제 설문 응답 보류.
- 4 실행 로그: PASS (Windows exe 개발 메뉴 밤1→설문 자동 열기, PM 직접 ESC 나가기→종료 설문). 두 연결 코드 TEST/0.1.0/동일run/slot0 확인. 응답 제출 없음. Drive ACK 및 Events1585 RUN_STARTED 적재 확인.
- 최신 dev+캐시 수정 최종 빌드: PASS (build_2c8ce100d5d9, errors0/warnings8, 24.528초). 새 exe 실제 실행·Drive ACK·내장 태그 확인. PM 밤1→ESC 재실행에서 설문 재열림 없음 확인. 일반 첫날 완주/두PC와 구분.
- 레그돌 후속: 외줄 사망 후 ReleaseControls의 BalanceReset이 모델 복구하는 결함 재현·수정. 두 실제 공연 씬 PlayMode 회귀 PASS(외줄6.15초·대포6.79초, 공개 API 합성/중력 실측), 반복 사망·전체절단·균형추락·정상부활 확인. Windows 재빌드 PASS(errors0/warnings12/42.4초)·Drive ACK, PM이 실제 최종 빌드의 두 공연 사망 레그돌 정상 동작 확인. [History/017](History/017-death-ragdoll.md).
- 증거: .harness/local/telemetry-windows-build-{latest,dev}.json, telemetry-windows-{player,dev-player}.log, telemetry-submit-node.txt. 상세 [History/016](History/016-windows-build-submission.md).

## 남은 확인
- [남은 작업](../../../../Docs/Log-system/remaining-work.md): 통로 막힘/우회·외줄 발판/지속 손잡기 의미는 기획 보류. 새 원천이 필요한 미도달 실패/함정 밖 충돌 미연결.
- CODE 두PC·재접속·강제 종료/PC 전체 오프라인·정상 공연/구매/의료 공동 플레이, Mac 빌드 보류.
- 실제 설문 답변→시트/대시보드 수치 대조·다른 계정 인증·실제 삭제 미실행. 응답0은 표시 검증이며 실제 응답 검증이 아님.
- Task는 공동 검증 완료/done으로 처리하지 않는다. PR 생성 후 링크·Status·todo 기록.

- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/208
