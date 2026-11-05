# Log

- [이전 제출 기록](History/001-log-before-submission.md)
- [완료한 코스·톱벽 조정 원문](History/002-log-before-dev-integration.md)
- [11-03~04 이동 장치·추격 불·승강기·안전지대 원문](History/003-log-2026-11-03-04.md)

## 2026-11-05 1구간 추격 불: 나타날 때 점점 커짐
- 사용자 요청: 후방 추격 불이 툭 나타나지 않고 나오는 시점부터 점점 커져서 오도록.
- `HumanCannonFireWall`: `appearSeconds`(기본 6초)·`appearStartScale`(0.1) 추가. 켜진 시각(`ArmedAt`, 서버 시각)부터 SmoothStep으로 0→1 등장 비율 `Appear(now)`. 파티클 시작 크기(3D 크기면 축별)·상승 속도는 0.1→1배, 방출량은 기존 예고/전진 비율 × 등장 비율(최소 0.1), 자식 불빛 세기는 0→원래 값. 원래 값은 첫 표시 때 캐시. 판정·위치·예고 시간은 그대로이며 추가 동기화 값 없음.
- 씬·프리팹 미수정(기본값 6초 적용). 시간 조정은 `FirstSectionFire`의 `appearSeconds`.
- 테스트: `RouteGeometryTests.FireEffect_GrowsFromSmallWhenItAppears` 추가, 기존 `FireEffect_*`는 등장 1초로 지정. EditMode `RouteGeometryTests` 55/55 PASS, PlayMode `FireWalls_*` 1/1 PASS. 실제 플레이 체감 미확인.
- 후속(사용자: 시작 때 확 한 번 나온 뒤 서서히 커짐 → 확 나오는 부분 제거). 원인: 프리팹 Flames·Core·Embers 모두 `prewarm`이 켜져 있어 켜지는 순간 2초 분량의 원래 크기 불꽃이 한꺼번에 생겼고, 크기를 줄이기 전에 켰다. 수정: 등장 시 크기·방출·속도를 먼저 줄인 뒤 켬, 다 자라기 전에는 `prewarm`을 끔(다 자란 뒤 다시 보일 때만 원래 값), 처음 보일 때 씬 시작부터 켜져 있던 파티클은 한 번 꺼서 처음부터 다시 시작. 프리팹은 미수정. EditMode `FireEffect_*` 2/2 PASS, PlayMode `FireWalls_*` 1/1 PASS. 실제 플레이 체감 미확인.

## 2026-11-05 시작 지면 전체 안전 착지
- 사용자 요청: 시작 대포가 놓인 지면 전체를 부상 없는 안전구역으로.
- 원인: 시작 지면(x-22~22, z-11~7) 타일 7개 중 `Ground` `HumanCannonLandingZone.additionalSurfaces`에 5개만 등록. 2026-11-02 구멍을 메운 `ElevatorFill_Back`·`ElevatorFill_Right`가 빠져 있어 그 위 착지가 추락 부상.
- 씬: 두 메움 바닥과 `DepartureTransport/Platform` 좌우 `FloorPanel`(출발 위치에서 지면과 같은 높이)을 추가해 9개, 저장. 발판은 이동 중에도 안전 착지면이다(기울어진 패널은 기존 규칙대로 제외).
- 확인: 에디터에서 `SupportsLanding`으로 메움 바닥·발판·모서리 안전, 지면 밖(z7.5) 비안전. 자동 테스트는 사용자가 직접 하기로 해 미실행.

## 2026-11-05 트램펄린 들고 낙사 → 시작 위치 복귀
- 사용자 요청: 트램펄린을 든 채 낙사하면 사망 지점에 남지 않고 시작 지점 원래 위치로.
- 기존: 운반자가 `Fallen`/`Retired`가 되면 무작위 생존자 옆으로 옮김, 생존자가 없으면 그 자리(낭떠러지 아래)에 남음.
- `HumanCannonTrampoline.Update`(호스트): 운반자가 비정상 상태이고 운반자 또는 트램펄린이 코스 아래(`HumanCannonRouteSystem.IsBelowCourse`, y<-10)면 놓기와 함께 최초 위치·회전(`initialPosition`)으로 돌림. 생존자 옆 복구는 하지 않음. 위치는 기존 소유 상태 패킷으로 모든 화면에 공유. 톱·불 등 다른 사망은 기존대로 생존자 옆 복구.
- 컴파일 확인. 테스트는 사용자가 직접 하기로 해 미실행.

