# Log

특별한 문제·해결·검사 결과·다음 할 일·PR 링크만 짧게 남긴다.

## 2026-11-04 원인·수정
- 재현: WaitingRoom.unity 플레이 → `DebugStart`·4단계 강제·가짜 대상(PlayerMover). 추격 전환 직후 대부분이 `pathPending=True, hasPath=False`로 Chase/Run 제자리. 순찰 단독은 정지 0건.
- 원인1: `Navigate`가 0.2초마다 `SetDestination` 재요청 → 비동기 경로 예산(100 iter/frame)을 26체가 공유하며 매번 초기화. 동기 `CalculatePath`는 같은 위치에서 성공.
- 원인2: `PursuitDoorObstacle`가 매 프레임 center/size 재대입 → NavMesh 재깎기 → 경로 무효화 → autoRepath 비동기 대기.
- 수정 중 함정: MonoBehaviour 필드 초기화에서 `new NavMeshPath()` 호출 시 생성자가 실패해 뒤 필드(`poseBuffer` 등)가 null → ResetActor NRE. 지연 생성으로 해결.
- 추가 수정: 방 밖 추격이 꺼진 추격자가 최인접 단계에서 방 밖 대상을 놓지 않고 마지막 지점에서 달리기 → 대상이 방을 나가면 놓치고 수색. 막힌 길 끝 도착 판정 1.5m.
- NavMesh 최신 여부: 현재 씬으로 메모리 재베이크 비교 시 삼각형 3518·충돌체 겹침 166 동일 → 재베이크 불필요(겹침은 문 등 움직이는 차단물·측정 오차).
- 수정 후 4단계·대상 6곳 순환 78초: pathPending 0/100,698 에이전트 프레임, 0.5초 이상 연속 정지 0체, 26체 합계 제자리 걸음 4.8초(대상 주변 몰림).
- 수정 전 같은 조건: 추격 전환 직후 26체 중 다수가 2.3초 이상(측정 시작부터 계속) 경로 없이 정지.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-04 길찾기 부하 개선(사용자 요청: 순찰은 1회, 추격만 지속)
- 측정 조건: WaitingRoom 플레이, 4단계 강제, 가짜 플레이어 4명(PlayerMover) 10초마다 위치 교대, 약 40초. `PursuitNavigation` 계측(계산 수·ms·프레임 최댓값).
- 개선 전: 226회/초, 평균 0.36ms/프레임, 최악 11.97ms(한 프레임 88회), 2ms 초과 116/2093프레임, 5ms 초과 57.
- 1차(고정 목표 1회·추격 0.4초±25%·대상 0.3~1m 이동·프레임 상한 8): 134회/초, 최악 2.1ms/8회, 평균 0.43ms(남은 계산이 장거리라 1회 비용 증가).
- 2차(+최인접 선택 직선거리 하한 가지치기, 먼 대상 재계산 이동 기준 최대 3m): 51회/초, 평균 0.047ms, 최악 1.33ms/8회, 2ms 초과 0, 정지 0체, 제자리 걸음 합계 2.8초.
- 문 변경 신호: 평상시 Version 고정(매 프레임 무효화 없음). Invalidate 시 26체 재계산이 프레임당 최대 8회·0.09ms로 분산. 수동 충돌체 토글은 ExplorationGate가 매 프레임 덮어써서 직접 신호로 확인.
- 네트워크: 길찾기는 호스트만 수행, 스냅샷 주기·크기 무관.

## 검사
- 1단계(이탈 제한 30m) 복귀: 4단계에서 몰린 17체가 PathComplete 경로로 순찰 복귀, 제자리 걸음 합계 0.5초.
- EditMode `Pursuit` 62/62 PASS. PlayMode `Pursuit` 22개 중 21 PASS, `PursuitActivation_StaggeredStages…` 1건은 첫 테스트의 런 실행(`LastLaunchSucceeded`) 준비 단계 실패 → 단독 재실행 PASS.
- 플레이 측정 부산물로 Unity가 폰트 SDF 2개·escape-labels·머티리얼 3개를 수정함(작업 범위 아님, 커밋 제외 대상).
- 부하 개선 후 재검사: EditMode `Pursuit` 62/62 PASS. PlayMode `Pursuit` 22개 중 21 PASS, 같은 `PursuitActivation_StaggeredStages…`가 다시 카메라 테스트 직후 런 실행 준비(`LastLaunchSucceeded`, 3.3초) 단계에서 실패 → 단독 재실행 PASS. 실행 순서 의존으로 보이며 길찾기 코드 이전 단계. dev 기준 일괄 실행 비교는 미실시.
- check-work: 안전성·문서/규칙·컴파일 PASS, 테스트는 위 수동 실행.
- 다음: 실제 플레이어로 사람 확인, 다인(4인) 호스트 실기에서 프레임 확인.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 2026-11-04 dev 병합: 같은 번호 Task-20261104-101(pursuit-eye-art)이 dev에 먼저 들어와 이 Task를 102로 변경.

- 2026-11-04 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-04 PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/285 (fix/pursuit/systems → dev)
