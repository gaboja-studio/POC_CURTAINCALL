# 043 착수와 공용 생존 기준 변경

## 승인·수신

- 2026-10-16 사용자: "PM에게 해당 문제에 대해서 내 임의로 공동 시스템의 실패 규칙을 수정하는 것을 허용 받았어. 먼저 공동 시스템에서 사용자 두명 미만의 실패 규칙을 한명 미만으로 수정 후 우리 작업 다시 시작하자".
- 정식 공통 기준을 모든 묘기에 1명으로 변경하는 명시적 요청이다. 기존 run-rules/finale-result/setup의 전역 2명 유지·공용 수정 금지보다 최신 사용자 지시가 우선한다. 게임 시작 최소 인원은 요청 대상이 아니다.
- 사용자 직접 브랜치 전환/병합 후 HEAD0128623에서 로컬/원격 일치, 부모204be8c·dev d8b6db2·042 #111/75862ef 모두 포함, 부모 tracked tree 일치·미커밋0 확인.
- 기존 PM 순차 인계 위임으로 042 integrated·Allowed 반환, 043 working/Active Task/코드·씬 단독 이관. 승인 범위의 공용4파일을 setup에 명시했다. 별도 commit/push는 하지 않았다.
- start-work 스크립트는 옛 원격 scaffolded gate 때문에 실행하지 않았다. 기술 수신·문서 전환 사실과 자동 gate PASS를 구분한다.

## 구현

- PerformanceRewards.OfficialMinSurvivors=1을 자동 실패와 최종 정산이 함께 사용한다. 정식/Dev 모두 생존1명은 계속·성공 가능, 생존0명이면 NoSurvivors 실패·무보상.
- 생존자 집계는 기존과 같이 살아 있고 연결된 참가자 기준이다. 마지막 생존자 연결 종료도0명 실패이며 사망 명부와는 구분한다.
- DevModeTestUI도 같은 상수를 표시한다. DevMode 설명은 시작 인원 예외와 현재 생존 규칙에 맞췄다. 시작 최소2명/Dev1명, 보상 수치, 부상·관전·시간대 전환 코드는 변경하지 않았다.
- 기존 DummyPerformancePlayTests에서 EditorPrefs를 건드리지 않고 DevMode의 메모리 캐시만 false로 설정/복구한다. 정식 모드1명 성공·정산과0명 자동 실패를 실제 공용 API로 검증한다.
- 기존 generic PerformanceRewardModel의 호출별 minimumSurvivors 인자·min2 테스트는 그대로 보존했다.

## 검증

- doctor: FAIL0/WARN3(CLI 버전 차이·에디터 탐지·skill links). 최초 Pipeline 미연결 → 사용자가 Unity 실행 후 정확한 프로젝트/6000.3.25f1/Stop/씬 dirty0 확인.
- check-work -Mode PlayMode: scope FAIL8, 후속 문서/컴파일/테스트 SKIPPED. 통합 관리3·042 문서4·043 setup1이며 기존 순차 위임 범위다. 공용 구현4파일은 setup 구역에서 차단되지 않았다. 검사 우회/스크립트 수정 없음.
- 별도 verify-fast: PASS, hard0. git diff --check PASS.
- 직접 Pipeline recompile/recompile_status: up_to_date, failed=false, compilationFailed=false, errors=[] PASS.
- PlayMode DummyPerformancePlayTests: 6/6 PASS, 실패/건너뜀0. 두 명 정상 도착의 중복 종료 방지, 부상 후 시간초과 실패, 한 명 사망/이탈 뒤 생존1명 성공·보상, 전멸/마지막 생존자 이탈 자동 실패·0원·RestTime 전환 검증.
- 전체 EditMode: 327/327 PASS, 실패/건너뜀0. 보상 모델30개, CannonLanding10개, RouteCourse18개 포함.
- 테스트 후 Console 오류0/경고0. HumanCannon.unity 한 씬/루트12/dirty0, Play stopped. 코드/문서 외 Unity 에셋 변경 없음.
- 결과 원본: 로컬 ignored .harness/local/task043-survivor-playmode.json, task043-editmode.json. 실제 네트워크4인/사람 조작 결과는 아니다.

## 043 다음 범위

- 쿠션 안전 착지·불꽃 종료 경계는 040~042 구현을 재사용한다. 위 EditMode로 실제 저장 씬 배치/착지 경계는 확인했으며 부상 유지·회복/부활/성공 없음의 사람 확인은 별도다.
- 최종 대포4대·공유 톱 주기·단독 완주 경로·실제 목적지와 공용 결과/보상/런 종료 연결은 미구현. 공용 생존 기준 변경만으로 043 완료/통합 종료 PASS 처리하지 않는다.
- 기존 합의대로 기능 하나씩 구현·사용자 확인. 실제4인 공동 검증은 전체 통합 후 계획 유지.
- PM 지식/가이드 갱신 제안: run-rules·finale-result·보상 가이드의 기존2명 규칙 설명은 최신 승인으로 대체할 필요가 있다. 이 작업에서는 Task에 명시 기록했다.