## 2026-11-05 지상 톱날 외형 회전
- 사용자 요청: 지상 톱날 이미지가 회전하지 않음 → 전 구간 지상 톱 전부 회전.
- 원인: `HumanCannonHazard.Present`가 톱날 면의 법선 축으로 돌리고 있었으나 `rotationSpeed`가 60°/s(0.5초에 30°)라 거의 멈춰 보임. 회전축은 톱날 모델 법선과 일치함을 에디터에서 확인.
- 씬: 지상 톱(`GroundSaw`, 봉 톱 제외) 14개 전부(숨긴 것 포함) `rotationSpeed` 60 → 720(외줄타기 같은 톱 모델 `spinSpeed`와 같음), 저장. 난이도 속도 배율(1~1.5)이 그대로 곱해진다. 지상 톱 접촉 판정은 원판이라 회전 속도와 무관.
- 테스트는 사용자가 직접 하기로 해 미실행. 실제 화면에서 회전이 거꾸로/느리게 보이는 착시가 있으면 값 조정 필요.

- 2026-11-05 PR: `fix/main-stages` → dev PR #297(https://github.com/gaboja-studio/POC_CURTAINCALL/pull/297) 생성, PM 팀 검토 요청. 인간 대포(Task-081)·외줄(Task-074) 수정 묶음. CI 1/1 통과, 검토 대기(BLOCKED). 최신 dev 미병합(충돌 없음, MERGEABLE).

- 2026-11-05 최신 dev 병합: PR #297이 `70b0bb7b`에서 병합돼 외줄 커밋 `00a271c3`은 dev에 없음. `origin/dev`(#297·#298) 병합 `a12c383a` 충돌 없음, Unity 컴파일 PASS. `_ThirdParty`를 dev 포인터 `c1e64a6d`로 정방향 갱신(로컬 셰이더 수정 2개 보존, LFS 파일 정상). dev의 Verify Docs 실패(Task-081 log.md 144줄 > 130) 해결: 11-03~04 기록을 원문 그대로 `History/003-log-2026-11-03-04.md`로 옮김, `verify-fast` OK(경고만).

## 2026-11-05 트램펄린 운반자 사망 → 그 자리에 둠
- 사용자 요청: 멀티 플레이에서 트램펄린 운반자가 죽으면 생존자에게 강제 소환됨 → 낙사는 그대로(시작 위치 복귀), 그 밖의 사망은 사망 지점에 그대로 둠.
- `HumanCannonTrampoline`: 무작위 생존자 옆 이동(`TryRecoverNearSurvivor`, `carrierRecoveryPending`, 미사용 `CarryHandlePoint`) 삭제. 운반자가 비정상 상태면 놓기만 하고, 낙사면 최초 위치로 돌린 뒤 소유 상태를 공유한다.
- 컴파일 PASS, `check-work` 문서/규칙 PASS, 자동 테스트 SKIPPED. 실제 멀티 플레이 확인 필요. 공중(점프 중)에서 죽으면 트램펄린이 그 높이에 남을 수 있음.
- 검사 중 Unity가 `ProjectSettings/Packages/com.unity.services.vivox/Settings.json`을 바꿈(이번 작업과 무관, 되돌리지 않음).

