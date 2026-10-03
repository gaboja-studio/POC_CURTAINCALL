# 외줄 코스·진행 (코스 조회·묘기 판정)

## 목적

외줄 묘기의 무대(코스)와 진행(시작·도착·클리어·실패·재시작)을 담당한다. 코스는 인스펙터 값대로 만들어지는 직선 외줄이고, 진행은 호스트가 판정해 모두에게 공유한다. 장애물(012)·줄 위 동작(011)·목마(006)·화재(이후)는 이 도메인의 조회·신호를 쓴다.
규칙: [외줄 코스·진행 결정](../../Harness/Project/Decisions/tightrope-course-rules.md) · 원문 요약: `Docs/References/tightrope-rules-summary.md`

## 담당 파일 (2026-10-03 확인, Task-20260930-005 병합)

`Assets/Scripts/Tightrope/Rope/`
- `TightropeCourse.cs` — **코스 공개 진입점**. 줄 수·간격·도착 거리·옆줄 구간·플랫폼·여유 인스펙터 값, 메시·충돌체 생성(플레이 중), 코스 조회, 줄 간격 → 모든 `PlayerMover.LaneSpacing`, 내 캐릭터 줄 아래 추락 검사, 코스 값 내보내기·잠금.
- `CourseLayout.cs` — 줄 위치·끝 거리·옆줄 구간 계산(MonoBehaviour 없음).
- `TightropeRun.cs` — **진행 공개 진입점**. 호스트 판정: 제한시간, 도착 완료, 1명 도착 즉시 클리어(생존자 도착 지점 이동, `EndGame`), 실패 → 재시작, 묘기 시작 신호, 사망 플레이어 몸통 통과. NGO 이름 메시지로 공유.
- `CourseShapeSync.cs` — 대기 중 호스트 코스 값을 모두에게 공유, 게임 시작 후·클라이언트는 잠금.
- `CourseControlSwitcher.cs` — 내 캐릭터가 줄 위면 줄타기 조작·균형 켬·줄 중앙 정렬, 플랫폼이면 일반 이동·균형 끔.
- `PlatformControlScheme.cs` — 플랫폼 조작 규칙(WASD 8방향·Space 점프, Q/E·F 없음).
- `TightropeRunDebug.cs` — **테스트 전용**: 상태·남은 시간 표시, K 추락, L 도착, O 표시, 좌우 착지 표시기, 착지 로그, R = 호스트만 재시작.
- `Editor/TightropeCoursePrefabBuilder.cs` — 코스 프리팹 생성 메뉴(덮어쓰므로 값 조정 후 실행 금지).

에셋
- `Assets/Resources/Prefabs/Objects/Interactables/Tightrope/TightropeCourse.prefab` — 위 컴포넌트 + `PlayerSpawnPoints`(007) + 출발 위치 `SpawnPoints/Slot0~3`. 다른 Task는 배치만.
- 테스트 씬: `Assets/Scenes/Tests/Tightrope/Tightrope.unity` (코스, SessionUI, 카메라 리그, 게이지, HUD, LocalPlayerViews, TightropeRunDebug).

## 진입점

```csharp
var course = TightropeCourse.Current;
course.GetDistance(pos); course.GetSide(pos);                 // 진행 거리(시작점 0, 도착 100) / 옆 좌표
course.TryGetRopePoint(pos, out lane, out distance);          // 가장 가까운 줄
course.IsOnRope(pos, radius, out lane);                       // 줄 위인지
course.GetLanePosition(lane, distance);                       // 줄 위 월드 위치
course.FindLandingLane(pos, dir);                             // -1 왼/+1 오른 착지 줄, 없으면 TightropeCourse.NoLane
course.IsLaneChangeAllowed(distance); course.IsLaneUsable(lane, distance);
course.BlockSegment(lane, from, to); course.ClearBlockedSegments(); course.SetLaneBlocked(lane, true);  // 화재 등
course.GetStartPose(slot); course.GetFinishPose(slot); course.HasReachedFinish(pos);
course.FinishDistance; course.LaneCount; course.LaneSpacing; course.Rebuilt += ...;

var run = TightropeRun.Current;
run.State; run.StateChanged += (prev, next) => ...;           // Waiting / Running / Failed / Succeeded
run.IsPerformanceStarted; run.PerformanceElapsed; run.PerformanceStarted += ...;   // 장애물 시간 기준
run.TimeRemaining; run.InProgressCount; run.ArrivedCount; run.DeadCount;
run.RunRestarted += () => ...;                                // 장애물 등 처음 상태로
run.RestartByHost();                                          // 호스트만
```

## 의존 도메인

- [플레이어 동기화](player-sync.md) — 상태(`Normal`·`Fallen`·`Arrived`), `ServerRestartAll`, `ServerCanChangeState`(이 도메인이 규칙을 넣음).
- [플레이어 조작](player-control.md) — `PlayerMover`(일반 이동·통과·출발 위치), `PlayerBalance`, 조작 규칙.
- [네트워크 세션](network-session.md) — `StartGame`·`GameState`·`EndGame`.
- 사용하는 쪽(planned): 외줄 위 캐릭터(011), 톱날(012), 목마(006), 최종 씬(009).

## 수정 주의점

- 판정은 호스트만 한다. 클라이언트는 메시지로 받은 상태를 쓴다. 도착은 소유자도 요청한다(`RequestState(Arrived)`).
- 코스는 회전 없이 배치한다(플레이어 전진이 월드 +Z). 회전하면 경고.
- `SetLaneBlocked`·`BlockSegment`는 이 화면에만 적용된다. 온라인에서는 모든 화면이 같은 신호로 부르거나 공유를 붙인다(화재 작업).
- 묘기 시작은 "진행 중 전원이 시작점(0m)을 넘음"으로 판단한다(공중에 떠 있어도 놓치지 않게).
- 테스트 전용(`TightropeRunDebug`)은 실제 씬에 넣지 않는다. 실제 재시작 UI는 `RestartByHost()`를 쓴다.
- 에디터 저장 때 TMP 동적 폰트 에셋이 저절로 바뀔 수 있다(`Harness/Engine/Unity/Pitfalls/tmp-dynamic-font-churn.md`).
