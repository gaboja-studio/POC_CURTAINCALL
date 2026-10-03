# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 구현 끝(todo 구현 항목 완료, "011 plan에 알리기"만 PM 요청 대기). 2026-10-02 중간 저장. 남은 것: #17 조사·수정, 공동 테스트 결과 정리, 검사·제출.
- 동작하는 것(작업자 플레이 확인 통과):
  - 코스 `TightropeCourse.prefab`(공용 폴더) — 인스펙터 값대로 4줄→1줄 직선 코스, Play 중 값 변경 즉시 재생성, 줄 간격 = 모든 플레이어 옆줄 점프 거리(#14), 줄 아래 1m 떨어지면 추락(#15).
  - 플랫폼(시작·도착)은 일반 이동(WASD 8방향·Space, 이동 방향 바라봄·카메라 월드 고정), 줄 위에 서면 줄타기 조작+균형 켜짐·줄 중앙으로 맞춤(`CourseControlSwitcher`).
  - 게임 시작: 최소 인원(`MinPlayers`, 테스트 1) 이상이면 호스트가 접속 UI "게임 시작"(`NetworkSessionManager.StartGame()`), 정원 자동 시작 없음.
  - 코스 값: 대기 중 호스트 값 공유, 시작 후 고정(`CourseShapeSync`, NGO 이름 메시지).
  - 묘기 진행 `TightropeRun`(호스트 판정): 2026-10-03 규칙으로 교체(제한시간 5분, 1명 도착 즉시 클리어+`EndGame()`·살아 있는 전원 도착 지점으로, 도착 없이 진행자 0·시간 종료 → 실패 → 3초 뒤 재시작, 묘기 시작, R은 호스트만, 플랫폼 3m/s) — 작업자 플레이 확인 통과(2026-10-03). 디버그 `TightropeRunDebug`(K 추락, L 도착, O 표시, 좌우 착지 표시기).
  - 플레이어끼리: 살아 있으면 줄 위·플랫폼 모두 막힘, 추락하면 몸통 통과·래그돌은 밀림(#16).
- 진입점 — 코스: `TightropeCourse.Current`, `TryGetRopePoint`, `GetDistance`, `GetSide`, `GetLanePosition`, `IsOnRope`, `FindLandingLane`(없으면 `NoLane`), `IsLaneChangeAllowed`, `IsLaneUsable`, `SetLaneBlocked`, `GetStartPose`, `FinishDistance`, `HasReachedFinish`, `Rebuilt`.
  진행: `TightropeRun.Current`, `State`, `StateChanged`, `WinnerSlot`, `RemainingCount`, `ParticipantCount`, `RestartRemaining`, `RunRestarted`.
  플레이어(004 파일, setup 배정): `PlayerMover.FreeMovement`·`SetMoveInput(side, forward)`·`SetStartPose(.., moveNow)`·`LaneSpacing` set·`PassThrough`, 신체 부위 준비 `PlayerCondition`·`BodyPart`·`PlayerControlContext.Condition`(프리팹엔 아직 안 붙임).
- 기획 값(2026-10-03): 도착 100m, 4줄 끝까지, 간격 3m, 여유 2m, 플랫폼 6m, 옆줄 0~100m, 줄 0.2m, 제한 300초. 기획에 없는 임시 값: 출발 -1m, 안전망 6m, 추락 깊이 1m, 재시작 3초, 회전 720°/s, 래그돌 미는 속도 2m/s, 최소 인원 1.
- 주의: 에디터 저장 때 Unity가 `Player.prefab`(004 구역)에 새 필드를 자동 기록할 수 있다 → 구역 검사 FAIL이면 PM 확인 후 되돌린다. MPPM 가상 플레이어는 저장 전에 끈다.
- 이슈: #14·#15·#16 수정됨, #17 흔들림 배율 1.75로 해결. 막힌 것: 없음. PR: 없음

## 다음 할 일

1. (저장 완료 5c68903, 결정 문서·012 plan 갱신 완료 3e65ec4) 진입점: `TightropeRun.TimeRemaining`·`IsPerformanceStarted`·`PerformanceElapsed`·`PerformanceStarted`·`InProgressCount`/`ArrivedCount`/`DeadCount`·`RestartByHost()`, 코스 `GetFinishPose`·`BlockSegment`/`ClearBlockedSegments`/`IsSegmentBlocked`.
2. #17 결론(2026-10-03): 충격 크기는 고정(±35/±10), 연속 옆줄 착지로 흔들림 ×2.5가 계속 갱신되는 게 원인 → PM 결정 A: `Player.prefab` `laneSwayBoost` 1.75. 작업자 재확인 통과, 이슈 닫음.
3. 공동 테스트 1·2·3 작업자 통과·검사 완료(10-03) → "제출해줘".

## PM이 확인할 것

- 공용 파일 요청: `tightrope-balance.md`·균형 요약에 단독 옆줄 흔들림 ×2.5 → ×1.75(#17, 2026-10-03 PM) 기록 · 011 plan에 코스 조회 진입점 기록(위 목록) · 007 테스트 씬 `TestRoundRestart` 오브젝트(Missing Script) 제거 · Domain Map(network-session 자동 시작 제거, player-sync 테스트 부품 삭제, 외줄 코스 새 도메인) 갱신

## Verification

- 구역 검사: PASS (10-03)
- 1 문서/규칙: PASS (10-03)
- 2 컴파일: PASS (10-03)
- 3 테스트: `NO_PROJECT_TESTS` (10-03, Pipeline list_tests 0개. check-work는 에디터가 열려 있어 unity test가 거부돼 FAIL 표시 — 함정 unity-test-editor-open, 테스트 어셈블리 없음 결정)
- 4 실행 로그: 작업자 플레이 확인(위 "동작하는 것")
- 5 공동 테스트: 항목 1·2·3 작업자 플레이 통과(2026-10-03, plan.md), 병합 후 PM 공동 테스트 대기
