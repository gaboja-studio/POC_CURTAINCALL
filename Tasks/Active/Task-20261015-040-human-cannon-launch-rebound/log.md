# Log

## 2026-10-15

- PM 승인 계획으로 Task 발급·CC-124 연결·첫 단계 단독 구역·4인 공동 테스트 3묶음을 준비했다. Unity에서 빈 씬과 폴더만 생성했으며 기능 구현·공동 검증·실제 전달은 미실행이다.
- CC-126 실제 URL·제목 확인 후 승인 준비 edf561c에서 5개 작업 브랜치 생성·push, e0ab817에 기준/승인 SHA 등록. 040만 assign-task 사전 검사·실행으로 assigned(a97fbbc), 기술 점검 todo 완료. #71은 배정 문서 Merge commit 수신용이며 기능 PR·실제 전달·공동 테스트 완료가 아니다.
- #71 최신 check-folder PASS 확인. 정상 Merge commit 병합 실행은 세션 권한에서 Merge Without Review로 거부되어 미실행, 다른 도구로 우회하지 않고 PM 정상 리뷰·병합을 기다렸다.
- 사용자의 #71 병합 안내 후 MERGED·233403c 실조회 확인(2026-10-15). 준비본 edf561c/통합 head 7ad0cdf의 정상 2-parent merge·tree 일치, 작업 브랜치 로컬/원격 ref 및 핵심 배정 문서 수신 일치 확인. 당시 착수 gate는 실제 전달 미확인으로 차단됐다.
- 이어서 PM 사용자가 5개 모두 실제 전달했고 전부 RagSpirin 담당이라고 확인했다(2026-10-15). 실제 전달 todo 완료, 040만 assigned/Active 유지. 완료 체크를 작업 브랜치로 정상 sync 수신한 뒤 착수하며 후속은 선행 검증·통합·소유 반환 전까지 예약이다.
- 전달 확인 26문서 범위·담당·상태·Allowed·준비 이력·verify-fast(hard0/WARN3)·diff --check PASS. 저장 직전 verify-unity -Compile은 ENV_BLOCKED(exit2), status 인스턴스0/기존 Pipeline 7802 연결 불가. 이전 컴파일 PASS로 대체하지 않고 commit/push/새 sync PR 생성을 중단했다. 폰트 변경은 보존했다.
- PM 사용자 문서 저장·각 브랜치 적용 요청에 따라 Unity 6000.3.25f1로 프로젝트를 열어 연결 복구, verify-unity -Compile PASS(up_to_date)/dirty0 확인. 실제 전달 확인 문서만 커밋하고 5개 준비 브랜치로 Merge commit sync하며 041~044는 scaffolded/Allowed 없음 유지.
- 문서 26개 b668b88 저장/push 후 #72~76을 정상 Merge commit으로 병합했다. 5개 수신본 모두 2-parent/두 번째 부모 b668b88/tree 일치 PASS. 040 원격 착수 gate PASS, 041~044 scaffolded 착수 차단 PASS; 실제 착수·기능 구현은 실행하지 않았다. 폰트는 미커밋 변경으로 보존했다.
- 2026-10-15 사용자 실제 테스트 보고: 안내한 1~4번(외줄 균형 제외, F 탑승, 홀드 취소, 새 F 발사) 모두 정상. R의 세 상황별 결과·Console 로그·4인 공동 테스트는 별도 미보고로 유지.
- 2026-10-15 비행 WASD 수평 보정 구현: 사용자 선택에 따라 누르는 동안만 가산하고 키 해제 시 기본 발사 속도 유지. 3m/s Inspector 임시값, 입력/중력 분리, 대각선 상한·초기화 정리. 로직 22/22 통과, 실제 플레이는 사용자 대기.
- 2026-10-15: 사용자 요청으로 바닥 1m 격자·5m 거리 숫자·포구 기준 HUD 전방/좌우 표시 추가. 씬 저장·눈금 Collider 0 확인. TMP 공용 폰트 자동 변환은 사용자 승인으로 원복하고 테스트 전용 사본 사용. 실플레이는 사용자 담당.
- 2026-10-15: 사용자 거리/WASD 정상 확인 기록. 트램폴린 잡기/운반/몸·장치 점프/놓기, 무주인·다인 반동, 호스트 ticket/segment 판정, 역할 인계·사망/접속해제·R 복원 구현. 사용자 선택: 수평 유지+상승12m/s·무입력 궤적에 기본 도형·R 장치 복원. 씬 저장, EditMode32/32 PASS. 실제 조작/멀티는 사용자 확인 대기.
- 2026-10-15: 사용자 수정 확인: 수직 하강은 수직 반동 후 같은 위치 하강 의미. 첫 접촉 이후 수평 관성과 WASD 보정을 제거, 새 대포 발사에서 원래 값 복구. 이전 전진 유지 결정 대체, HUD 안내 수정. 실제 플레이 확인 대기.
- 2026-10-15: 사용자 의도 정정: 반동에서 자동 전진 관성만 제거하며 WASD 조절은 계속 허용. AI가 잘못 추가한 airControlSpeed=0 제한 제거, 수평 기본속도0과 입력 해제 시 보정 종료 유지. HUD·현행 결정/검증 항목 수정.
- 2026-10-15: 사용자 요청/선택으로 트램폴린 잡기 즉시 정면 고정, 플레이어 방향 회전·점프 추종으로 변경. 원래 접근 거리 오프셋 제거, 손잡이 anchor를 몸에 맞춤. 놓기/동기화에 회전 포함·R 초기 회전 복원·회전면 접촉 보간 추가. 사용자 Play 종료 후 검증 진행.
- 2026-10-15: 정면 고정/회전 추종 변경 최종 검사: 구역·문서·컴파일 PASS, EditMode43/43 PASS, PlayMode NO_PROJECT_TESTS. 씬 dirty0, 실제 입력·멀티 확인은 사용자 담당.

