# Log

## 2026-11-10 1. 추락 후 끝나지 않는 문제
- 원인: Task-114에서 외줄 추락·매달림 실패가 `ServerKill` 대신 일반 낙하(`RequestNaturalFall`/`ServerReleaseIntoFall`)로 바뀜. 일반 낙하는 걸을 수 있는 바닥 착지 때만 피해를 계산하는데, 외줄 공연 씬(코스 y=0) 아래에는 바닥·안전망·낙사 영역이 없음.
- 사용자 결정: 공통 낙사 영역 추가(씬별 즉사 예외로 되돌리지 않음).
- 변경: `Tightrope.unity` 루트에 `TightropeFallVoid` + `EscapeFallVoid`. 좌표계 = TightropeCourse, 범위 x -50~50 / z -50~250, 줄 아래 5m(`fallHeight -5`)에서 `MapExit` 사망. 매달림 위치(줄 아래 1.9m)보다 깊다. 범위·깊이는 조정 가능한 씬 값.
- 대기실 외줄 리허설은 이 씬을 쓰지 않으므로 영향 없음.
- 검증: 씬 저장 diff 확인(추가 58줄, 삭제 0). 플레이·자동 테스트 미실행.

## 2026-11-10 2~3. 세로 톱·협동 관문
- 사용자 지적으로 확인: 톱 단계는 `StartHorizontal` 1개뿐이라 세로 톱이 나오지 않고, `cooperativeGates: 0`으로 협동 관문도 꺼져 있다. 두 항목은 해당 없음.

## 2026-11-10 4. 줄 구간 불
- 문제: Task-114에서 구간 불이 일반 화염(사지 1개 손실)으로 분류되어, 마지막 35m를 맞고 지나갈 수 있었다.
- 사용자 결정: 반드시 피해야 하는 불.
- 변경: `TightropeFire.Kill`에서 구간 불·추격 불벽 모두 `HazardDamageKind.FatalFire`. 사망 시 매달림 정리도 두 종류 모두 적용. `TightropeFireContactPlayTests.FireSegmentRejectsContactBeforeIgnition` 기대값을 사망(Fallen)으로 변경. `injury-rules.md`에 결정 기록.
- 검증: Unity 컴파일 성공, PlayMode `FireSegmentRejectsContactBeforeIgnition` 1/1 통과. 실제 플레이는 사용자 확인.

## 2026-11-10 4-1. 구간 불 판정 정리 (사용자 후속 지적)
- 사용자 의도: 불꽃이 최대가 되는 순간 줄이 끊어지므로 추락으로 알아서 죽는다. 접촉 즉사는 중복.
- 변경: `TightropeFire.TryContact`에서 구간 불 접촉 판정을 제거하고 추격 불벽만 남김. 앞선 '구간 불 FatalFire' 변경은 이 결정으로 대체된다(추격 불벽 즉사는 유지).
- 영향: 구간 불로 인한 사망은 이제 불 사망(`FIRE_Segment`)이 아니라 추락/맵 이탈로 기록된다(텔레메트리·결과 원인 표시).
- 테스트 `FireSegmentRejectsContactBeforeIgnition`: 발화 전·후 모두 접촉 무시, 상태 Normal·부위 손실 0을 확인하도록 변경.
- 검증: PlayMode `FireSegmentRejectsContactBeforeIgnition`(구간 불 무판정)·`FireContactRejectsAvoidanceOldRoundRestartAndStaleReport`(추격 불벽 즉사 유지) 2/2 통과. 실제 줄 끊김→추락→낙사 흐름은 사용자 플레이 확인.

## 2026-11-10 4-2. 구간 불 예고 시간·소멸 연출
- 사용자 요청: 최대 불꽃까지 3초, 최대 도달(줄 끊김)과 동시에 불꽃이 아래부터 사라지게.
- 변경: `TightropeSettings.asset` `fire.warningSeconds` 2.5→3. `FireView`에서 발화 후 2초 최대 유지(`burnHoldSeconds`)를 제거하고, 발화 순간 새 불꽃 방출을 멈춘다. 남은 파티클은 지우지 않아 위로 올라가며 수명이 끝나므로 아래쪽부터 비어 보인다(최대 약 1초).
- 테스트 `RestoredCourseFireWarnsBlocksThenResetsWithWholeCourse`: 발화 후 방출 정지·잔여 파티클 존재를 확인하도록 변경. 마지막 줄 `Find("ChaseFire")` NullReference는 Task-074부터 있던 기존 실패.
- 검증: 컴파일 성공. PlayMode `RestoredCourseFireWarnsBlocksThenResetsWithWholeCourse`는 새 검사(예고 중 방출, 발화 후 방출 정지·잔여 파티클, 줄 끊김)까지 모두 통과한 뒤 마지막 줄 1199 `Find("ChaseFire")` NullReference로 FAIL — 기존 실패, 이번 변경과 무관. 실제 연출 체감은 사용자 확인.