## 2026-11-05 공중 사망 시 트램펄린 낙하
- 사용자 요청: 운반자가 공중(점프 등)에서 죽으면 트램펄린이 그 위치에서 내려와 지면에 닿게.
- `HumanCannonTrampoline.Drop.cs` 추가: 호스트가 낙사 아닌 사망이면 바로 아래로 레이캐스트(경사 법선 y≥0.7, 플레이어·자기 자신·트리거 제외, `HumanCannonRouteSystem.FallHeight`까지)해 지면을 찾고 중력 가속으로 떨어뜨림. 지면이 없으면 낙사처럼 시작 위치.
- 상태 패킷은 도착 지면 위치를 보낸다. 원격 화면은 그 지점이 현재 위치 바로 아래(수평 2m 이내)면 순간이동 대신 같은 방식으로 떨어뜨린다. 잡기·재시작·연습 복구·오프라인은 낙하를 멈춘다.
- 컴파일 PASS, `check-work` PASS, 자동 테스트 SKIPPED. 실제 멀티 플레이 확인 필요. 움직이는 발판(리프트) 위에 떨어지면 발판이 떠난 뒤 다시 공중에 남을 수 있음. 손으로 놓기(Drop)를 공중에서 하는 경우는 이번 범위 밖.
- 검사 중 Unity가 Pretendard SDF 폰트 에셋 2개를 바꿈(작업과 무관, 되돌리지 않음).

## 2026-11-05 잡고 있을 때만 반동
- 사용자 요청: 아무도 잡지 않은 트램펄린에서도 점프하면 반동됨 → 손으로 들고 있을 때만 반동.
- 코드에 이미 있던 옵션 사용: `HumanCannon.unity`의 `/Trampoline` `requireCarrierForBounce` 0 → 1, 씬 저장. 정상 상태 운반자가 있을 때만 대포 착지·땅 점프 반동 모두 허용, 운반자 자신은 반동 불가(기존).
- 놓인 트램펄린은 충돌체가 없어 그 아래 바닥에 착지한다. 리허설 코스는 `RehearsalCannonSourceBuilder`를 다시 실행하면 같은 값이 복사된다(이번엔 미실행).

## 2026-11-05 낙사 시 생존자 발밑으로 이동
- 사용자 요청: 들고 낙사하면 시작 지점 대신, 그 순간 살아 있는 플레이어 중 시작 지점에 가장 가까운 사람 위치로. 공중이면 그 아래 지면.
- PR #303 병합 확인 후 Unity 종료 상태에서 최신 `origin/dev`(#305까지) 병합(fast-forward, 충돌 없음).
- `HumanCannonTrampoline.Drop.cs` `TryFindSurvivorGround`: 정상 상태 플레이어 중 트램펄린 최초 위치와 수평 거리가 가장 가깝고 발밑에 지면이 있는 사람의 발밑 지면에 둔다(회전은 최초 회전). 발밑 지면이 없으면 다음 사람. 아무도 없으면 시작 위치. 낙사 아닌 사망인데 아래 지면이 없을 때도 같은 처리. `TryFindGround`는 시작 위치를 인자로 받게 바꿈.
- 트램펄린 중심이 생존자 발 위치라 생존자가 천 위에 겹쳐 보인다. 잡고 있을 때만 반동 설정이라 튕기지는 않는다.
- 검사: Unity 에디터가 닫혀 있어 `check-work` 컴파일 ENV_BLOCKED, 문서/규칙 PASS. 대신 `dotnet build Assembly-CSharp.csproj`로 이번 파일 오류 없음 확인(남은 1개는 dev의 새 `SpatialLogAdapter`가 오래된 csproj에 없어서 생긴 것). Unity 컴파일·플레이 확인 필요.
- 추가 요청: 고른 생존자가 대포 안(`HumanCannonSystem.IsSeated`)이거나 발밑에 지면이 없으면 생존자 수평 정면 반대쪽 2m 뒤에 둔다. 그 아래 지면이 있으면 지면 위, 없으면 생존자 높이. 이제 지면 없는 생존자를 건너뛰지 않고 시작 지점에 가장 가까운 생존자를 항상 고른다.
- 검사(재실행): Unity 컴파일 PASS, `check-work` PASS, 자동 테스트 SKIPPED. 실제 멀티 플레이 확인 필요.
- 추가 요청: 2m 뒤 아래에도 지면이 없으면 2m 더 뒤(4m)를 본다. 거기도 없으면 4m 뒤 생존자 높이. Unity 컴파일 PASS.
