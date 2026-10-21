# Windows 빌드·로그/설문 최소 실행 확인·제출

2026-10-22 PM: 전체 변경 커밋, Windows 빌드 생성과 실제 실행 파일의 로그/설문 최소 검사 통과 시 dev 제출. 실제 두PC는 보류. 단일 Task dev PR 예외를 meta에 선언, 원격 dev는 12267d7 확인.

## 빌드 준비
- Unity6000.3.25f1, Windows64, gameVersion0.1.0, Start 씬 clean/stopped. Pipeline 재시작 사용자 수행 후 ready. User 전송설정 유효(bool만 조회), 태그 기존off/off를 빌드에만 on/on, Development+DetailedBuildReport.
- CLI beta13→14 업데이트 존재, 이번에는 패키지/도구 변경 없이 등록된 beta13 명령 스키마를 사용했다.
- Editor Compile PASS(up_to_date), Node84/84 PASS. 실제 빌드 대상은 기존 EditorBuildSettings 전체, 별도 테스트 코드 삽입/게임 규칙 변경 없음.

## 기존 외부 에셋 차단
- 1차 build_aaa3658f3840: 25.7초 Failed, SplinePatrolWayPointEditor 마지막 namespace 괄호가 UNITY_EDITOR 밖에 있어 Player CS1022. 사용자 정확한 한파일 수정/비공개 저장·제출 승인.
- 2차 build_0ff0e2ed45ab: 3.5초 Failed, SplinePatrolCharacterEditor에 UNITY_EDITOR 없음·ObstaclePathEditor using가 조건 밖. 사용자 두파일 추가 승인. Editor 재컴파일 completed/failed=false 확인 후 재빌드.
- private fix/editor-waypoint-player-build fbc448cf +1a619e83, gh MoHoDu 작성자. 비공개 PR https://github.com/gaboja-studio/CURTAINCALL_PaidAssets/pull/2 (app attachment). 원문 public 반출 없음, 제공 게임/씬/리소스 변경 없음.
- private base383e0f4f, origin/main67d9007c와 차이는 자동 README1개뿐. PR update check SUCCESS·MERGEABLE/CLEAN 확인 후 main 병합(331883a0). public gitlink1a619e83은 origin/main 조상. 원문은 비공개에만 저장.
- Unity 빌드 자동 저장: 기존 폰트/머티리얼4개 외 CheerGauge Bold·URP설정3개·ProjectSettings2개 추가. PM이 이번 자동 변경과 PerformanceTestRunInfo/Settings만 복원 승인. 6개 복원, Performance 파일은 빌드 후 남지 않음. 기존4개 SHA256 동일 확인·커밋 제외.

## 현재 검사
- 3차 build_4c306b624d25: Windows64 Succeeded, errors0/warnings37, 551770ms, 1556836451bytes. Development+LOGS+SURVEY_010, 내장 endpoint/token 유효 여부만 bool 확인. 비밀값 출력 없음.
- 실제 exe -devmode 실행 → 기존 개발 메뉴 밤1 선택 → WaitingRoomBase 입장으로 NIGHT1 브라우저 자동 열기. 연결 코드의 TEST/0.1.0/run2e8193e9-a942-f30d-ebd8-b1a7dfdec355/slot0 확인.
- ESC 자동 입력은 게임에 전달되지 않아 사람 확인 요청. PM이 직접 ESC→나가기 후 종료 설문 표시 확인, 브라우저 EXIT 코드도 같은 run/slot 확인. 실제 응답 제출·폼 편집 없음. 두 종류 opened 캐시 생성 확인, 재열기 억제는 기존 EditMode7/7 근거이며 실제 반복 런 검사는 미실행.
- Player.log Drive ACK 및 Events1585 RUN_STARTED(TEST/0.1.0/slot0/위run) 실제 적재 확인. 기존 큐132개가 있어 이월 재전송도 진행, 큐 삭제/이전 원본 변경 안 함. 밤1은 일반 첫날 완주가 아니라 기존 개발 메뉴로 진입한 smoke다.
- PM의 물리 ESC가 네이티브 자동화 중단으로 인식되어 이후 네이티브 조작을 멈췄다. 이후 확인은 CLI/시트 도구만 사용. 컴파일/빌드와 사람 확인 결과를 구분한다.
- 제출 직전 dev에 PR207 공연 수정이 추가됨. 사용자 최신 dev 요청에 따라 origin/dev84517ba를 merge(21파일/충돌0), recompile completed/failed=false 확인.
- 새 Windows 빌드 build_fb03df1fd203 Succeeded(49015ms/errors0/warnings12), 출력 Builds/Telemetry-0.1.0-Windows-TEST-dev/CURTAINCALL.exe. 실제 실행·0.1.0/LOGS/SURVEY_010 manifest·수집 시작 확인. 설문 소스는 이전 실행 검증과 동일하여 재응답/반복 브라우저 입력은 생략. 기존 opened 캐시 보존. 6개 자동 변경 재복원, 에디터 태그off/off 복원. 네이티브 조작은 사용자의 ESC 중단 후 재시도하지 않음.
- Node84/84 PASS(140.6ms). Scope/Fast/Compile는 제출 전 현재 파일로 다시 확인한다. 전체 Unity 테스트 재실행·두 PC·Mac·실제 응답 제출은 SKIPPED(사용자 최소검사/보류 요청). 기존 전체 EditMode 목마 점프 실패는 이 smoke로 해결됐다고 주장하지 않는다.
- PM 재검증: 새 빌드 첫 밤/종료에는 다시 열리고 같은 프로세스 다음 런은 안 열림. SurveyCache에 서로 다른 identity hash2세트 생성 확인. 기존 UGS 익명 PlayerId 기반 캐시가 프로세스별 ID 변화에 취약한 실제 결함으로 판정, PASS 범위를 정정.
- 수정: 런타임은 persistentDataPath 아래 고정 installation identity 사용(OS 사용자·MPPM 저장 경계 유지). 같은 폼의 기존 account-hash marker도 읽어 이전 열림 보존, 코드에는 run/slot 그대로 유지. SurveyPolicyTests에 legacy→새빌드→재시작 회귀 추가, EditMode8/8 PASS(0.47초). 실제 캐시 bool eval은 UI main-thread timeout으로 판정에 사용 안 함. 최종 빌드 build_2c8ce100d5d9 Succeeded(24528ms/errors0/warnings8).
- 처음 실패 증거 .harness/local/telemetry-windows-build-first.json. 테스트 출력은 Builds/Telemetry-0.1.0-Windows-TEST/CURTAINCALL.exe (gitignored).
- 성공 전 dev 제출 금지. 실제 입력 경로로 검증하며 공개 API 강제 호출을 실제 플레이 검사로 보고하지 않는다.

- 최종 수정 exe를 새 프로세스로 실행, PM이 밤1→ESC 나가기 재검증 후 ‘안 뜨고 있어’ 확인. 이전 marker4개 유지·새브라우저 재열림 없음. 최종 Player 수집 시작/Drive ACK 확인. 6개 승인 자동 변경·에디터 태그off/off 복원 후 커밋한다.
- save-work DryRun은 PASS. 기존4개 보존 때문에 add-A 저장 대신 허용 경로만 git add/commit하고, new-pr의 dirty 차단은 해당4개만 남았음을 확인 후 동일 템플릿 gh body-file로 제출한다. 보호 훅·검사·작성자 규칙은 유지한다.
