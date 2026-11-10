# Log

이전 단계 원문: [001-previous-stages.md](History/001-previous-stages.md).

## 2026-11-10 봉 및 첨부 12항목 전체 통일
- 요청: UI를 제외한 통일 미완료 항목을 전부 적용하고 변경을 빠짐없이 기록. 앞선 봉 1~6단계 승인과 80°/180°/30초/상단만 반동/2인/놓으면 스위치 해제/운반 관성·자동 하차 제거를 포함한다.
- 같은 Task에서 doctor를 중복 실행하지 않았다. 구현 시작 전 dirty scene/asset 0 확인 → Unity 정상 종료 → 기존 TMP 사본 보존 → origin/dev fetch·ff-only merge(Already up to date, 2d591802) → 기존 작업 브랜치에서 에디터 재개. commit/push 없음.
- GameSettings에 PoleSettings, TrampolineSettings를 추가하고 Base에 공통 점프 버퍼·접근 거리·공중 조절 설정을 연결. 옛 씬 저장값은 참조 보존용으로 남기고 실행 경로에서 배제했다. 설정 에셋은 Unity API로 생성/저장했다.
- 봉 점유/운동/비행을 HumanCannonPoleSystem으로 통합. 외줄 Trapeze/Relay/Swing의 중복 Motion/Network/Pending 경로를 제거하고 배치·코스 연결 어댑터로 바꿨다. 일반·스위치 외형은 기존 프리팹을 런타임 재사용한다. 새 외부 에셋 없음.
- 생성 봉은 생성 순서 대신 배치 경로 기반 네트워크 ID를 사용. 재생성 상태 초기화, 먼저 로드되는 봉 서비스의 상태 준비, 비활성/사망/연결 해제 시 점유·스위치 정리를 보완했다.
- 착지는 실제 지지면을 확인해 일반 바닥인데 대포 비행이라는 이유만으로 다치지 않게 했다. 급경사·트리거·플레이어 몸은 제외한다. 3m/5m 낙하 피해와 대포 지상 톱 높이는 명시적 후속으로 유지한다.
- 트램펄린은 상승 12m/s·운반 4.5m/s·공중 조절 3m/s. 운반자 필요는 처음 본편 true를 선택했으나 마지막 연습 운반자의 SelfBounce가 막히므로 **전체 false**로 수정했다. 이는 사용자가 지정하지 않은 구현 선택이며 별도 예외 없이 바닥 반동을 허용한다.
- 부상 이동은 1/.8/.5, 팔 손실 접근 .8로 Base를 사용한다. 대포 최소80%/외팔 지연, 외줄 별도 접근 보정, 높은 버튼의 몸 높이 보상을 제거했다. 목마 연결은 별도 공통 1.5m 행동 거리다.
- 일반 근접 상호작용 2.5m는 탐색·의료실·상점/선반·기물·대포/봉/트램펄린·연습 시작까지 연결. 로컬 선택과 호스트 승인에서 같은 팔 부상 제한을 사용. UV 원거리·가림·다른 층 판정은 유지한다. 직접 F 읽기 3곳을 공통 Interact 액션으로 변경했다.
- 원래 전체 EditMode 초회 1844 중 1796 통과/48 실패. 여러 실패는 바뀐 규칙을 전제로 하지 않은 이전 테스트였다. 같은 기본 구현을 단순 복제한 테스트가 아니라 실제 운동/네트워크 경계/입력/수명 검사를 새 공통 경로로 옮겼다. 폐기한 외줄 전용 비행 프로토콜 검사는 공통 봉 프로토콜 검사와 실제 PlayMode 검사로 대체했다.
- Pipeline은 도메인 재로드를 포함하는 전체 검사에서 summary와 반환 개별 결과 수가 다를 수 있었다(1844개 요약/177개 상세). 검증 중 임시 TestRunner 콜백으로 전체 NUnit XML을 저장하고 전달 전 임시 스크립트를 제거한다. 정규식 필터 시도 1회는 NO_PROJECT_TESTS이며 통과에 포함하지 않는다.
- 현재 최종 PlayMode: Ordinary 4/4, CommonPoles 3/3, AppliedRoundTrampoline 4/4, W6_Rehearsal_TrapezeUsesPerformanceDevicesAndRules 1/1, CarryMovement_UsesCommonCarrySpeed 1/1. 실제 LAN 호스트와 CharacterController 경로이며 원격 탑승자는 합성 소유자다. 두 실제 PC 동시 검사로 표현하지 않는다.
- 활성 봉 36개의 배치 좌표를 읽기 전용 preview scene으로 조사했다. 직접 도약 연쇄/중간 플랫폼·외줄/운반 레일을 구분했다. 씬의 기하/배치는 저장하지 않았다. 전 씬 수동 완주는 미검증이다.
- 전체 규칙·변경 이유·수치·제거한 예외·남은 범위는 full-change-list.md에 기록한다. 기존 Task-112/Final-build 지식 문서는 보존했다.