## 2026-11-10 5. 가로 톱 여유 (전체 난이도)
- 핵심: 줄 위 점프 1.0→1.5m로 가로 톱(윗면 0.2m) 위 체공이 0.81→1.03초로 늘어 너무 여유로움. 공통 점프·폭 규칙은 유지하고 외줄 톱 값으로 보정.
- 사용자 결정: 두 다리 손실(점프 1.5×0.5=0.75m, 기어가기 점프 허용)도 넘을 수 있는 높이로 올리고 속도를 올린다.
- 변경(`TightropeSettings.asset` saws): `horizontalAttackTop` 0.2→0.45(두 다리 손실 체공 0.49초 = 공통화 전과 같음, 정상 0.93초·한 다리 0.74초), `horizontalFirstSpeed` 9.5→11.4, `horizontalLastSpeed` 12→14.4(+20%, 반응 시간 단축).
- 속도는 발밑 통과 시간(0.1~0.2초)을 줄이므로 체공 여유를 줄이지 않고 반응 시간만 줄인다. 수치는 사용자 플레이로 조정.
- 검증: 설정 값 변경만. 자동 테스트·플레이 미실행.
- 배치 확인: 수평 톱은 씬에 고정 배치가 없고 `TightropeSaws`가 `GameSettings.Tightrope.Saws.HorizontalCenterHeight`로 판정·표시·원격 판정 위치를 모두 계산한다(씬·프리팹 덮어쓰기 없음). 설정 변경이 곧 배치 변경.
- 에디터 계산 확인: 두 다리 손실 줄 위 점프 1.5×0.5=0.75m, 기어가기 점프 허용, 톱 윗면 0.45m(중심 0.4m), 톱 위 체공 0.49초.
- PlayMode `MovingHorizontalSawCanBeJumpedWithoutDamage`: FAIL(1091줄, 점프 전 대기 중 이미 부위 손실). 원래 값(0.2m·9.5~12m/s)으로 되돌려도 같은 위치에서 FAIL — 이번 변경 전부터 있던 실패. 원인 미조사. 이후 새 값으로 복구 확인.

## 2026-11-11 10. 공통 낙하 높이 변경
- 사용자 요청: 외줄·인간대포에 같은 규칙. 착지까지 높이 5m 이상 15m 미만 부상, 15m 이상 사망, −15m 아래로 떨어지면 즉시 사망. 기존 4부위 절단·4곳 손실 사망 규칙은 유지.
- 변경: `BaseGameSettings.asset` `fallInjuryHeight` 4→5, `fallDeathHeight` 8→15(코드 기본값·툴팁도 같이). `HumanCannon.unity` `flightFloorHeight`·`HumanCannonRouteSystem.fallHeight` −10→−15. 외줄 `TightropeFallVoid`는 이미 −15(범위 x −50~50, z −50~250, 코스 0~165m 포함).
- 테스트 갱신: `CommonRulesFollowupTests` 경계 사례 5/15, `NetworkFallSegmentTests` 4.5m 낙하→5.5m, `FinalCannonPlayTests.CommonPoles` 바닥 2m 낮춰 봉 착지가 부상 구간에 들게 함.
- 문서: `injury-rules.md`, `game-settings-application.md`.
- 영향(탄도 계산 근사): 안전 착지대 밖 착지 시 출발 대포→높이 6m 바닥 약 7.1m 부상, 쉘터·최종 대포→6m 바닥 약 9.1~9.3m 부상(이전엔 사망), y=0 지상 착지 출발 13.3m 부상·쉘터 15.3m/최종 15.5m 사망. 트램펄린 반동(약 7.3m)은 부상 유지.
- 사용자 후속 지적(같은 날): −15m는 낙사 영역이 아니라 캐릭터 규칙. `NetworkPlayer.ServerKillBelowFallLimit` 추가 — 호스트가 매 프레임 외줄·인간대포 공연 씬(`SpaceTransition.TightropeScene/HumanCannonScene`)에서 발 Y ≤ `BodyDamage.FallKillHeight`(−15)이면 즉시 `ServerKill(MapExit)`. Dev 추락 무시는 제외. 씬의 기존 낙사 영역·코스 밖 판정은 같은 −15라 중복이지만 남겨 둠.
- 사용자 추가 결정: 씬 제한 없이 모든 씬에 적용. 탈출로는 이후 맵을 올려 맞춘다. 그 전까지 탈출로 −48m·−80m 바닥 구간은 이 규칙으로 사망한다.
- 범위 밖: 대기실 리허설 코스 프리팹(−10)·탈출로 씬 낙사선은 바꾸지 않음.
- 검증: 컴파일 성공, 에디터에서 4.99=Safe/5=Injured/14.99=Injured/15=Dead 확인. 자동 테스트·플레이는 사용자.

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 사용자 확인: 외줄은 톱날로만 부상, 추격 불벽·균형 실패 추락은 낙사=사망이 의도. 줄 아래 바닥 없음은 문제 아님(injury-rules.md 기록).
- check-work(2026-11-11): 컴파일 PASS, 공통 설정 PASS, 자동 테스트 SKIPPED(사용자 실행). 에디터에서 부상 5·사망 15·즉사 높이 −15 적용 확인.

