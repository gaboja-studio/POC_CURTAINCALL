# Handoff

## 지금 상태

- 단계: 1단계(W/S 앞뒤 이동) 완료, 2단계(디버깅 표시) 시작 전. 9단계 순서는 `plan.md` "단계".
- 동작하는 것: `PlayerControls.inputactions`(Common 맵 6개 액션) → `PlayerInputReader.Current`(PlayerCommand.Move) → `PlayerMover`가 코스 방향(Destination 또는 Course Direction) 기준으로 W/S 이동. 작업자 플레이 검증 통과(2026-10-01).
- 테스트 씬: 바닥·카메라·Player 프리팹(`Assets/Resources/Prefabs/Characters/Players/Player.prefab`) 배치.
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. 2단계: 명령 확인 HUD(기능 코드와 분리된 디버그 표시).
2. 이후 `plan.md` 단계 3~9. 균형·점프·옆줄은 명령+디버그까지만(실제 동작은 005).

## PM이 확인할 것

- 확인 요청: 005 `plan.md`의 "균형 값" 부분을 004 결과(무너짐 신호 받아 낙하 처리)로 갱신 (`Harness/Project/Decisions/tightrope-balance.md`)
- 대기 중인 결정: 없음
- 공용 파일 요청: 없음

## Verification

- 구역 검사: save-work에서 실행
- 1 문서/규칙: save-work에서 실행
- 2 컴파일: save-work에서 실행
- 3 테스트: NO_PROJECT_TESTS (테스트 어셈블리 없음, 결정)
- 4 실행 로그: 미실행
- 5 공동 테스트: 미요청 (1단계 W/S 이동은 작업자 플레이로 확인)
