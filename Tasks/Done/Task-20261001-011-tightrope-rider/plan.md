# Plan

## Context

외줄타기 묘기에서 캐릭터가 줄 위에서 하는 동작. 005(코스·진행) 위에서 004(입력·균형)를 받아 쓴다.
원문: `Docs/References/tightrope-prototype-brief.md`
요약: `Docs/References/tightrope-rules-summary.md`(2026-10-03 상세 규칙, 우선), `tightrope-keymap-summary.md`, `tightrope-plan-summary.md` · 규칙 결정: `Harness/Project/Decisions/tightrope-course-rules.md`, `tightrope-rules.md`, `tightrope-balance.md`

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 테스트 어셈블리는 만들지 않는다. AI는 컴파일만 검사한다.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

세부(2026-10-03 범위 축소 — 아래 외에는 이미 있음):

- 이미 있음(다시 만들지 않음): 줄 위 걷기·점프·옆줄 점프 움직임(004 `PlayerMover`·`TightropeControlScheme`), 착지·옆줄 착지 충격과 흔들림(004 `PlayerBalance`), 줄 오르내림 때 조작·균형 전환·줄 중앙 맞춤·줄 아래 추락(005 `CourseControlSwitcher`·`TightropeCourse`), 추락 호스트 확정·재시작 복귀(007).
- 남은 것:
  1. **옆줄 점프 판정 연결**: `PlayerMover.LaneJumpFilter`에 005 조회를 붙인다 — `IsLaneChangeAllowed(거리)`, `FindLandingLane(위치, 방향)`이 `NoLane`이 아님, `IsLaneUsable(줄, 거리)`(불타는 구간 포함). 하나라도 아니면 입력 무시.
  2. **뒤쪽 착지**: 착지 자리에 동료가 있으면 코스 진행 방향 뒤쪽으로 보정, 자리가 없으면 착지 실패(목마 합체가 되는 경우는 006이 합체로 바꾼다).
  3. **손잡기(협동 옆줄)**: 도착점이 같은 줄 동료 앞뒤 1.0m 이내면 `PlayerBalance.SetLaneLandingReduction(0.5)`로 착지 충격·흔들림 감소(`tightrope-rules.md`). 거리·감소율은 세팅 칸.
  4. **테스트 부품 정리**: `Network/PlayerSync/TestLaneLanding.cs`를 위 판정으로 대체하고 삭제(PM 배정).
- 판정은 소유자가 하고(내 캐릭터 이동), 결과 위치·추락은 007 공유를 그대로 쓴다.

005 진입점(2026-10-03 병합, 지도 `Docs/Domains/tightrope-course.md`): `TightropeCourse.Current` — `TryGetRopePoint`·`IsOnRope`·`GetDistance`·`GetSide`·`GetLanePosition`·`FindLandingLane`(없으면 `NoLane`)·`IsLaneChangeAllowed`·`IsLaneUsable`(불타는 구간 포함)·`LaneSpacing`. 진행 `TightropeRun.Current` — `State`·`RunRestarted`. 줄 오르내림 조작·균형 전환은 005 `CourseControlSwitcher`가 하므로 겹치게 만들지 않는다.

## Files

- 수정 예정:
- 수정 예정 (모두 `Assets/Scripts/Tightrope/Rider/`): `RopeRider.cs`(줄 위 상태·이동, 공개 진입점), `RopeSideStep.cs`(옆줄 이동·착지 보정), 테스트 씬 `TightropeRider.unity` 배치
- 읽기 전용 참고: `Assets/Scripts/Player/`(004 `PlayerCommand`·`PlayerInputReader`·`PlayerMover`·`PlayerBalance`), `Assets/Scripts/Tightrope/Rope/`(005)

## 004에 필요한 진입점 (004 단계와 대응)

| 필요한 것 | 004 단계 | 2026-10-02 현재 |
|---|---|---|
| 점프·Q/E 방향·옆줄 이동 명령 (`PlayerCommand`) | 8, 9, 10 | 없음 (`Move`, `Posture`만) |
| 균형 무너짐(빨강 2초) 이벤트 | 6 | 없음 |
| 공중 정지 + 착지 충격 넣기, 옆줄 충격(±35 + 3초 ×2.5) 넣기 | 8, 10 | 없음 |
| 균형 켜기/끄기 `SetBalanceActive`, 중앙 복귀 `ResetBalance` | 5 | 있음 |
| `PlayerMover` 이동 끄기/켜기 (줄 위에서 011이 위치를 맡음) | — | **요청 필요** (`enabled` 토글로 충분한지 004에서 확인) |

## Risks

- 004·005 진입점이 바뀌면 이 작업도 따라 바뀐다. 착수 전에 두 진입점 이름을 확인한다.
- `PlayerMover`가 CharacterController로 중력·이동을 계속 적용하면 줄 고정과 충돌한다. 줄 위에서는 반드시 끈다. 004 코드는 고치지 않고, 끄는 방법이 부족하면 PM(004)에 요청한다.
- 004·005 착수 지연이 곧 011 지연이다. 기다리는 동안 004를 먼저 끝낸다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정)
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
2026-10-03 범위 축소로 항목 교체. **작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** (2026-10-01 PM·담당자 @MoHoDu 합의)

1. 옆줄 이동 불가 구간·옆에 줄이 없는 쪽·불타는 줄 쪽으로 Q/E 홀드 + Space를 하면 아무 일도 일어나지 않는다.
2. 넘어갈 자리에 동료가 서 있으면 그 뒤쪽에 착지한다.
3. 동료 1m 이내로 옆줄 착지하면 혼자 착지할 때보다 균형이 덜 흔들린다(손잡기).