### 최종 검사 결과와 정리
- 추가 PlayMode FourFinalFlights1/1, CommonRopeRules3/3 통과. 이번 최종 PlayMode 합계17/17.
- 전체 EditMode 최종 실행1845 중1828 통과/17 실패. Cannon/Tightrope/상호작용/지지면/생성 봉 ID 관련 새 검사 통과. 전체 XML과 JSON을 .harness/local/task114-full-edit-final.*에 보존했다.
- 이후 ControlRulesTests의 오래된 달리기 기대1.5를 Base2로 맞추고, ExitSawTests reflection 호출에 원래부터 필요한 선택 인자 null을 추가했다. 집중 재검사18/18+39/39로 실패3개 해결. 전체 통과 수를 합성하지 않는다.
- 남은14개는 보류한 톱 높이1, 난이도/휴식9, 추격자 원본/통합 씬 차이1, 보상 생존 호응1, 문구2. 범위 밖 구현/설정을 임의 수정하지 않았다. 모든 실패 이름·메시지·판단을 Inventory/verification-failures.csv에 기록했다. 별도 깨끗한 기준 프로젝트 전체 재실행은 하지 않았다.
- 임시 Task114TestRecorder.cs/.meta는 AssetDatabase.DeleteAsset으로 제거. CheerGauge/CommonButtons 동적 캐시는 원래 fontFeatureTable을 보존하고 Unity TMP API로 초기화하여 해당 파일의 Git diff를 없앴다. escape-labels는 작업 전 사본을 복원/재import했다. 추가 자동 캐시가 기존 사용자 변경에 섞이지 않게 했다.
- 모든 변경 파일과 목적은 Inventory/changed-files.csv, 사용자용 전체 규칙은 full-change-list.md, 검사 상세는 verification.md. 수동 전 코스 완주·실제 두 PC·보류 낙하 규칙은 후속으로 표시했다.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)
- check-work 1회차는 full-change-list.md190줄로 문서 hard130 제한에서 실패하여 컴파일을 건너뛰었다. compact-docs 절차 적용: 현재 목록의 모든 번호 항목을 Changes/3개 문서로 그대로 분리하고 root에 링크. 과거 log는 History/001로 원문 보존했다. 문서 검사를 다시 수행한다.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 재검사 안전성/문서/컴파일 PASS, 자동 테스트 재실행 안 함. Context hard0/warn61, Knowledge17폴더 OK, git diff --check 통과. 현재 규칙112개 번호 항목 분리 전후 일치, 파일별201개 목록 완료. 에디터 API 최종 값 확인 및 GameSettings.asset 선택, Play 중 아님. 커밋/push 없음.

