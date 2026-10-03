# Handoff

덮어쓰는 스냅샷이다. 다음 AI(또는 다음 날의 나)는 이 파일부터 읽는다.

## 지금 상태

- 단계: 착수 준비 완료(2026-10-03). 최신 통합(013 세팅 포함)을 받아 합침. 2026-10-03 범위 축소로 남은 일 4개만(`plan.md` 세부)
- 동작하는 것: 걷기·점프·옆줄 점프 움직임·착지 충격(004), 줄 오르내림 전환·줄 아래 추락(005), 추락 공유·재시작(007), 조정값 세팅 파일(013)
- 2026-10-03 1번 완료(미커밋): `Tightrope/Rider/RopeLaneJumpRules.cs`(씬에 두면 내 캐릭터 `LaneJumpFilter`에 코스 판정 연결), `PlayerMover.GetLaneLandingPosition` 공개(기존 계산 추출). 테스트 씬 `TightropeRider.unity` = 005 `Tightrope.unity` 복사 + `RopeLaneJumpRules` 오브젝트(에디터로 저장)
- AI Play 확인(LAN 호스트 1명, eval로 줄 위 이동·점프 요청): 바깥쪽 줄 없음 → 무시, 안쪽 → 옆줄 이동, 옆 줄 막힘(`BlockSegment`) → 무시, 해제 → 이동. 옆줄 이동 불가 구간은 현재 코스가 0~100m 전부 허용이라 확인 못 함. 균형 입력 없이 옆줄 착지하면 약 2초 뒤 균형 붕괴 추락(기존 004 동작으로 보임)
- 막힌 것: 없음
- PR: 없음

## 다음 할 일

1. ~~옆줄 점프 판정 연결~~ 완료(위). 실게임용으로는 `RopeLaneJumpRules`를 코스 프리팹에 붙이는 것을 PM에 요청
2. ~~뒤쪽 착지~~ 완료(미커밋, 2026-10-03): `RopeLaneJumpRules`가 착지 자리 동료(줄지어 있으면 맨 뒤) 뒤로 보정, 설 자리 없으면 착지 순간 `ForceFall`, 있을 때 `BlockLaneJumpOntoPlayer` 끔. `PlayerMover.LaneLandingShift` 추가 + 보정 시 앞뒤도 목표에서 멈춤. AI Play(가짜 동료 = PlayerMover만 붙인 오브젝트): 9.35m·8.70m 정확히 착지, 일반 옆줄 영향 없음, 줄 시작 0.3m는 추락 확인. 실제 2인 접속 확인은 작업자
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
