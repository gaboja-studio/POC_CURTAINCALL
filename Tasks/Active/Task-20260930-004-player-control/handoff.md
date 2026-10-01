# Handoff

## 지금 상태

- 단계: 1~12단계 구현 완료(작업자 플레이 확인). 다음은 검사 → 공동 테스트 → 제출. 12단계 순서는 `plan.md` "단계".
- 구조: 키 → `PlayerInputReader`(`PlayerInputFrame` 공통 역할) → `PlayerController` → 조작 규칙 `TightropeControlScheme` → 동작 부품 `PlayerMover`(코스 이동·점프 1.0m·옆줄 점프 1.5m, 공중 움직임 고정, `LaneJumpFilter`) / `PlayerInteraction`(F 짧게 상호작용·길게 해제 신호) / `PlayerModelSlot`(모델 교체, 키 1.73m 맞춤, 테스트 모델 `Models/DefaultCapsule`·`BlockDoll`) / `PlayerBalance`(±100, 흔들림·가속·목마 배율·위층 전달·착지 충격·빨강 2초 추락 `Fell`). 규칙: `Harness/Project/Decisions/player-control-architecture.md`.
- UI: `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab`(TMP, 생성 메뉴 Tools/CurtainCall). HUD: P 표시, B 균형 켜기/끄기, R 재시작(추락→조작 잠금은 HUD가 테스트용 연결).
- 막힌 것: 없음 / PR: 없음

## 다음 할 일

1. 검사("검사해줘") 결과 확인.
2. 공동 테스트 3항목 작업자 플레이 → 제출("제출해줘").

## PM이 확인할 것

- 확인 요청: 005 `plan.md`의 "점프·옆줄 이동"을 004 움직임 사용으로 갱신(005는 `LaneJumpFilter`·착지 판정·`SetLaneLandingReduction`만). 005·006 `plan.md`에 rope_balance 결정 반영 (`tightrope-balance.md`, `tightrope-rules.md` 2·7번)
- 대기 중인 결정: 옆줄 합체 시 착지자가 동료 위로 올라가는지(가정), F 길게 '해체' 범위(006에서 결정)
- 공용 파일 요청: 통합 plan.md "함께 쓰는 파일" 표에 `Assets/Resources/Prefabs/UIs/Player/` → 플레이어 조작 추가 (setup.md에는 반영됨)

## Verification

- 구역·문서·컴파일: save-work 실행 (아래 커밋)
- 3 테스트: NO_PROJECT_TESTS (테스트 어셈블리 없음, 결정)
- 4 실행 로그: 미실행 / 5 공동 테스트: 미요청 (단계별 작업자 플레이 확인: 1~4단계 통과)