## 2026-11-10 남은 통일 후보 조사
- 사용자 요청은 추가 차이 조사. UI 제외, 게임 코드·씬·설정 변경 없이 현재 구현을 읽었다. doctor는 같은 Task이므로 재실행하지 않았다.
- Unity 미저장 씬/설정 0 확인 후 정상 종료, 기존 escape-labels 사본 보존/복원, origin/dev fetch·ff-only merge는 Already up to date(2d591802). 기존 브랜치에서 에디터 재개 후 ready·컴파일 없음·Play 중 아님 확인. commit/push 없음.
- 피해 간격, 상호작용 가림/범위, F 대상 선택, 줄 추락 매달리기, 카메라 벽 충돌을 추가 후보로 확인. 신발 효과 범위·운반 기물 회수·화염 종류는 기존 의도를 먼저 구분할 후보다. 상세 근거/추천은 remaining-unification-audit.md.
- 조사 결과는 정적 근거다. 벽 너머 상호작용·입력 중복을 실제 재현한 것으로 표현하지 않는다. 이전 보류 3m/5m와 톱 높이는 별도 유지한다. 게임 구현이 바뀌지 않아 런타임 테스트는 반복하지 않는다.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-10 추가 통일·폐기 설정 제거 구현
- 사용자 7항목 및 추가 3항목 승인. 추락 3m/5m도 이번 적용으로 확정. UI 외형, 운반 기물 회수 정책, 톱 배치는 범위 밖.
- Unity 정상 종료 확인·기존 font 사본 보존/복원 후 origin/dev fetch 및 ff-only merge 완료(Already up to date, 2d591802). 같은 브랜치·Task 유지, Unity PID33732 재실행.
- 공통 위험물 1초, 몸 중심 3D 거리/가림, F 단일 선택, 3m/5m 낙하 및 신발, 1.5m 추락 매달림과 실패/만료 일반 낙하, 화염 종류, 모든 씬 카메라 벽 충돌을 반영했다.
- 봉/트램펄린/대포/외줄/대기실의 폐기 필드와 제작 도구 참조 제거. CommonSettingsValidation을 빌드 및 verify-unity 컴파일 검사에 연결. 상세 제거 목록 Inventory/retired-settings.json.
- 폐기 필드32타입64개와 제작 도구를 정리하고 Unity API로 8개 씬·4개 프리팹·2개 설정의 옛 저장값을 제거했다. 12개 씬/프리팹의 위치·회전·크기 diff는0. 외줄 판정 폭/높이/이탈 깊이는 공통 RopeMovement로 이동, 구조 거리는 Base 근접 거리만 읽는다.

- 새 규칙 EditMode18/18, 외줄 집중69/69, PlayMode 공통18/18 통과. 전체 EditMode1860/1845통과/15실패 중 새 검사 좌표 문제1개는 위69개로 해결, 나머지14개는 이전과 같은 범위 밖 실패다. 상세 followup-verification.md 및 CSV. 전체 통과라고 보고하지 않는다.
- Confluence 39124994 변경 전·후 live doc 갱신(snapshot d1:42) 및 읽기 재확인. 기존 항목 유지·추가11항목, 폐기64개·3m/5m 반영. UI 외형·맵 배치·운반 기물 회수 정책은 유지. commit/push 없음.
- 구조 거리 후속 PlayMode4/4 통과. 최초 재검사3/4의 위치8cm 차이는 수직 접지 보정과 입력 이동을 분리하고 입력 즉시0·수평 정지를 확인했다. 파일명 필터 실행1회는 NO_PROJECT_TESTS. 테스트 TMP 캐시는 Unity API로 정리하고 기존 escape-labels 사본을 복원했다.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=FAIL; 테스트=SKIPPED(자동 테스트 선택 안 함)
- check-work 1회차는 Unity 컴파일 자체 PASS였지만 run_script 응답의 바깥/안쪽 result를 혼동해 검사 래퍼가 실패했다. 실제 공통 검사는 오류0. 응답을 정확히 풀고 errors 배열 유무·실행 success를 확인하도록 수정하여 재검사한다.

- 2026-11-10 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 최종 check-work 안전성/문서/컴파일 및 공통 설정 검사 PASS. 응답 정상·연결 누락·실행 실패 전달3/3 확인. 누적 파일 목록307개 갱신, 최종 검증 followup-verification.md. 실제 두 PC·전 코스 완주와 기존 범위 밖14실패는 남겨 두며 커밋/push하지 않았다.

