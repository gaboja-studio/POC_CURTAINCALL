# Handoff

## 지금 상태

- 단계: 1~3단계 완료, 4단계(균형 게이지) 3색 버전 동작 확인 → 기획 문서 반영 수정 전. 12단계 순서는 `plan.md` "단계".
- 동작하는 것: 입력 에셋 → `PlayerInputReader.Current`(Move, Posture) → `PlayerMover` 코스 방향 W/S 이동. 명령 HUD(P 토글). `PlayerBalance`(A/D로 바늘, 아직 -1~1·3색) + `BalanceGaugeView` 하단 게이지. 프리팹에 PlayerBalance, 씬 DebugHud에 게이지 부착.
- 균형 규칙: `Harness/Project/Decisions/tightrope-balance.md`, 수치: `Docs/References/tightrope-balance-summary.md`(원문 `rope_balance.doc`).
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. 4단계 수정: 균형값 ±100, 초록 ±40·빨강(노랑 제거), A/D 60/초, CharacterController 키 1.73m.
2. 5단계: 자연 흔들림(이동 15/정지 5, 방향 1~2초 랜덤) + 기울기 가속(균형값 × 0.5).
3. 이후 `plan.md` 6~12단계. 점프·옆줄·목마 실제 동작은 005·006, 004는 균형 입력 함수까지.

## PM이 확인할 것

- 확인 요청: 005·006 `plan.md`에 rope_balance 결정 반영 (`tightrope-balance.md`, `tightrope-rules.md` 2·7번)
- 대기 중인 결정: 옆줄 합체 시 착지자가 동료 위로 올라가는지(가정), F 길게 '해체' 범위(006에서 결정)
- 공용 파일 요청: 없음

## Verification

- 구역·문서·컴파일: save-work 실행 (아래 커밋)
- 3 테스트: NO_PROJECT_TESTS (테스트 어셈블리 없음, 결정)
- 4 실행 로그: 미실행 / 5 공동 테스트: 미요청 (단계별 작업자 플레이 확인: 1~4단계 통과)