## 2026-10-15 지상 점프 첫 반동
- 사용자 요청: 일반 지상 Space 점프로 윗면에 내려와도 반동. 사용자가 테스트 높이0.5m 선택(공용 점프0.8m 유지).
- PlayerMover의 공개 상태/Update 전후 위치로 하강 sweep을 검사, 운반자 제외. 첫 반동 호스트 승인 후 기존 수직12m/s·WASD3m/s 비행으로 연결. 첫 요청 sequence/장치 epoch와 후속 segment로 중복·R 이전 요청/응답 거부.
- 전용 씬 윗면/다리/손잡이 낮추고 대포 무입력 예상 접촉 위치 z25.03에 정렬, Unity SaveScene 완료. 최초 eval은 새 필드가 미컴파일되어 적용 전 실패. 누락된 PlayerState namespace 수정 후 컴파일 PASS. Undo 후속 dirty로 검사 1회 차단, Undo flush/소유 씬 재저장 후 dirty0.
- EditMode 51/51 PASS. 자동 Play 조작 없음, 실제 지상 점프/운반/대포/멀티 확인은 사용자 담당. 공용 파일 변경 없음.
- 최종 check-work: 구역/문서/컴파일 PASS. PlayMode 조회는 컴파일 후 연결 reset으로 실패했으나 verify-unity -RunTests -Mode PlayMode 단독 재실행 NO_PROJECT_TESTS. 연결 포트7801 복구. 씬 저장/높이 재조회 및 human-cannon-ground-jump.png 배치 확인, Play 실조작 미실행.

## 2026-10-15 쿠션 정상 착지
- 사용자 1~9 정상 보고는 혼자 플레이한 확인으로 기록. 4인 공동 검증과 구분. 다음 임시 쿠션 지대 구현/배치 승인.
- 전용 수평 BoxCollider 윗면만 정상 착지, 발 위치·현재 비행 식별자로 호스트 확정 후 지상 이동 복구. 일반 바닥/코스 밖 사망 유지. R/중복 착지 무효화·안전 착지 횟수 초기화. 전용 씬 오른쪽6m 주황4×4m/높이0.2m 쿠션 저장(root10, dirty0).
- 컴파일 PASS, EditMode 최초 60/61(편집 모드 OnEnable 미호출로 Collider 캐시 없음)→공개 조회에서 Collider 확보 후 61/61 PASS. 실제 쿠션/멀티 플레이는 사용자 담당.
- 최종 check-work 구역/문서/컴파일 PASS, PlayMode NO_PROJECT_TESTS. Unity 저장/재조회 root10·dirty0·쿠션 solid collider/top0.2m 확인, human-cannon-cushion.png 시각 배치 확인. 변경 파일은 Task 소유 구역뿐.
- 2026-10-15 사용자 쿠션 테스트 1~5 모두 정상 확인 보고. 예정 기능 구현과 개별 조작 확인을 완료로 기록하고, 다음은 호스트1/참가자3 공동 검증으로 안내. 기존 plan 3묶음을 점유/역할·다인/무주인/인계·착지/R/참가자 이탈의 안내1~8로 구체화. 실제 멀티 결과는 미수신. 코드/씬 변경 및 Play 실행 없음.

## 2026-10-15 제출 판단 전 기능 오류 점검
- 사용자 지시 변경: 4인 공동 검증은 인간 대포 전체 통합 후. 현재 기능 오류 점검 결과만 보고하고 제출은 사용자 결정, commit/push/PR 미실행.
- 코드 점검에서 R 당시 빈 대포의 revision 미갱신으로 지연 탑승 요청이 수락되는 누락 발견. 모든 좌석 Reset revision/전용 메시지로 이전 요청 거부, 늦은 발사·취소 응답 후에도 비행 해제/출발 위치 복구. 회귀 테스트3개 추가.
- 최종 check-work 구역/문서/컴파일 PASS·PlayMode NO_PROJECT_TESTS, EditMode64/64 PASS. 씬 dirty0/missingScripts0/직렬화 참조 정상. 현재 Console 오류0/경고0; 보존 오류8개는 수정 전 컴파일5+과거 UDP7777 충돌3, 현재 UDP7777 미점유. 새 실제 Play는 미실행, 사용자 기존 트램폴린1~9/쿠션1~5 정상 보고와 구분. 검사 범위 내 잔여 오류 미발견.

## 2026-10-15 제출 요청
- 사용자 Task 제출 승인. 4인 공동 테스트는 인간 대포 전체 기능 통합 후 진행하며 현재 미실행으로 기록. 최신 통합 브랜치 병합 확인(Already up to date), 제출 검사 구역/문서/컴파일 PASS·PlayMode NO_PROJECT_TESTS. gh 설치/사용자 로그인 완료(@RagSpirin), 커밋/푸시/PR 등록 진행.
- 구현 c16d45b 커밋/푸시 및 PR #93 제출 완료: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/93 → integration/main-stage-human-cannon. PR 본문에 EditMode64/64·사용자 단독 확인·4인 전체 통합 후 진행을 명시. PM 검토/통합 대기.

- 2026-10-27 하네스 3문서로 이관. [이전 현재 상태·검사·미검증](History/Harness-v1/handoff.md), [결정](History/Harness-v1/meta.md), [참조 경로](History/Harness-v1/setup.md). 예전 구역·인계·세부 상태는 현재 제한이 아님.
