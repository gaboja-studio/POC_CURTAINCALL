# 운영·설치

기획자는 먼저 [로그·대시보드 사용 안내](operator-guide.md)와 [통계 읽는 법](reading-guide.md)을 참고한다. 아래는 설치·개발 담당자용이다.

## 실제 Google 리소스
- 계정: ourhosu@gmail.com
- [상위 로그 폴더](https://drive.google.com/drive/folders/1zLstjPM44i_aWO6hjvFfBcOSXdClrLAX)
- [원본 JSON](https://drive.google.com/drive/folders/1MEeWD5UNdKcypZ6jAhXfaMkP6i84WAi5)
- [로그 시트](https://docs.google.com/spreadsheets/d/1KCLPn1tEW5qsK16jcw8swZ75IeN6p6lG_82zRUOg7I8/edit)
- [Apps Script 프로젝트](https://script.google.com/home/projects/1V4KUY5s1YNPdyGBJj_wr4XMjYLCaReUJUQ-5oG8acx9SDBOtCgYg15TO/edit)
- 등록 멤버 대시보드(v36 M2 호스트 타임라인·설문 버전/회차·기존 보관/캐시 유지): https://script.google.com/macros/s/AKfycby1UfzhOaNl9ycjKYATPF-b32AkSskzupKQ--Zxj3Z_eIKraOqy9Aw1PYYLkqKewJ_wqw/exec
- [갱신·캐시 운영](refresh-cache.md): 비공개 Drive 캐시, 실패 복구와 소유자 진단 방법.
- 수신 전용 v35(M2 호스트 명단·도착 계약·기존 인증/중복 방지 유지): https://script.google.com/macros/s/AKfycbyBQxQU6XRZXS1qPEs11K8U_i8cwkJEi68-yHgIL6XHe9yCYzyXf3MFjsw6m2cYKEKD3A/exec
- 2026-10-21 ccScheduledRefresh_ 30분 트리거 1개·실행 이력·오류율 0% 확인. 본인 전용 배포를 v2로 갱신해 빈 화면 해결.
- TEST 필터에서 실제 이벤트 159건·v4 통계 그룹/탭/타임라인 확인. 기본 LIVE 필터는 현재 데이터 없음. [화면·집계 기준](dashboard-design.md).

## 배포 순서
1. node Scripts/Telemetry/prepare-deployment.cjs 실행. core.gs·surveys-core.gs·management.gs·surveys-server.gs·server.gs·refresh-cache.gs를 결합한 수신/웹용 소스를 .harness/local에 만든다.
2. dashboard.html을 이름 dashboard인 HTML 파일로 추가한다.
3. appsscript.json의 시간대·서비스 범위를 적용한다.
4. 프로젝트 설정 → 스크립트 속성에 INGEST_TOKEN을 충분히 긴 무작위 값으로 직접 설정한다.
5. 스프레드시트·Drive·트리거·이메일 확인·메일 발송 접근 승인. initializeDashboardManagement를 소유자가 한 번 실행해 멤버/삭제 관리 시트를 준비한다. 결제 API는 없다.
6. 대시보드 배포: telemetry-dashboard-code.txt(dashboardEnabled=true), 본인으로 실행·모든 사용자. 공개 입구 뒤 RPC마다 등록 이메일 인증을 확인하며 관리자 기능은 소유자만 가능하다.
7. 수신 배포: telemetry-ingest-code.txt(dashboardEnabled=false), 본인으로 실행·모든 사용자. GET은 차단하고 POST token을 검증한다. 두 기존 배포를 각각 새 버전으로 갱신한 뒤 편집기는 웹용 소스로 유지한다.
8. 수신 코드의 token 확인과 schema 제한은 필수. 인증 실패는 Drive/Sheets를 수정하지 않는다.
9. installRefreshTrigger를 소유 계정으로 한 번 실행한다. 중복 설치하지 않는다.
10. 대시보드 웹 주소를 시트 운영 상태 B7에 기록한다. 소유자가 scheduledRefresh를 한 번 실행해 첫 캐시를 만든 뒤 checkDashboardCache로 조회를 확인한다. 일반 접속에서는 첫 집계도 자동으로 시작하지 않는다.

## Unity 설정
- 새 빌드/Editor 실행은 PlaytestBuildTags manifest의 LOGS 태그가 있을 때만 기록·재전송한다. 설문 태그는 독립이며 설정된 버전 범위에 맞는 설문만 가능. 빌드 담당자 메뉴·최초 열기 캐시는 [설문 안내](survey-guide.md). 기존 실행 파일에는 소급 적용되지 않는다.
- 게임 버전은 Player Settings의 Version을 사용한다(현재 0.2.0). 다음 빌드의 이 값만 변경하면 새 로그와 대시보드 선택지가 자동 구분된다. 배포 v12는 서버 코드 버전이며 게임 0.1.0과 별개다. 과거 대기 파일의 버전은 바꾸지 않는다.
- 원격 전송은 설정이 없으면 비활성. 현재 Windows 사용자는 수신 URL·token 직접 설정 후 자동 전송 검증 완료.
- 게임 프로세스 환경 변수 CURTAINCALL_TELEMETRY_URL: 수신 배포의 https://script.google.com/macros/s/.../exec.
- CURTAINCALL_TELEMETRY_TOKEN: Apps Script INGEST_TOKEN과 같은 값. 문서·커밋·콘솔에 넣지 않는다.
- 설정 변경 뒤에는 Play를 다시 시작한다. Windows는 프로세스 값이 없으면 사용자 환경 변수도 읽는다.
- Windows 직접 설정: pwsh -File Scripts/Telemetry/configure-client.ps1 -Endpoint <수신 웹 앱 /exec 주소>
- 또는 Scripts/Telemetry/configure-client.cmd 더블클릭(현재 수신 주소 포함).
- 이 스크립트의 보안 입력창에 token을 직접 입력한다. 채팅·명령줄 인자에 token을 넣지 않는다.
- Editor·Development Build는 TEST. 정식 빌드는 LIVE로 분리한다.
- 로컬: Application.persistentDataPath/TelemetryPipeline. active.json과 UUID.json pending 파일.
- MPPM: 공유 persistentDataPath 충돌을 피하려고 보조 프로젝트의 dataPath 해시별 clients 하위 큐·설치 ID를 사용한다. 메인 기존 큐는 유지한다. 상태 조회: PendingBatchCount·LastUploadAt·LastUploadError; Editor/개발 빌드는 Console [Telemetry]에서 대기/Drive ACK 확인.
- 통계 시트 v15: 5개 요약 시트에 gameVersion·environment 필터. TEST/LIVE 모두 집계, 과거 무버전은 UNVERSIONED. 자금 일계 mode=AVERAGE는 대시보드 기본 평균, RUN+runId는 개별 런. balance·samples·carried는 웹 그래프와 같은 원천/함수다. 평균과 개별 런 행은 합산하지 않는다.
- LastError로 저장/전송 상태 확인. HTTP 200만으로 성공 처리하지 않고 batchId·SHA256·건수 검사.
- 강제 종료는 GAME_EXITED를 보장하지 못한다. 마지막 디스크 배치와 재실행 복구로 결손을 줄인다.

## 공개 범위
- 원본 Drive·시트를 링크 공개로 바꾸지 않는다. 상위 폴더 상속 권한은 운영자가 확인한다.
- 요청 token은 테스트용 클라이언트 수신 제한이며 서버측 신원·치트 방지 증명이 아니다.
- Apps Script 소유자 실행은 Drive/Sheets 권한을 사용한다. 배포 승인 시 정확한 범위를 확인한다.
- 유료 Google Cloud·BigQuery·Looker Pro·외부 SaaS를 생성하지 않는다.

## 확인 명령
- Node: node --test Scripts/Telemetry/core.test.cjs Scripts/Telemetry/server.test.cjs Scripts/Telemetry/dashboard.test.cjs Scripts/Telemetry/management.test.cjs Scripts/Telemetry/surveys.test.cjs
- 로컬 예시 화면: node Scripts/Telemetry/preview-dashboard.cjs → http://127.0.0.1:8787 (예시 데이터는 TEST 선택).
- Unity: pwsh -File Scripts/check-work.ps1
- PlayMode: pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -RunTests -Mode PlayMode
- 완료 조건: TEST 런 → Drive JSON → Events → 수동 갱신 → 화면 타임라인 실제 값 일치.

## 무료 서비스 제한
- [Apps Script 할당량](https://developers.google.com/apps-script/guides/services/quotas)
- [웹 앱 배포](https://developers.google.com/apps-script/guides/web)
- [ContentService 리디렉션](https://developers.google.com/apps-script/guides/content)
- JSON 응답은 googleusercontent.com으로 리디렉션될 수 있다. 실제 UnityWebRequest 왕복으로 확인한다.
- Events 적재 안전 상한 200,000건·100배치/갱신. 50,000행 이상에서는 Drive 저장본과 전체 내용이 일치한 처리 행만 옮긴다. LIVE30/TEST7일 후 상세를 정리해도 세션 요약·설문은 남는다. 대시보드 수집 이벤트는 키·버튼·마이크 입력을 제외한 수다. [보관·증분 처리 기준](session-retention.md).

## 현재 확인된 실제 데이터 경로
- Start Play에서 GAME_STARTED/SCENE_ENTERED/TITLE_ENTERED를 디스크 저장했고, Editor 종료로 GAME_EXITED도 기록했다.
- Google Drive connector로 TEST 원본을 보관 후 Apps Script scheduledRefresh로 실제 Events/Batches 적재 확인.
- 이후 실제 UnityWebRequest로 시작 로그 3건 자동 전송·서버 ACK·로컬 대기 파일 0개 확인.
- 종료 로그 1건은 다음 Play 시작에서 자동 재전송되어 Sheets에 적재됨. 총 5배치·TEST 11건 확인.
- 마지막 Play 종료 로그는 로컬에 보존되어 다음 실행에서 전송될 수 있다. 종료 직전 HTTP 완료를 보장하지 않는다.
- 대시보드 TEST 필터·타임라인·수동 갱신 확인. 호스트+참가자 및 상세 공간 이벤트 검증은 남음.
- 19:09 KST 상세 LAN 실행 로그를 포함한 TEST 33건·즉시 갱신 12배치 확인. 이후 추가 검사 로그도 TEST에 누적된다.
- 어댑터 수집 범위는 coverage.md 참조. v3는 미제공 section/target 상태의 명시적 null을 허용하며 기존 배치도 호환한다.
- MPPM/같은 PC 복수 실행의 persistentDataPath 충돌은 별도 확인 필요. 현재 4인 검증 완료로 보고하지 않는다.
- 21:05 KST v5: TEST 240건·침실/소품실 재방문 각 2회·체류 표시, 실제 ACK pending 0 확인. 합성 PlayMode 구역/운반 데이터이며 물리 입력 플레이 검증과 구분한다.
- v6 배포(22:43 KST)·기존 수신 v3 호환, 공연 좌표 추가 테스트 ACK pending 0·TEST 259건 적재 확인. 과거 좌표 미수집은 도식 아래 접힌 목록에서 확인하며 실제 이동 경로를 추정하지 않는다.
- v8(23:48 KST) 본인 전용 동일 URL에 이동·집결 평균/의료 상세 배포. 수신 v3·OAuth·토큰·30분 트리거 변경 없음. 신규 상세 TEST 배치 자동 재전송 pending 0을 확인했으며 이후 최종 회귀/종료의 새 배치는 다음 실행에 재전송된다.

## 호스트 전송·PC 빌드 (2026-10-22)
- 참가자는 원본 배치를 NGO ReliableFragmentedSequenced로 호스트에 전달한다. 호스트 디스크 저장 후 ID/hash/count 확인 응답, 이후 호스트만 HTTPS 전송. 원래 eventId/appSessionId/sequence/시계를 유지하므로 Google 기존 중복 제거가 적용된다.
- 참가자 미전달 원본은 자기 큐에 보존, 같은 환경의 다음 방 접속 때 전달. 호스트 수신 원본은 Google ACK까지 호스트에 보존. 전송권은 현재 호스트 또는 호스트였던 앱만, 현재 참가자는 제외한다. 아직 방을 만든 적 없는 앱은 큐만 보관한다.
- TelemetryBuildProcessor가 빌드 담당자의 환경 설정을 Library/CurtainCallTelemetryBuild에만 생성하고 BuildPlayerContext로 StreamingAssets에 포함한다. Assets/Git에는 비밀값을 만들지 않는다. 누락 시 BuildFailedException. PC 실행은 내장 설정 우선, 에디터만 환경 설정을 사용한다.
- 분산 클라이언트의 수신용 token은 배포 파일에서 추출 가능하다. Google 계정 OAuth/Drive 접근 권한이 아니며 비공개 데이터 조회 권한을 부여하지 않는다. 원격 로그의 부정 입력을 완전히 방지하는 인증으로 취급하지 않는다.
- Windows64 검증 빌드 생성·실제 exe Drive ACK·Events 런 적재·밤1/ESC 설문 브라우저를 확인했다. 다인 전달 타이밍·실제 설문 제출·Mac 실행은 보류. 빌드는 Development(TEST)이며 LIVE 배포 전체 검증과 구분한다.

- 빌드 설정 포함 API 근거: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.BuildPlayerContext.html
