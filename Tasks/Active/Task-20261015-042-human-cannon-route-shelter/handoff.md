# Handoff

## 지금 상태

- human-verify / check-work — 042 전체 구현·초기 정상 피드백 후 1인 양문 함정, 첫 출발 안전 복귀, 최신 자동 승강/쉼터 낙차 요청까지 반영. 최신 변경의 사람 확인은 남았다.
- 041 #108 MERGED(292667c)를 042 착수 기준 dae56af가 수신. 현재 042만 코드·씬 소유: [순차 인계 위임](../../../Harness/Project/Decisions/Human-cannon/sequential-handoff.md).
- 구현: 모든 리프트는 트램폴린을 든 1명만1.5초 자동 출발. 조건 충족 대기/이동만 초록·미충족/취소/도착 파랑. 놓기/이탈/과승 취소·2인 양문 함정 유지. 첫 출발4m/안전 바닥-4m/계단, 쉼터6m/측면7m 틈.
- 사용자 선택: 쉼터는 실제 트램폴린 담당자를 제외한 전원 도착 조건 유지·스위치/F 출발 제거. 최신 요청으로 장치 소지 조건 추가. 운송5초/승강3초·양문0.35초/복귀1.2초·불꽃15초/0.7m/s는 시험값.
- 2026-10-16 다른 PC 재개용 저장 묶음: feat/human-cannon-route-shelter. [다른 PC 재개](History/20261016-pc-resume.md)를 읽고 원격 최신 커밋을 받는다. PR·병합 미실행, 제출 대상 integration/main-stage-human-cannon(Squash).

## 다음 할 일 — 사람 확인 최대 3묶음

1. 빈손 탑승→파랑/대기, 트램폴린 들기→초록/1.5초 자동 이동, 대기 중 놓기/하차 또는 도착→파랑. 2명→양문 함정·안전 바닥/계단 복귀. 나머지2대·이동 중 과승도 확인.
2. 비행자 대포 F→F·첫 받기→1명 미합류 시 쉼터 승강 차단→전원 합류/1.5초 자동 상승. 쉼터 측면에서 하부 발판으로 점프→낙사, 교대·부상 접근 확인.
3. 불꽃 전진·쉼터/쿠션 경계 종료·최종 구간 별도 타이머 없음. 이동 중 R·원격 담당/재접속 확인.

- [setup](setup.md)·[plan](plan.md)·[색상](History/20261016-lift-color-verification.md)·[소지 조건](History/20261016-carrier-lift-verification.md)·[자동 승강](History/20261016-auto-lift-verification.md)·[양문](History/20261016-lift-verification.md)·[기존](History/20261016-verification.md). 실제4인은 전체 통합 후, 041 미검증 유지.
- 자동 scope FAIL13: 위임된 전환 문서8개+기존 폰트·MPPM·PlayMode5개. 저장은 승인된 구현/전환 문서만 선택, 기존5개는 로컬 보존·제외. 수동 감사에서 그 외 구역 밖 변경 없음.
- start-work 미실행·원격 gate/dirty PASS 아님. 지식 제안: 최신 자동 승강/쉼터 낙차는 사용자 확정 결정. 고정 바닥/안전 착지 일치·합성 원격 보간 등 검증 주의점은 상세 기록.

## Verification

- 환경: doctor FAIL0/WARN2(최초1회), Unity6000.3.25f1·정확한 프로젝트 확인.
- 0 구역: check-work -Mode PlayMode FAIL13, 후속 SKIPPED. 위임/기존 변경은 수동 감사로 구분.
- 1 문서: 별도 verify-fast PASS(hard0/기존 범위 밖 WARN5). diff --check PASS.
- 2 컴파일: 일괄 SKIPPED(구역 실패); 별도 verify-unity -Compile PASS(completed). 최신 색상 조건 반영 후 확인.
- 3 로직: 최신 EditMode269/269 PASS, 실패/건너뜀0. PlayMode NO_PROJECT_TESTS. 단일 호스트 합성 배치로3대 빈손 파랑·소지 대기 초록·놓기 파랑·재소지/이동 초록·도착 파랑 확인.
- 4 입력/로그: 최신 색상/소지 조건의 실제 입력 확인은 미실행. 이전 가상 A+Space 낙사 증거는 자동 승강 기록 참조. 합성 배치/Grab·Drop 직접 호출은 실제 입력·원격 동기화 검증이 아니다.
- 로그: .harness/local/task042-lift-color-runtime-console.json(109279~109327). 새 게임 Console 오류0. 최신 색상/소지 조건과 실제 원격 동기화의 사람 확인은 남았다.
- 에디터 Stop·dirty0·Missing Script0·임시 객체 제거·공용 설정 변경 없음. 실제4인 미실행이며 일시·역할·commit·예상/실제·영상/로그를 plan에 맞춰 기록한다.
