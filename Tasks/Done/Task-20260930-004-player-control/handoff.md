# Handoff

## 지금 상태

- 단계: done — PR 병합·공동 테스트 기록 확인 후 2026-10-05 PM 요청으로 Done 보관. dev 반영은 통합 종료 때 별도로 진행.
- 구조: 키 → `PlayerInputReader`(`PlayerInputFrame` 공통 역할) → `PlayerController` → 조작 규칙 `TightropeControlScheme` → 동작 부품 `PlayerMover`(코스 이동·점프 1.0m·옆줄 점프 1.5m, 공중 움직임 고정, `LaneJumpFilter`) / `PlayerInteraction`(F 짧게 상호작용·길게 해제 신호) / `PlayerModelSlot`(모델 교체, 키 1.73m 맞춤, 테스트 모델 `Models/DefaultCapsule`·`BlockDoll`) / `PlayerBalance`(±100, 흔들림·가속·목마 배율·위층 전달·착지 충격·빨강 2초 추락 `Fell`). 규칙: `Harness/Project/Decisions/player-control-architecture.md`.
- UI: `Assets/Resources/Prefabs/UIs/Player/BalanceGauge.prefab`(TMP, 생성 메뉴 Tools/CurtainCall). HUD: P 표시, B 균형 켜기/끄기, R 재시작(추락→조작 잠금은 HUD가 테스트용 연결).
- 막힌 것: 없음
- PR: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/7

## 다음 할 일

- 이 Task의 구현·제출·병합 대기는 종료했다. 아래 목록은 이전 인계 기록이며, 통합 전체의 dev 반영·테스트 씬 처리 결정은 PM이 통합 종료 때 진행한다.

1. PR #7 제출 완료(10-02). PM 병합·공동 테스트 대기.
2. 연결 작업(009·010)에서 005·006이 004 공개 함수(`SetBalanceActive`, `Fell`, `LaneJumpFilter`, `SetLaneLandingReduction`, `SetStackSize`, `SetUpperBalanceSum`, `InteractRequested`/`ReleaseRequested`, `SetScheme`)를 붙임.

## PM이 확인할 것

- 확인 요청: 005 `plan.md`의 "점프·옆줄 이동"을 004 움직임 사용으로 갱신(005는 `LaneJumpFilter`·착지 판정·`SetLaneLandingReduction`만). 005·006 `plan.md`에 rope_balance 결정 반영 (`tightrope-balance.md`, `tightrope-rules.md` 2·7번)
- 대기 중인 결정: 옆줄 합체 시 착지자가 동료 위로 올라가는지(가정), F 길게 '해체' 범위(006에서 결정)
- 공용 파일 요청: 통합 plan.md "함께 쓰는 파일" 표에 `Assets/Resources/Prefabs/UIs/Player/` → 플레이어 조작 추가 (setup.md에는 반영됨)

## Verification

- 구역 검사: PASS (10-02, check-work)
- 1 문서/규칙: PASS (10-02, check-work)
- 2 컴파일: PASS (10-02, check-work, 통합 브랜치 병합 후 재확인)
- 3 테스트: NO_PROJECT_TESTS (10-02, 열린 에디터 list_tests 0개, 테스트 어셈블리 없음은 결정). check-work 표시는 FAIL — 에디터가 열려 있으면 unity test가 거부됨(unity-cli 함정)
- 4 실행 로그: SKIPPED(미실행)
- 5 공동 테스트: 작업자 플레이 3항목 모두 통과 (10-02)
