# 외줄 묘기 (코스·진행·장애물·결과)

## 목적

외줄 묘기 한 판의 전체를 담당한다: 무대(코스), 진행(시작·도착·클리어·실패·재시작), 장애물(톱날·불), 결과·보상. 기획자와 AI가 같이 읽는 문서다 — 앞부분(흐름·조정값·계획)은 쉬운 말, 뒷부분(파일·진입점·주의점)은 구현용.
규칙: [코스·진행 결정](../../Harness/Project/Decisions/tightrope-course-rules.md) · [조작·목마 결정](../../Harness/Project/Decisions/tightrope-rules.md) · [균형 결정](../../Harness/Project/Decisions/tightrope-balance.md) · [조정값 세팅 결정](../../Harness/Project/Decisions/game-settings.md) · 원문 요약 `Docs/References/tightrope-rules-summary.md` · 작업 순서 `Integrations/Active/Integration-tightrope-prototype/plan.md`

## 묘기 흐름 한눈에 (✅ 있음 / ⏳ 계획)

1. ✅ **대기**: 방에 모여 출발 플랫폼에서 자유 이동. 호스트가 "게임 시작"(최소 인원 이상). 대기 중 호스트의 코스 값이 모두에게 맞춰진다.
2. ✅ **묘기 시작**: 진행 중인 플레이어 중 한 명이라도 줄(0m)에 올라선 순간(2026-10-03 변경, 플랫폼 대기로 장애물 늦추기 방지). 제한시간 5분은 게임 시작부터, 장애물 시간은 묘기 시작부터 잰다.
3. **줄 위 진행**: ✅ 걷기 0.5m/s·점프·옆줄 점프·균형(A/D)·균형 붕괴 추락 / ⏳ 옆줄 판정·뒤쪽 착지·손잡기(011), 목마(006·010).
4. **방해물**: ✅ 톱날(012, 절단은 판정 로그만) / ⏳ 절단 적용(020)·불(014). 가로 톱날 22개(점프로 회피)·수직 톱날(옆줄 이동 또는 목마 2층이 넘어가 발판, 밟으면 0.1~0.3초에 내려감, 012). 톱날에 닿으면 **닿은 위치의 팔·다리가 잘림**(팔 → 상호작용 패널티, 다리 → 느려짐, 공통 균형 흔들림 증가, 팔 2개 또는 다리 2개 손실 → 탈락·래그돌, 020). 잃은 부위는 **완전히 죽기 전까지 재시작·다음 묘기에도 유지**(게임 전체 규칙 `Harness/Project/Decisions/body-damage.md`, 이후 돈으로 부품 수리). 뒤에서 쫓아오는 불·시간 따라 불타는 줄 구간(014).
5. ✅ **판정**(호스트): 한 명이라도 100m 도착 → 즉시 클리어(살아 있는 전원 도착 지점으로, 게임 종료). 도착 없이 전원 사망 또는 시간 종료 → 실패 → 3초 뒤 묘기 재시작(코스·타이머·장애물 처음부터).
6. ⏳ **결과·보상**: 결과(성공 여부·도착 인원·시간)와 임시 보상 표시, 보상 규칙은 파일 교체로 변경(015), 정식 UI(016).
7. ⏳ **겉모습**: 캐릭터 모델·애니메이션(017), 맵 아트(018), 소리·효과(019) — 아트·소리 준비 후.

## 조정값 (기획자가 바꾸는 수치)

013 이후 모두 `Assets/Resources/GameSettings/`에서 바꾼다. 그 전에는 "지금 위치"의 프리팹 인스펙터. 값은 2026-10-03 프리팹 기준(씬에서 덮어쓴 값 없음).

