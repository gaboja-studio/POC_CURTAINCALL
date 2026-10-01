# Handoff

## 지금 상태

- 단계: 1~14단계 구현·작업자 플레이 확인(13 몸 기울기 `BalanceTiltView`, 14 추락 래그돌 `PlayerRagdoll`). 공동 테스트 통과. 제출 단계. 12단계 순서는 `plan.md` "단계".
- 구조: 키 → `PlayerInputReader`(`PlayerInputFrame` 공통 역할) → `PlayerController` → 조작 규칙 `TightropeControlScheme` → 동작 부품 `PlayerMover`(코스 이동·점프 1.0m·옆줄 점프 1.5m, 공중 움직임 고정, `LaneJumpFilter`) / `PlayerInteraction`(F 짧게 상호작용·길게 해제 신호) / `PlayerModelSlot`(모델 교체, 키 1.73m 맞춤, 테스트 모델 `Models/DefaultCapsule`·`BlockDoll`) / `PlayerBalance`(±100, 흔들림·가속·목마 배율·위층 전달·착지 충격·빨강 2초 추락 `Fell`). 규칙: `Harness/Project/Decisions/player-control-architecture.md`.
- UI: `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab`(TMP, 생성 메뉴 Tools/CurtainCall). HUD: P 표시, B 균형 켜기/끄기, R 재시작(추락→조작 잠금은 HUD가 테스트용 연결).
- 막힌 것: 없음 / PR: 없음

## 다음 할 일

1. PR 제출 후 PM 병합·공동 테스트 대기.
2. 연결 작업(009·010)에서 005·006이 004 공개 함수(`SetBalanceActive`, `Fell`, `LaneJumpFilter`, `SetLaneLandingReduction`, `SetStackSize`, `SetUpperBalanceSum`, `InteractRequested`/`ReleaseRequested`, `SetScheme`)를 붙임.

## PM이 확인할 것

- 확인 요청: 005 `plan.md`의 "점프·옆줄 이동"을 004 움직임 사용으로 갱신(005는 `LaneJumpFilter`·착지 판정·`SetLaneLandingReduction`만). 005·006 `plan.md`에 rope_balance 결정 반영 (`tightrope-balance.md`, `tightrope-rules.md` 2·7번)
- 대기 중인 결정: 옆줄 합체 시 착지자가 동료 위로 올라가는지(가정), F 길게 '해체' 범위(006에서 결정)
- 공용 파일 요청: 통합 plan.md "함께 쓰는 파일" 표에 `Assets/Resources/Prefabs/UIs/Player/` → 플레이어 조작 추가 (setup.md에는 반영됨)

## Verification

- 구역 PASS · 1 문서/규칙 PASS · 2 컴파일 PASS (10-02, check-work)
- 3 테스트: NO_PROJECT_TESTS (10-02, 열린 에디터 list_tests 0개). check-work는 FAIL로 표시 — 에디터가 열려 있어 unity test 거부(Pitfalls/unity-test-editor-open.md, 메인 체크아웃에만 있음). 테스트 어셈블리 없음은 결정.
- 4 실행 로그: SKIPPED(미실행) / 5 공동 테스트: 작업자 플레이 3항목 모두 통과 (10-02, 1 공통 역할·2 F 짧게/길게·3 모델 교체)