## 2026-11-10 변경 전후 문서 표 재작성
- 사용자 요청에 따라 Confluence 39124994를 13개 표·109개 항목으로 재작성. 항목/변경 전/변경 후, 수치와 조건 중심의 짧은 표현. 기존12개 제목 유지·유지/보류 표 추가. snapshot d1:80 읽기 재확인.
- 톱 상단0.42m는 미조정으로 명시. 옛 봉 수치는 pole-differences.md·씬 저장값 조사, 현재값은 적용 기록과 코드로 대조했다. 게임 코드/씬/설정 수정 없음.
- 실패14개 원본 XML·CSV·실패 지점 확인: 난이도5개는 가이드 휴식의 튜토리얼 완료 전 입장 거부, 휴식4개는 WaitingForExit와 검사 기대 불일치. 보상1개는 검사50과 실제50+완주100=150 차이. 나머지 톱 높이1·추격자 Small/Medium 차이1·문구 누락/중복1·사용처 미검출121키1. 테스트/게임 수정과 재실행 없음.
- 문서 변경 검사: check-work -SkipUnity -NoRecord, 안전성/문서·규칙 PASS. 이번 단계는 문서만 변경하여 Unity 컴파일·플레이 검사는 생략(스크립트 종료코드2). Confluence 표13개·내용 행109개 및 주요 변경값 읽기 재확인 완료.

## 2026-11-10 표에 씬 구분·글머리 기호 추가
- Confluence 39124994의 기존13개 표·109개 항목 유지. 각 항목에 대상 씬, 변경 전에 씬별 값/동작 추가. 표 셀 안 실제 글머리 기호 적용. 씬 파일/대기실 런타임 연습 생성 구분 추가.
- 기존 사용자 표 너비·배치·local ID 보존. 봉 활성 조사 CSV, 씬별 트램펄린 저장값, HEAD의 대포 연습 프리팹 대조. 별도 확인하지 않은 옛 발 높이 수치는 미확인 표시. snapshot d1:426 읽기 재확인. 코드/씬/설정 수정 없음.
- 문서 검사 안전성/문서·규칙 PASS. 문서 작업이므로 Unity 검사 생략(-SkipUnity, 종료코드2). 저장 후109개 대상 씬·전후 셀 글머리 기호 확인, 기존 local ID883개 모두 보존 확인.

