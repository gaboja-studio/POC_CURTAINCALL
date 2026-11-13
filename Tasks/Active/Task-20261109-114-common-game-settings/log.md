# Log

이전 기록: [PR350까지](History/017-log-through-pr350.md).

## 11/12 PR 350 행동 조합 재검사
- DEV 3aabf021 포함 확인. 안전성·문서·컴파일·공통 설정 통과. EditMode 공통 587/587, 톱날·운반·입력 후속 124/124, 밤 전환 4/4. doctor의 기존 Task ID 중복은 계속 남아 있다.
- 목마·외줄 집중 단독 실행 35/40. 양 끝 W/S 4건, 도구 동작 종료 직후 목마 점프 1건은 공통 묶음에서 모두 통과했다. 같은 테스트 함수의 대표 사례를 입력 잠금 유무로 실행해 잠긴 프레임에서만 같은 실패를 확인했다. 단독 테스트의 공통 입력 잠금 초기화가 필요하다.
- F 홀드 경계에 샘플이 없으면 놓는 프레임에서 경과 시간이 0.5초를 넘었어도 탭이 발생하는 코드 경로를 확인했다. EditMode 게임 시계가 0이므로 실제 프레임 지연 재현은 미실행.
- 두 대기실 씬에서 연습실 문턱과 같은 XZ의 지붕 위(y 5.8m)와 아래 가상 지점도 실내로 판정했다. 정상 문턱과 완전 통과는 구분했다. 결과 .harness/local/nov12-combo-probe.json. 실제 플레이 및 코드 수정은 이번 검사에 포함하지 않았다.