## 2026-11-11 11. 인간대포 출발대 1m 상향
- 사용자 요청: 출발대에서 지상으로 뛰어내리면 부상. 공통 기준 5m에 맞춰 출발대를 4m→5m로 올림. 외줄은 톱날 부상·추락 사망이 의도라 변경 없음.
- 변경(`HumanCannon.unity`, 19줄): 출발 바닥 7칸·`Cannon`·`DepartureCannon2/3`·`SpawnPoints`·`Trampoline`·`DepartureTransport` Start/Platform +1m. 출발 대포 3대 `forwardSpeed`를 `FirstCatch` 겨냥으로 재계산(7.224→7.070, 7.296→7.141, 기존 `HumanCannonLiftTrapBuilder.Aim` 공식). 2인 리프트 함정은 이미 꺼져 있음(`overloadTrap: 0`).
- 테스트: 새 `FinalCannonPlayTests.RaisedDeck.cs`(Category RaisedDeck) 8/8 PASS. 실측 — 시작 위치 y5.05·탑승 정상, 대포 미포착 최고점 14.34·낙하 14.26 → 두 다리 부상(3대 모두, 착지 z≈25.3, FirstCatch 25.03), FirstCatch 트램펄린 포착 무피해, 리프트 5m 하강 무피해, 출발대 높이 5.0에서 지상 낙하 4.92m 무피해(지상 발 높이 0.08), 점프 0.8 포함 5.72m 부상.
- 수정 테스트: `PerformanceLinkPlayTests` 시작 위치 4.05→5.05, `TeleportDestinationFrameCanBeSkipped…`·`NetworkFallSegmentTests` 2건은 5m/15m 기준에 맞춘 가상 수치 조정(통과).
- PlayMode `CurtainCall.HumanCannon.PlayTests` 전체 96건: 83 PASS / 13 FAIL → 위 1건 수정 후 12 FAIL. 남은 실패는 이번 변경과 무관하다고 판단: `AppliedMetalTransports…`(2026-11-04 기록과 같은 기존 실패), 봇 2인 4건(봇 실행파일과 NetworkVariable 불일치 OverflowException), 그네 3건(SwingLength 8 기대·실제 1.5 등 공통 봉 설정), `OriginalCourse_InjuryPolicy…`(NRE), `OneArmGrab…`, `TimedLinkedPole…`, `UpperMetalMesh…`, `CannonLaunch_DirectionInput…`(공중 보정 속도 차 3.73). HEAD 대조 실행은 하지 않음(파일 되돌리기 권한 거부).
- `PerformanceLinkPlayTests.Managed_CannonLastSurvivorDeath…`는 준비 단계 리플렉션이 `RunState.StartSession(bool=false)` 선택 인자를 못 찾아 FAIL — 기존 문제, 시작 위치 검사 미도달.
- 남은 주의: 대기실 리허설 복사본(`RehearsalCourseFromPerformance.prefab`)은 4m 그대로. `HumanCannonLiftTrapBuilder.DepartureHeight` 상수(4)는 1회용 도구라 미변경. 출발대가 높아져 전력 질주 점프로 9m 틈을 건널 가능성이 조금 늘었다(계산 약 8.9m).

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 사용자 결정(2026-11-11): 출발대와 지상 사이 9m 틈은 그대로 둔다. 틈으로 떨어지면 −15m 즉사, 건너편 지상 착지만 부상.
- dev 병합(2026-11-11): dev에 같은 규칙이 먼저 반영돼 있었다(부상 5m·사망 15m, `BodyDamage.WorldDeathY` −15, `NetworkPlayer.CheckWorldDeath`). 충돌 3건은 dev 구현을 채택하고 이 Task의 중복 구현(`FallKillHeight`·`ServerKillBelowFallLimit`)은 제거했다. 출발대 5m·조준·코스 밖 −15는 그대로 유지.

- 2026-11-11 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=ENV_BLOCKED(Unity 환경); 테스트=SKIPPED(자동 테스트 선택 안 함)
- 제출(2026-11-11): PR #328 (fix/main-stages → dev), PM 팀 검토 요청. 병합 후 컴파일은 Unity 종료 상태라 미확인.