| 세팅 파일 · 묶음 | 항목 = 현재 값 | 지금 위치 |
|---|---|---|
| Base · 세션 | 정원 4, 최소 시작 인원 1(테스트) | NetworkManager 프리팹 |
| Base · 캐릭터 | 플랫폼 이동 3m/s, 회전 720°/s, 중력 9.81, 캡슐 높이 1.5·지름 0.6, 모델 맞춤 키 1.73(⚠ 캡슐과 불일치, 017에서 확정), 밀기 2m/s, 몸 간격 0.05 | Player 프리팹 |
| Base · 입력 | 길게 누르기 0.5초 | Player 프리팹 |
| Base · 균형 공통 | 초록 ±40, 빨강 2초 추락, 보정 속도 60, 흔들림 걷기 15·서기 5, 흔들림 방향 간격 1~2초, 기울기 가속 0.5 | Player 프리팹 |
| Base · 연출·동기화 | 몸 기울기 최대 25°·따라가기 10, 래그돌 밀기 1.5, 균형 전송 기준 0.5 | Player 프리팹 |
| Tightrope · 코스 | 줄 4, 간격 3m, 길이 100m, 옆줄 이동 구간 0~100m, 줄 끝 거리(비움=끝까지), 시작·도착 플랫폼 6m, 양옆 여유 2m, 줄 두께 0.2, 걷는 폭 0.4, 줄 위 높이 허용 0.3(코드 상수), 추락 깊이 1m, 안전망 6m, 출발 -1m | 코스 프리팹 |
| Tightrope · 진행 | 제한시간 300초, 실패 후 재시작 3초 | 코스 프리팹 |
| Tightrope · 줄 위 동작 | 줄 위 이동 0.5m/s, 점프 0.8m, 착지 충격 10(체공 0.15초 이상), 옆줄 착지 충격 35, 옆줄 뒤 흔들림 ×1.75·3초 | Player 프리팹 |
| Tightrope · 손잡기 ⏳011 | 동료 거리 1.0m, 감소 50% | (011에서 추가) |
| Tightrope · 목마 | 층별 흔들림 배율 1/1.3/1.6/2, 위층 균형 전달 0.2 | Player 프리팹 |
| Tightrope · 톱날 | 장애물 순서 `steps`(단계: 시작 조건·지연·가로 범위 작동/정지·수직 출현·반복, 기본 = 가로 전체 게임 시작·수직 묘기 60초·제거 20초 뒤 반복), 가로 22개 목록(위치·출발 쪽, 기본 번갈아)·속도 3.0→4.5m/s·왕복 반폭 9.5·지름 1.0/외형 1.2·상단 0.2, 수직 0.8m/s·93→5m·최대 1·중심 0.6, 발판 뒤 1.5m·밟는 즉시·하강 0.2초, 허리 0.85m, 재판정 1초 | 톱날 프리팹 `SawSettings` |
| Base · 신체 손상 기본 ⏳020 | 다리 1개당 이동 배율, 팔 1개당 상호작용 패널티(모든 묘기 공통 기본값) — **기획 미정, 임시값** | (020에서 추가) |
| Tightrope · 신체 손상 ⏳020 | 외줄 조정: 부위 1개당 균형 흔들림 배율, 이동·상호작용 배율 덮어쓰기, 같은 톱날 연속 절단 방지 시간 | (020에서 추가) |
| Tightrope · 불 ⏳014 | 추격 속도·출발 지연·불타는 구간(줄·거리·시각) — **기획 미정, 임시값** | (014에서 추가) |
| Tightrope · 보상 ⏳015 | 보상 파일 연결(`TempReward`, 교체형) | (015에서 추가) |
| Base · 소리 ⏳019 | 볼륨·켜기/끄기 | (019에서 추가) |

## 앞으로 만들 것 (planned)

| 단계 | Task | 이 도메인에 더하는 것 | 쓰는 진입점 |
|---|---|---|---|
| 0 | 013 세팅 정리 | `Assets/Scripts/Settings/`, 위 표 이동 | 모든 컴포넌트가 세팅을 읽음 |
| 1A | 012 톱날 | 톱날·발판, 닿은 부위 판정 → 020 절단 요청 | `PerformanceStarted`·`PerformanceElapsed`·`RunRestarted`, `RequestState(Fallen)` |
| 1A | 020 신체 손상 | 절단 요청 진입점·잃은 부위 공유·패널티·탈락 | 004 `PlayerCondition`·`BodyPart`, 007 상태 공유, `RunRestarted` |
| 1A | 014 불 | 추격 불, 시각별 `BlockSegment` | `PerformanceStarted`, `BlockSegment`·`ClearBlockedSegments`, `RequestState(Fallen)` |
| 1B | 011 | `LaneJumpFilter` 연결, 뒤쪽 착지, 손잡기 | `IsLaneChangeAllowed`·`FindLandingLane`·`IsLaneUsable` |
| 1B | 006·010 | 목마·합체 착지·예외 | 007 호스트 판정 통로 |
| 2A | 015·016 | 결과 확정·공유, `RewardTable`, 정식 UI | `StateChanged`·`ArrivedCount`·`DeadCount`·`TimeRemaining` |
| 2B·3 | 017·018·019 | 모델·애니메이션, 판정과 분리된 맵 아트, 사건별 소리·효과 | 공개 상태·이벤트만 읽음(판정 코드 수정 없음) |
| 4 | 009 | 묘기 플로우 최종 씬 | 위 전부 |

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

