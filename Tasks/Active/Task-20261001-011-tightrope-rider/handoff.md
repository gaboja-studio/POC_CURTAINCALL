# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 착수 준비 완료(2026-10-03). 최신 통합(013 세팅 포함)을 받아 합침. 2026-10-03 범위 축소로 남은 일 4개만(`plan.md` 세부)
- 동작하는 것: 걷기·점프·옆줄 점프 움직임·착지 충격(004), 줄 오르내림 전환·줄 아래 추락(005), 추락 공유·재시작(007), 조정값 세팅 파일(013). 테스트 씬 `TightropeRider.unity`는 빈 씬, `Tightrope/Rider/`는 `.gitkeep`만
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. 옆줄 점프 판정 연결: `PlayerMover.LaneJumpFilter`에 005 `IsLaneChangeAllowed`·`FindLandingLane`·`IsLaneUsable` 연결(코드는 `Assets/Scripts/Tightrope/Rider/`)
2. 뒤쪽 착지: 착지 자리에 동료가 있으면 진행 방향 뒤쪽으로 보정, 자리가 없으면 착지 실패(추락). 지금 `PlayerMover.BlockLaneJumpOntoPlayer`(임시)가 점프 자체를 막으므로 이걸 대체한다
3. 손잡기: 도착점이 같은 줄 동료 앞뒤 1.0m 이내면 `PlayerBalance.SetLaneLandingReduction(0.5)`. 거리·감소율은 `TightropeSettings`에 "손잡기" 묶음으로 추가
4. 테스트 부품 정리: `TestLaneLanding.cs` 삭제, `NetworkPlayerSync.unity`에서 그 컴포넌트 제거
5. 테스트 씬 `TightropeRider.unity`에 코스 프리팹·접속 UI 등 배치(005 Tightrope 씬 구성 참고), 공동 테스트 1~3 확인

## PM이 확인할 것

- 순서 배정: `TightropeSettings`(012와 공유), `PlayerMover.cs`(020과 공유) — 먼저 병합되는 쪽 다음에 다른 쪽이 최신 통합을 받는다
- 이 Task 다음: 006 목마 → 010 목마 예외(같은 갈래 B, 011 병합 후 인계)
- 대기 중인 결정: 없음

## Verification

- 구역·문서/규칙: save-work 실행 / 2 컴파일: save-work 실행 (코드 없음)
- 3 테스트: `NO_PROJECT_TESTS` (테스트 어셈블리 없음, 결정) / 4 실행 로그: 미실행 / 5 공동 테스트: 미요청