## 11/12 F 홀드·연습실 지붕 판정 수정
- DEV 836dd382 반영. 공통 0.5초 기준에 실제 입력 이벤트 시각을 사용한다. 한 번에 들어온 0.45초·0.50초·0.60초 이벤트와 홀드 발행 후 해제를 EditMode에서 확인했다.
- 두 대기실의 실제 바닥·지붕 사이를 출구 통로로 센다. 두 씬 모두 문턱과 공중 탑승자를 포함하고 지붕 위·바닥 아래·완전 통과 위치를 제외했다. 결과 .harness/local/nov12-combo-fixed-exit.json.
- 단독 검사 입력 잠금을 격리했다. 목마·외줄 40/40(이전 실패 5개 포함), 톱날·공통 입력 128/128, 공통 587/587. 안전성·문서·컴파일·GameSettings 통과. Unity 도메인 재로드 직후 검사 연결 오류는 재시도로 해소했다. 실제 멀티플레이는 사용자 확인 예정이다.
- 제출 직전 DEV 836dd382를 재확인했다. 수정 커밋 6f41e3fe를 [PR #351](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/351)에 제출하고 PM 팀 검토를 요청했다.

## 11/13 행동 조합 추가 감사 — 수정·제출 없음
- origin/dev fetch 결과 836dd382가 HEAD에 포함돼 있다. doctor 기존 Task ID 중복 실패. 전체 검사: 안전성·문서·컴파일·공통 설정 통과, EditMode 2268개 중 2250 통과·18 실패. 검사 중 생성된 폰트 atlas로 최종 파일 지문 검사는 실패했으며 해당 생성분만 원복했다. Start 열린 상태를 복원했다.
- 추가 합성 재현 4건: 목마 상태 대포 연습 종료에서 복귀 위치가 동료 머리 위로 덮임; F 도중 메뉴 입력 초기화 후 즉시 새 F를 누르면 소비 상태 잔류; InputSystem.ResetDevice가 탭을 발행; 사지 조각을 머리 위에 두면 기립 거부. 게임 코드·공통 설정은 변경하지 않았다.
- 전체 실패18개 분류와 범위는 [실패 목록](Inventory/remaining-test-failures.md). 잘린 다리 검증용 에셋 물리 검사는 단독 1/1 통과. 원본 XML `.harness/local/nov12-all-editmode-results.xml`, 합성 결과 `nov12-extra-combination-probe.json`. 실제 다른 PC 2·3·4인 플레이는 미실행.

## 11/13 재현 4건·대포 Space·사지 충돌 수정
- 기존 변경 9개를 .harness/local/nov13-before-followup에 백업. Unity 정상 종료 후 origin/dev 19bb420f(PR352)까지 fast-forward. doctor의 기존 Task ID 중복 실패는 이번 코드 작업을 막지 않음.
- 목마 연결 해제를 확인한 후 리허설 복귀 잠금 저장, 입력 초기화 시 F 제스처 폐기, 장치 리셋의 가짜 해제 무시, 사지 조각의 기립 방해 제외. 대포 공통 Action 입력 발사와 트램펄린 전체 충돌체의 물리 무시 추가. 자동 검사 진행 중.
- 1차 공통 검사 600개 중 598개 통과. 신규 트램펄린 물리 2개는 EditMode에서 SceneManager.CreateScene을 사용할 수 없어 준비 단계 실패. 독립 물리가 있는 PreviewScene으로 수정 후 입력·운반 133/133 통과(실제 사지 통과/일반 바닥 접촉 포함). 도메인 재로드 직후 run_script 연결 실패는 재시도로 해소.
- 종료 상태 수신보다 늦게 온 목마 연결 요청도 호스트에서 거부하도록 보완하고, 복귀 후 새 요청은 다시 허용하는 회귀 검사 추가. 최종 공통 검사 진행.
- 최종 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS. 공통601/601, 입력·운반133/133(중복 포함). 새 14사례 모두 통과. `.harness/local/nov13-final-common-results.json`, `nov13-input-carry-results.json`. 실제 PlayMode/다른 PC 2·3·4인 플레이는 사용자 확인.
- 제출 직전 Unity 정상 종료·Start 씬 dirty 0 확인 후 origin/dev 19bb420f 재확인(변경 없음). 기존 Start/에셋/다른 Task 수정은 보존하고 이번 수정 경로만 커밋한다.
- 수정 커밋 2d61cbad를 [PR #353](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/353)에 제출하고 PM 팀 검토를 요청했다. new-pr 미리보기 완료; 기존 다른 작업의 미커밋 파일은 보존하기 위해 이번 커밋만 push하고 gh로 등록했다. dev 병합은 하지 않음.

## 11/13 PR353 이후 추가 진행 차단 감사
- Unity 종료 뒤 origin/dev 1764a21c(PR353 병합)로 fast-forward. 기존 변경은 .harness/local/nov13-next-audit-before에 보존. doctor의 기존 Task ID 중복 실패는 유지, Unity 연결 정상. 게임 코드·세팅 변경 없이 새 전환 조합을 조사한다.
- 치료 중 대포 연습 시작 시 입력 잠금/치료 종료 뒤 코스 이탈 3회 재현. 치료 입장 때 트램펄린 소유가 겹치면 실제 공통 충돌 Mesh+임시 측벽 두 배치에서 약2.96m 밀림과 잠금 유지 재현. [상세·한계](Inventory/post353-transition-audit.md). 신규2건 미수정.
- 기존 치료19/19·리허설70/70 통과. 복합 필터 첫 시도는 NO_PROJECT_TESTS, 개별 필터로 유효 결과 확보. 임시 Scene 생성은 EditMode API로 바꿔 재현. 실제 PC 간 플레이는 미실행. 이전601개 결과와 이번89개를 중복 합산하지 않음.
- check-work 안전성·문서/규칙·컴파일·공통 설정 PASS, 명령 내 테스트 SKIPPED(위89개 별도). Knowledge19폴더 OK, 기존 문서 길이 WARN71/하드0. Start/동적폰트는 작업 전 백업과 해시 동일. 커밋·push·PR 제출 없음.

## 11/13 치료·리허설·운반 인계 수정
- 사용자 수정·제출 요청 후 Unity 정상 종료, 기존 변경 `.harness/local/nov13-medical-fix-before` 백업, origin/dev 1764a21c 최신 확인. doctor는 기존 Task ID 중복만 실패하여 코드 작업 진행.
- 치료 Working/Exiting 중 재연습 시작 배제, 반대로 리허설 뒤 늦은 의료 입장 거부. 참가자의 의료 상태가 늦게 끝나도 복귀 좌표 저장/배치/종료 잠금을 기다린다. 의료 입장은 운반 해제와 목마 해제를 먼저 공유하고 참가자도 운반 해제 수신 뒤 이동한다. 뒤늦은 운반·목마 신규 연결을 거부한다. GameSettings 수치·공연 씬 배치 변경 없음.
- 첫33개 검사에서 새 씬 준비7개 실패와 목마 해제 이동복원5개 회귀 발견. 테스트 러너 소유 씬의 이름만 임시 변경/복원하고, 치료의 신규 잡기 차단을 실제 기물 이동 소유권 판정과 분리해 수정. 재컴파일 전 재실행은 옛33개 결과여서 최신 결과로 집계하지 않음. 최신17개 추가 회귀 검사를 포함해 재검증 중.
- 최종 치료36/36·공통618/618 통과(36은618에 포함), 신규17사례. 실제 플레이는 사용자 확인. 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS. 공통 설정 연결도 PASS. 결과 `.harness/local/nov13-medical-fix-common-results.json`, `nov13-medical-fix-focused-results.json`.
- 제출 직전 Unity 정상 종료 후 origin/dev 1764a21c 재확인·반영(추가 변경 없음). 기존 Start/폰트/다른 작업 변경은 보존하고 이번 코드·검사·기록 경로만 저장한다.
- 수정 커밋39e1418b를 [PR355](https://github.com/gaboja-studio/POC_CURTAINCALL/pull/355)에 제출하고 MoHoDu 검토 요청. GitHub 연결 도구의 PR 생성은403(앱 권한 없음)으로 실패하여 gh 로그인 계정으로 생성. new-pr 미리보기의 기존 미커밋 파일 경고는 다른 작업 보존을 위해 명시 경로만 커밋하는 절차로 처리했다. dev 병합은 하지 않음.

## 11/13 PR355 이후 상태 조합 추가 감사
- 기존 변경을 `.harness/local/nov13-post355-before`에 백업하고 Unity 정상 종료 후 origin/dev fetch/반영(추가 변경 없음). 현재 작업본은 PR355의9ed54d30, DEV는1764a21c. doctor는 기존 Task ID 중복 실패. 구역 이동·리허설 시작/종료·목마/운반/절단 상태 조사이며 실제 플레이와 새 코드 수정/제출은 하지 않는다.
- 추가 결함2건 재현: 시작 배치 전 트램펄린 Grab 승인으로 배치/놓기 잠금(대표12조합), 수면 중 외줄 재연습 이동 후 침대 시점/입력/누운 외형 잔류(3회+제안 자격 확인). 코드/설정 수정·제출 없음. 기존 관련237개 통과; 첫 리허설2실패는 파괴된 임시 HUD singleton 참조를 정리한 뒤 소스 변경 없이 통과. 상세: Inventory/post355-transition-audit.md. 실제 플레이는 사용자 확인.
- 마무리 check-work 안전성·문서/규칙·컴파일·공통 설정 PASS. 기존237검사는 별도 실행. todo 길이 한도는 완료 기록 원문을 History/018에 보존해 해결; 기존 문서 WARN70/hard0. Start dirty0, 사용자 씬/폰트 백업 SHA256 일치. 새 수정·커밋·push 없음.

## 11/13 리허설 배치·수면 인계 수정
- 사용자 수정/저장/제출 요청. PR355 반영된 origin/dev 4bf66bab에서 fix/rehearsal-entry-handoffs 생성; 기존 미저장 변경 백업·보존. doctor는 기존 Task ID 중복 실패, Unity/도구 정상.
- 트램펄린 요청에 기존 배치 완료 검사를 연결. 외줄/대포 시작 때 수면 준비·시점·입력/외형 정리, 호스트 늦은 수면 요청/밤 전환 보호. 정상 수면의 마지막 준비자 Dawn 시점 유지.
- 신규68/68 통과: 운반45, 수면 이동16, 원격2, 호스트/밤4, 정상Dawn1. 초기 테스트 이름 충돌·DontSave 임시 객체 준비 문제 수정 후 통과. 공통 GameSettings·씬/프리팹·신규 런타임 스크립트 변경 없음. 실제 플레이는 사용자 확인.
- 제출 전 Unity 정상 종료 후 origin/dev fetch/merge: 4bf66bab 최신, 추가 변경 없음. 사용자 폰트 보존, 테스트로 생성된 CommonButtons 글꼴 캐시만 원래 추적 상태로 복원. 최종 공통 검사 진행.
- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=PASS. 공통686/686(신규68 포함), 실패/생략0. 공통 설정 연결 PASS. 기존 문서 WARN70/hard0 유지. 실제 플레이 미실행.
- 저장/제출: 수정4b4ec89a, PR356 https://github.com/gaboja-studio/POC_CURTAINCALL/pull/356. GitHub 연결 도구 PR 생성은403 권한 부족으로 로그인된 gh를 사용. PM 팀·MoHoDu 검토 요청. dev 병합/실제 플레이 미실행. 기존 사용자 변경은 제출 제외.

## 11/13 PR356 이후 추가 검사
- 원래 feat/exit-route/pursuer 27a7a1f8와 깨끗한 UnderstagePursuerPlaytest 씬을 확인하고 Unity 정상 종료. 사용자 저장 작업을 원래 브랜치에 보존. origin/dev fetch 후9a5baaa7(PR358 포함)에서 fix/common-action-audit 생성. doctor는 기존 Task ID 중복만 실패.
- 공통/대기실 행동 전환 감사, 탈출로 제외. 실제 플레이·게임 코드 수정·제출 없이 재현 조사와 기록만 수행.
- 최신 DEV의 공통686/686 통과. 자동 반환이 치료 입장을 우회하는 문제3/3, 포획 중 외줄/대포 재연습 배치 뒤 모션·입력 잔류 각3/3 재현. [상세](Inventory/post356-transition-audit.md). 치료 문제는 운반 소유 해제 후 입장이 회복됨. 목마 자동 반환은 이번 배치에서 거부되어 결함으로 집계하지 않음.
- check-work: 안전성·지식 검사 통과, 기존 DEV Task115 log154줄이 제한130줄을 초과하여 문서 검사 실패. 해당 검사 단계의 컴파일은 생략, 별도 공통686검사는 완료. Assets/Packages/ProjectSettings 변경 없음, 원래 열린 씬 dirty=false 확인. 검사 기록만 저장하고 제출하지 않음.

## 11/13 PR356 이후 재현2건 수정
- 사용자 수정·저장·제출 승인. Unity 종료 후 DEV 9a5baaa7 fetch/반영(동일). doctor는 기존 Task ID 중복만 실패, 구현 도구/Unity 연결 정상.
- 자동 반환은 일반 잡기 자격을 공유하고 치료/퇴실·목마 위·포획 등 점유를 우회하지 않음. 포획 중 재연습 시작 보류와 늦은 배치/복귀점 저장 차단. 외줄 활성화 직전 재검사와 전원 은퇴 뒤 빈 투표 정리 포함. GameSettings 수치·씬·에셋 변경 없음.
- 신규22/22·컴파일·공통 연결 검사 통과. 기존 Task115 로그 길이 초과는 과거35줄을 History/004에 원문 보존하고 링크하여 정리. 실제 플레이는 사용자 확인.
- 공통708/708 통과(신규22 포함), 실패·생략0. 결과 `.harness/local/post356-fix-common-results.json`. 실제 다인 플레이 결과와 구분.
- 제출 전 Unity 정상 종료·DEV fetch/반영: 9a5baaa7 동일. 종료로 생성된 폰트 캐시만 사전 백업으로 복원. 원래 열려 있던 씬은 저장 변경 없이 유지.

- 2026-11-13 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