`Assets/Scripts/Tightrope/Saws/` (Task-012, 작업 중)
- `TightropeSaws.cs` — **톱날 공개 진입점**. 가로 위치는 게임 경과로 각 화면이 계산, 장애물 순서는 호스트가 실행하고 가로 작동·정지 기록(게임 경과 시각)·수직 톱날 줄·출현·발판 눌림을 NGO 이름 메시지로 공유. 호스트가 진행 중 플레이어만 판정 → `Hit`(모든 화면) + 로그. 겉모습은 기본 도형(충돌체 없음), 프리팹 칸으로 교체.
- `SawSettings.cs` — 톱날 조정값 묶음·장애물 순서(013 병합 후 `TightropeSettings`로 이동). `ObstacleSequence.cs` — 순서 실행기·가로 톱날 작동/정지 기록(`HorizontalSawTrack`). `SawMath.cs` — 왕복·원판/몸통 접촉·부위 결정(모두 MonoBehaviour 없음).
- 에셋 `Assets/Resources/Prefabs/Objects/Obstacles/Saws/TightropeSaws.prefab`, 테스트 씬 `Assets/Scenes/Tests/TightropeSaws/TightropeSaws.unity`.

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

var saws = TightropeSaws.Current;                             // CurtainCall.Tightrope.Saws
saws.Hit += hit => ...;                                       // hit.Player·Part(BodyPart)·Kind·Index·Contact — 020이 절단 적용
saws.IsVerticalActive; saws.VerticalLane; saws.VerticalDistance; saws.IsPlatePressed; saws.VerticalChanged += ...;
saws.GetHorizontalPosition(i); saws.Settings;
```

## 의존 도메인

- [플레이어 동기화](player-sync.md) — 상태(`Normal`·`Fallen`·`Arrived`), `ServerRestartAll`, `ServerCanChangeState`(이 도메인이 규칙을 넣음).
- [플레이어 조작](player-control.md) — `PlayerMover`(일반 이동·통과·출발 위치), `PlayerBalance`, 조작 규칙.
- [네트워크 세션](network-session.md) — `StartGame`·`GameState`·`EndGame`.
- 사용하는 쪽(planned): 위 "앞으로 만들 것" 표.

## 수정 주의점

- 톱날 충돌은 물리 충돌체가 아니라 `SawMath` 거리 계산이다(플레이어와만 닿음). 판정 지연은 NetworkTransform 위치 기준.
- 판정은 호스트만 한다. 클라이언트는 메시지로 받은 상태를 쓴다. 도착은 소유자도 요청한다(`RequestState(Arrived)`).
- 코스는 회전 없이 배치한다(플레이어 전진이 월드 +Z). 회전하면 경고.
- `SetLaneBlocked`·`BlockSegment`는 이 화면에만 적용된다. 온라인에서는 모든 화면이 같은 신호로 부르거나 공유를 붙인다(화재 작업).
- 묘기 시작은 "진행 중 플레이어 중 한 명이라도 시작점(0m)을 넘음"으로 판단한다(공중에 떠 있어도 놓치지 않게).
- 테스트 전용(`TightropeRunDebug`)은 실제 씬에 넣지 않는다. 실제 재시작 UI는 `RestartByHost()`를 쓴다.
- 에디터 저장 때 TMP 동적 폰트 에셋이 저절로 바뀔 수 있다(`Harness/Engine/Unity/Pitfalls/tmp-dynamic-font-churn.md`).