## 2026-11-10 저장·제출
- 사용자 승인으로 Task-114 관련 변경 commit/push·dev PR 제출을 진행한다. Task-112 기존 문서와 무관한 변경은 제외하며 공용 색인에는 이번 작업의 문서 연결만 포함한다.
- Confluence 39124994에 실패14개 표 추가. 씬 미실행 단위 검사와 실제 씬 검사를 구분하고 기대/실제값·원인·검사 이름 기록. snapshot d1:433 읽기 재확인: 기존13개 표/109항목 및 모든 local ID 보존, 신규 표14행 확인.
- Unity 종료 후 origin/dev fetch·현재 브랜치 반영: 2d591802 동일, Already up to date. 기존 사용자 폰트 사본 보존, Unity 재실행. 실패14개는 추가 수정하지 않고 최종 코드 검증 결과를 재사용한다.
- 제출 문서 검사: check-work -SkipUnity -NoRecord, 안전성/문서·규칙 PASS, 컴파일/테스트 생략(종료코드2). 게임 코드·설정은 마지막 PASS 검사 이후 동일. new-pr -DryRun으로 제목/방향/템플릿 확인; 별도 작업 변경을 남겨야 하므로 명시 경로 stage와 gh --body-file로 저장·등록한다.
- 명시 경로309개 stage 확인. 최초 staged whitespace 검사가 신규 기록/테스트의 공백을 발견해 정리했다. 씬 조사 CSV는 빈 마지막 칸을 인용하며 파싱 결과 동일 확인, 테스트는 빈 줄 공백만 제거. 최종 git diff --cached --check PASS. 관련 없는 기존 파일29개와 공용 색인의 다른 작업 항목은 작업 트리에 보존한다.
- 저장: fe3b7bda, refactor/common-game-settings 원격 push 완료. 작성자 @RagSpirin. GitHub가 기존 대기실 씬3개의 75MB 크기를 경고했으나 push 성공. 에셋 저장 방식 변경은 별도 범위로 유지한다.
- 제출: [PR #317](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/317), refactor/common-game-settings → dev. 미해결14개·검사 결과·두 PC/전 코스 미확인과 Confluence 링크 포함. 병합하지 않았으며 지정 팀원 검토 대기.

## 2026-11-10 문서 대상 축소·검사 수정 중단
- 재개 준비 중 Unity 저장 상태 확인·정상 종료 후 origin/dev fetch/반영: Already up to date. 기존 폰트 사본 보존 후 Unity 재실행. 실패14개 관련 코드는 읽기만 했으며 변경·테스트 재실행 없음.
- 최종 사용자 지시: 실패14개 수정 중단. 현재 보고 대상에서 탈출로 B/C 내용 제외, 표 전체 너비 확대만 진행한다. Confluence 기존 변경 전후109개 항목·실패14개는 유지하고 B/C 비교 설명·씬 경로만 제거한다.
- Confluence 현재 제목 05. 공통 규칙 통일 — 변경 전·후, snapshot d1:923 읽기 검증: B/C 참조0, 변경 전후 표13개 너비1138px, 실패 표1150px 유지, 총14개 표/내용123행·글머리기호 보존. 문서 검사 안전성/문서·규칙 PASS, Unity·테스트 생략(종료코드2). Assets/Scripts 변경0. 추가 commit/push 없음.

## 2026-11-10 공통 규칙 Inspector 조정
- 사용자 요청으로 기존 설정에 고정 규칙7개 추가. 봉 인원1~4(기본2), 상단 단독 반동/탑승자 평균, 아래 사람을 둔 놓기 제한, 외줄 점프 중력, 일반 위험물/낙하 절단 개수, F 구조 우선 선택. 기존 기본값 유지. UI 외형·보류14개·탈출로 B/C 씬 수정 없음.
- 3~4인도 바로 위 사람을 잡고 수직으로 연결. 중간 이탈·만료는 아래부터 개별 revision으로 놓아 클라이언트가 비행 승인을 놓치지 않게 처리. 봉 정원은 생성 시 적용하며 재시작 안내를 Tooltip에 명시.
- 봉/트램펄린 전 항목과 신규 항목에 한글 이름·효과·단위·기본값·적용 시점 설명. 기존 피해/공중 조절/착지 파동 설명 누락 보완. 한글 이름은 기존 설정 Editor와 새 중첩 Drawer로 표시.
- Unity API로 Pole/Base/Tightrope 에셋 새 값 저장. Base의 기존1초/3m/5m 기본값도 명시 저장. EditMode7/7 통과. PlayMode 최초4개 중3개 통과, 새 피해 검사 타입 이름 오기1개 수정 후 해당1/1 통과(실행 코드 변경 없음). 기존2인 동작·4인 반동/중간 이탈/만료·설정 피해 개수 확인.
- 증거: .harness/local/inspector-edit-tests.json, inspector-play-tests.json, inspector-damage-final.json, inspector-settings-saved.json. 상세 사용법 inspector-controls.md, Confluence 변경 전후에 조정 표8행 추가. 전체 자동 검사와 실제 두 PC 플레이는 재실행하지 않았다.
- 외줄 공중그네 접근 어댑터에 남은 2인 점유 제한도 제거. 외줄4인 검사에서 생성 후 설정 변경과 낮은 봉 아래 바닥 차단을 분리했고, 씬 생성 전 정원 설정·틈 위 봉에서 1/1 PASS(inspector-trapeze-final.json). 최초2회 실패와 테스트 Attribute 이름 충돌은 검사 준비를 수정해 해결. 층·가림 규칙은 그대로 유지한다.
- 최대 인원은 플레이 중지 후 변경·재실행, 낮은 봉은 아래 공간 필요: Tooltip/사용법/Confluence 적용 시점 명시. Confluence d1:932 읽기 재확인: 표15개와 기존 local ID 보존. 이번 관련 검증은 EditMode7개·PlayMode5개 시나리오 최종 통과, 기존 실패14개 보류 유지.
- 최종 check-work -NoRecord: 안전성/문서·규칙/컴파일/공통 설정 연결 PASS, 전체 테스트 선택 안 함. 문서 길이 경고63개·hard0. Unity는 Start 씬·비재생·미저장 씬0, PoleSettings 선택·기본 정원2. 마지막 테스트가 만든 동적 폰트 캐시는 사본 보존 후 Unity API로 제거. 추가 commit/push 없음.

## 2026-11-10 Confluence Inspector 조정 방법
- 사용자 요청으로 페이지39124994에 Project 폴더→설정 에셋→Inspector 펼칠 메뉴/항목→기본값 표7행 추가. 봉 기존 시간/각도/속도·트램펄린 위치, 호버 설명·플레이 중지 후 재실행 안내 포함. local inspector-controls.md 동기화.
- d1:935 읽기 검증: 새 표1138px·셀28개 글머리 기호, 기존 local ID 전부 보존, 총16개 표. 실패14개/기존 전후 표 변경 없음. check-work -SkipUnity -NoRecord: 안전성/문서·규칙 PASS, 문서 작업으로 컴파일/테스트 생략(종료코드2). 코드·에셋 수정 및 추가 commit/push 없음.

## 2026-11-10 튜토리얼·대기실 후속 수정
- 시작 dev 동기화: Unity 종료 후806c8dab 반영. 기존 Inspector 확장·Task-112 변경 보존. 사용자 승인4m/6m, 봉 일반/스위치 각1.8m, 최초 교육 바닥 복귀·문·집결·도착 벽을 기존 GameSettings에 연결.
- 공통 외형 케이블·손 위치·부모 크기 분리·종료 수명, 낙하 순간이동/부활 번호/중복 보고, 외줄 재연습 사망 조작 복귀를 수정. 상세는 tutorial-regression-audit.md.
- 추가 발견 중 탈출로 전체는 사용자 지시로 제외. 나머지8개 수정·저장·제출 승인. 안전 착지대 무피해는 사용자 선택에 따라 BaseGameSettings에 추가하고 에디터 API로 저장. 일반 바닥4m/6m 유지.
- 대기실 생존자 밤 준비·의료 예약 해제/미수행분 환급·사망자 행동 차단·침대 F 참조·1세대 교육 제한 수정. 인간대포 재연습 무료 복구 제거, 사망 상태/사지 유지 귀환과 분리된 시체 순간이동 연결. 상세는 cross-scene-regression-audit.md.
- 이전 재현/검증: 문·경계 Edit10/10, 봉 외형 Edit15/15, 낙하 Play3/3, 봉 Play2/2. 마지막 추적 재검사5/6은 봉 외형 언로드 setup 오류1개를 발견해 수정. 대포 정상 착지 사망 진단1개 실패 후 안전착지대 정책 수정. 이 두 실패를 재실행 PASS로 바꾸지 않음.
- 최신 사용자 요청으로 추가 PlayMode/두 PC/전 코스 플레이는 실행하지 않음. 최종 코드 컴파일 PASS, 대기실 단위14/14·Inspector7/7 PASS. 기존 보류 실패14개 전체 수정은 재개하지 않음.
- Confluence39124994 d1:950 읽기 검증: 표19개, 기존 local ID 전부 유지, 너비1138/1150 유지. 튜토리얼·봉 전후19행·추가8개 전후·설정 위치 갱신, 탈출로는 현재범위 제외. 기존 보류14개 유지.
- 제출 직전 Unity 정상 종료 후 origin/dev fetch/merge:806c8dab 동일, Already up to date. Unity 재실행. 기존 동적 폰트 사본은 .harness/local/tutorial-common-audit/fonts-before-submit에 보존, 관련 없는 기존 파일은 제출 제외.
- 최초 check-work: DEV에 기존 포함된 Task-097 todo102줄 hard 초과로 문서 검사 FAIL·통합 컴파일 생략. 완료 체크 섹션5개를 History/009에 원문 그대로 보존해 todo76줄로 줄임. 미완료 항목·현재 결정은 유지.
- 최종 check-work 재검사: 안전성/문서·규칙/컴파일/공통 설정 연결 PASS, 전체 테스트 선택 안 함. 문서 길이 hard0. 기존 사용자·Task-112·동적 폰트32개는 제출 제외하고 이번 관련82개만 저장한다.
- 저장·제출 완료: d34b5043, refactor/common-game-settings push, dev 대상 [PR #319](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/319) 생성. 병합하지 않음. Unity는 Start 씬·비재생·PoleSettings 선택 상태. 플레이 검증은 사용자 담당.
