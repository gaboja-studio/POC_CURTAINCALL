# Plan

## Context

기획 상세 규칙(2026-10-03)의 장애물. 원문 요약 `Docs/References/tightrope-rules-summary.md` 5·6장, 결정 `Harness/Project/Decisions/tightrope-course-rules.md` #6~#8, 검토 근거 `Integrations/Active/Integration-tightrope-prototype/rules-review-20261003.md` §1-1·§3.

## Approach

1. `setup.md`의 작업 구역 안에서만 구현.
2. 새 로직은 가능하면 테스트를 함께 남김.
3. 공용 파일이 필요하면 직접 고치지 않고 PM에게 요청.
4. 검사 후 `integration/*`로 제출.

세부(구현 전에 PM과 합의):

- 가로 톱날 22개: 원문 위치에 미리 배치, 게임 시작과 함께 좌우 왕복(맵 경계 밖 3m까지, 끝에서 즉시 반전), 좌측 좌→우·우측 우→좌로 시작, 속도는 첫 톱날 3.0 → 마지막 4.5m/s 직선 증가(인스펙터). 충돌체 지름 1.0m(외형 1.2m), 공격 상단 외줄 윗면 0.2m.
- 수직 톱날: 묘기 시작(005 공개 신호) 1분 후, 진행자가 있는 줄 중 랜덤, 93m → 5m를 0.8m/s, 제거 20초 뒤 다음, 최대 1개. 충돌체 지름 1.0m·상단 1.1m. 톱날 뒤 **발판**(2026-10-03 PM: 임시, 나중에 형태 교체 가능 — "눌림 판정"과 "보이는 모양"을 분리)을 밟으면 톱날이 **0.1~0.3초(기본 0.2초) 안에 빠르게** 내려간다. 눌림 상태는 호스트가 확정해 공유.
- 충돌은 플레이어 캐릭터와만(톱날끼리·외줄과는 통과). **닿으면 부위 절단**(2026-10-03 PM, 규칙 #9): 닿은 위치를 몸 기준으로 바꿔 부위를 정한다(높이: 허리 아래 다리·위 팔, 좌우: 코스 오른쪽 방향 기준). 020의 절단 요청을 부르고 호스트가 확정. 020 병합 전에는 정한 부위를 로그·디버그 표시만. 도착 완료 플레이어는 무시(005). 같은 톱날에 계속 닿아 있을 때 연속 절단 방지(톱날·플레이어별 쿨다운, 세팅).
- 동기화: 가로 톱날은 "게임 시작 시각 + 경과 시간"으로 위치가 정해지므로 시작 시각만 공유하고 각 화면이 계산한다. 수직 톱날은 호스트가 정한 줄·출현 시각·내려감만 공유한다. 묘기 재시작 때 처음으로(005 `RunRestarted`).
- 수치(톱날 위치·속도·크기·출현 시간·발판 조건)는 `[Serializable] SawSettings` 묶음 하나에 모은다. 013과 병행 중에는 이 작업 폴더에 두고, 013 병합 후 `TightropeSettings` 톱날 칸으로 옮긴다.
- 1차 테스트 확인 항목(원문 7장)을 공동 테스트 항목 후보로 쓴다.
- 005 진입점(2026-10-03, `feat/tightrope-core`): `TightropeRun.Current` — `State`·`StateChanged`(Waiting/Running/Failed/Succeeded), `IsPerformanceStarted`·`PerformanceElapsed`·`PerformanceStarted`(묘기 시작, 수직 톱날 시간 기준), `TimeRemaining`, `RunRestarted`(처음 상태로). `TightropeCourse.Current` — `GetDistance`·`GetSide`·`GetLanePosition`·`TryGetRopePoint`·`IsLaneUsable`·`FinishDistance`·`LaneCount`·`LaneSpacing`. 사망: `PlayerBalance.ForceFall()`(소유자) 또는 `NetworkPlayer.RequestState(PlayerState.Fallen)` — 도착 완료(`PlayerState.Arrived`)·묘기 종료 후에는 호스트가 거절한다.

## Files

- 수정 예정: `Assets/Scripts/Tightrope/Saws/`(`TightropeSaws`·`SawSettings`·`SawMath`), `Assets/Resources/Prefabs/Objects/Obstacles/Saws/TightropeSaws.prefab`, 테스트 씬
- 읽기 전용 참고: `TightropeRun`·`TightropeCourse`·`NetworkPlayer`·`PlayerMover`(몸통 크기)·`PlayerCondition`(잃은 부위)

## Risks

- 가로 톱날 점프 회피는 겹침 시간(1.6m ÷ 속도)이 점프 체공(0.70초) 안에 들어와야 한다. 속도·크기를 바꾸면 권유서 §1-1 조건식으로 다시 확인한다.
- 005의 묘기 시작 신호·도착 완료 상태 이름이 정해진 뒤 착수한다.

## Auto Verification

- 구역 검사:
- 1 문서/규칙:
- 2 컴파일:
- 3 테스트:
- 4 실행 로그:

## 공동 테스트 항목

PM과 작업자가 구현 전에 합의한다. 최대 3개. 병합 후 공동 테스트에서 이 항목으로 통과 여부를 판단한다.
불필요하면 `불필요 — 이유`.
**작업자가 직접 플레이해 보고 결과(통과/실패, 본 현상)를 알려 준다.** (2026-10-03 PM·담당자 @MoHoDu 합의)

1. 가로 톱날이 좌우로 왕복하고 점프로 넘을 수 있으며, 닿으면 닿은 위치에 맞는 팔·다리가 잘린다. 팔 2개 또는 다리 2개를 잃으면 탈락·래그돌 — 모두에게 같게 보인다(020 병합 후 확인).
2. 묘기 시작 1분 뒤 수직 톱날이 진행자가 있는 줄로 내려오고, 옆줄 이동으로 피할 수 있다(모두에게 같은 줄·위치).
3. 수직 톱날 뒤 발판을 밟으면 톱날이 0.1~0.3초 안에 내려가 다른 사람도 지나갈 수 있다(목마 2층으로 넘기 확인은 006 이후).
