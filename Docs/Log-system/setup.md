# 운영·설치

기획자는 먼저 [로그·대시보드 사용 안내](operator-guide.md)와 [통계 읽는 법](reading-guide.md)을 참고한다. 아래는 설치·개발 담당자용이다.

## 실제 Google 리소스
- 계정: ourhosu@gmail.com
- [상위 로그 폴더](https://drive.google.com/drive/folders/1zLstjPM44i_aWO6hjvFfBcOSXdClrLAX)
- [원본 JSON](https://drive.google.com/drive/folders/1MEeWD5UNdKcypZ6jAhXfaMkP6i84WAi5)
- [로그 시트](https://docs.google.com/spreadsheets/d/1KCLPn1tEW5qsK16jcw8swZ75IeN6p6lG_82zRUOg7I8/edit)
- [Apps Script 프로젝트](https://script.google.com/home/projects/1V4KUY5s1YNPdyGBJj_wr4XMjYLCaReUJUQ-5oG8acx9SDBOtCgYg15TO/edit)
- 본인 전용 대시보드(v14 실패·운반·미수령·의료 미이용 추가, 버전 분리·시간·공연 도식 유지): https://script.google.com/macros/s/AKfycby1UfzhOaNl9ycjKYATPF-b32AkSskzupKQ--Zxj3Z_eIKraOqy9Aw1PYYLkqKewJ_wqw/exec
- 수신 전용 v13(새 접촉·종료 계약, v14 집계와 호환): https://script.google.com/macros/s/AKfycbyBQxQU6XRZXS1qPEs11K8U_i8cwkJEi68-yHgIL6XHe9yCYzyXf3MFjsw6m2cYKEKD3A/exec
- 2026-10-21 ccScheduledRefresh_ 30분 트리거 1개·실행 이력·오류율 0% 확인. 본인 전용 배포를 v2로 갱신해 빈 화면 해결.
- TEST 필터에서 실제 이벤트 159건·v4 통계 그룹/탭/타임라인 확인. 기본 LIVE 필터는 현재 데이터 없음. [화면·집계 기준](dashboard-design.md).

## 배포 순서
1. Scripts/Telemetry/core.gs·server.gs를 Apps Script 스크립트 파일로 추가한다.
2. dashboard.html을 이름 dashboard인 HTML 파일로 추가한다.
3. appsscript.json의 시간대·서비스 범위를 적용한다.
4. 프로젝트 설정 → 스크립트 속성에 INGEST_TOKEN을 충분히 긴 무작위 값으로 직접 설정한다.
5. 스프레드시트·Drive·트리거·이메일 확인 접근을 승인한다. 이 앱은 결제 API를 사용하지 않는다.
6. 대시보드 배포: 웹 앱, 본인만 접근, 본인으로 실행. doGet·RPC는 ownerEmail을 다시 확인한다.
7. 수신 배포: 웹 앱 POST 용도. 게임이 Google 로그인 없이 호출하려면 요청 도달을 허용해야 한다.
8. 수신 코드의 token 확인과 schema 제한은 필수. 인증 실패는 Drive/Sheets를 수정하지 않는다.
9. installRefreshTrigger를 소유 계정으로 한 번 실행한다. 중복 설치하지 않는다.
10. 대시보드 웹 주소를 시트 운영 상태 B7에 기록하고 실제 빈 상태·갱신 성공을 확인한다.

## Unity 설정
- 게임 버전은 Player Settings의 Version을 사용한다(현재 이미 0.1.0). 다음 빌드의 이 값만 변경하면 새 로그와 대시보드 선택지가 자동 구분된다. 배포 v12는 서버 코드 버전이며 게임 0.1.0과 별개다. 과거 대기 파일의 버전은 바꾸지 않는다.
- 원격 전송은 설정이 없으면 비활성. 현재 Windows 사용자는 수신 URL·token 직접 설정 후 자동 전송 검증 완료.
- 게임 프로세스 환경 변수 CURTAINCALL_TELEMETRY_URL: 수신 배포의 https://script.google.com/macros/s/.../exec.
- CURTAINCALL_TELEMETRY_TOKEN: Apps Script INGEST_TOKEN과 같은 값. 문서·커밋·콘솔에 넣지 않는다.
- 설정 변경 뒤에는 Play를 다시 시작한다. Windows는 프로세스 값이 없으면 사용자 환경 변수도 읽는다.
- Windows 직접 설정: pwsh -File Scripts/Telemetry/configure-client.ps1 -Endpoint <수신 웹 앱 /exec 주소>
- 또는 Scripts/Telemetry/configure-client.cmd 더블클릭(현재 수신 주소 포함).
- 이 스크립트의 보안 입력창에 token을 직접 입력한다. 채팅·명령줄 인자에 token을 넣지 않는다.
- Editor·Development Build는 TEST. 정식 빌드는 LIVE로 분리한다.
- 로컬: Application.persistentDataPath/TelemetryPipeline. active.json과 UUID.json pending 파일.
- LastError로 저장/전송 상태 확인. HTTP 200만으로 성공 처리하지 않고 batchId·SHA256·건수 검사.
- 강제 종료는 GAME_EXITED를 보장하지 못한다. 마지막 디스크 배치와 재실행 복구로 결손을 줄인다.

## 공개 범위
- 원본 Drive·시트를 링크 공개로 바꾸지 않는다. 상위 폴더 상속 권한은 운영자가 확인한다.
- 요청 token은 테스트용 클라이언트 수신 제한이며 서버측 신원·치트 방지 증명이 아니다.
- Apps Script 소유자 실행은 Drive/Sheets 권한을 사용한다. 배포 승인 시 정확한 범위를 확인한다.
- 유료 Google Cloud·BigQuery·Looker Pro·외부 SaaS를 생성하지 않는다.

## 확인 명령
- Node: node --test Scripts/Telemetry/core.test.cjs Scripts/Telemetry/server.test.cjs Scripts/Telemetry/dashboard.test.cjs
- 로컬 예시 화면: node Scripts/Telemetry/preview-dashboard.cjs → http://127.0.0.1:8787 (예시 데이터는 TEST 선택).
- Unity: pwsh -File Scripts/check-work.ps1
- PlayMode: pwsh -File Scripts/verify-unity.ps1 -ProjectPath . -RunTests -Mode PlayMode
- 완료 조건: TEST 런 → Drive JSON → Events → 수동 갱신 → 화면 타임라인 실제 값 일치.

## 무료 서비스 제한
- [Apps Script 할당량](https://developers.google.com/apps-script/guides/services/quotas)
- [웹 앱 배포](https://developers.google.com/apps-script/guides/web)
- [ContentService 리디렉션](https://developers.google.com/apps-script/guides/content)
- JSON 응답은 googleusercontent.com으로 리디렉션될 수 있다. 실제 UnityWebRequest 왕복으로 확인한다.
- 50,000 이벤트·100배치/갱신 상한. 많은 데이터는 원본 보존한 채 별도 보관·집계 분할을 계획한다.

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
