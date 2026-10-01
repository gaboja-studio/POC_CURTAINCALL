# Handoff

## 지금 상태

- 단계: 1~5단계 완료(작업자 플레이 확인). 다음은 6단계. 12단계 순서는 `plan.md` "단계".
- 동작하는 것: 입력 → `PlayerInputReader.Current`(Move, Posture) → `PlayerMover` 코스 방향 이동. `PlayerBalance`(±100, 초록 ±40, A/D 60, 자연 흔들림 이동 15/정지 5, 기울기 가속 0.5, `SetBalanceActive` 켜기/끄기). 게이지는 Canvas 프리팹 `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab`(TMP, 임시 폰트 Pretendard-Medium, 생성 메뉴 Tools/CurtainCall). 명령 HUD(P 표시, B 균형 켜기/끄기). 프리팹: 키 1.73m, 모델은 `Model` 자식.
- 균형 규칙: `Harness/Project/Decisions/tightrope-balance.md`, 수치: `Docs/References/tightrope-balance-summary.md`.
- 막힌 것: 없음 / PR: 없음

## 다음 할 일

1. 6단계: 빨강 2초 → 추락 신호(이벤트), 게이지에 빨강 체류 시간, 초록 복귀 시 초기화, 디버그 재시작 키.
2. 이후 7~12단계. 점프·옆줄·목마 실제 동작은 005·006, 004는 균형 입력 함수까지.

## PM이 확인할 것

- 확인 요청: 005·006 `plan.md`에 rope_balance 결정 반영 (`tightrope-balance.md`, `tightrope-rules.md` 2·7번)
- 대기 중인 결정: 옆줄 합체 시 착지자가 동료 위로 올라가는지(가정), F 길게 '해체' 범위(006에서 결정)
- 공용 파일 요청: 통합 plan.md "함께 쓰는 파일" 표에 `Assets/Resources/Prefabs/UIs/Player/` → 플레이어 조작 추가 (setup.md에는 반영됨)

## Verification

- 구역·문서·컴파일: save-work 실행 (아래 커밋)
- 3 테스트: NO_PROJECT_TESTS (테스트 어셈블리 없음, 결정)
- 4 실행 로그: 미실행 / 5 공동 테스트: 미요청 (단계별 작업자 플레이 확인: 1~4단계 통과)
