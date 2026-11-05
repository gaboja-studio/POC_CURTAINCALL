# Log

- [이전 제출 기록](History/001-log-before-submission.md)
- [완료한 코스·톱벽 조정 원문](History/002-log-before-dev-integration.md)

## 2026-11-03 이동 장치: 이동·복귀 중 탑승 차단
- 담당자 요청: 승강기/이동 발판이 출발했거나 빈 채로 자동 복귀하는 동안 다른 플레이어가 올라타지 못하게.
- 원인: 발판은 운반자만 위치를 맞춰 옮기고, 그 밖의 사람은 그냥 발판 위에 서 있을 수 있었다. 함정 켠 장치(ShelterLift·ShelterDeparture)는 이동 중 2인째가 들어오면 바닥이 열려 운반자까지 떨어졌다.
- `HumanCannonTransport.PresentBarrier`: 이동(Moving)·복귀(Returning) 중에만 발판 둘레 바깥에 투명 벽 4장(`BoardingBarrier`, 높이 `barrierHeight` 3m, 두께 0.3m, 지붕 없음)을 켠다. 실행 중 생성, 씬 변경 없음.
- `HumanCannonRouteSystem.EjectStowaways`: 벽을 넘어(위에서 떨어지거나 뛰어들어) 발판 영역에 들어온 사람을 각 소유자 클라이언트가 가장 가까운 옆 벽 바깥으로 옮긴다(호스트는 봇 포함). 비행·대포 착석·이동 맡김 중인 사람은 제외.
- `CountPassengers`: 이동·복귀 중에는 운반자만 센다 → 밀려나기 전 늦은 위치가 과승 함정·복귀 대기를 건드리지 않는다.
- 결정: 지붕은 두지 않았다(지붕 위에 서서 같이 이동하는 문제). 위에서 뛰어든 사람은 옆으로 밀려나 아래로 떨어질 수 있다.
- 테스트: `RouteGeometryTests`에 벽 4장·발판 영역 밖·바닥 높이, 가장 가까운 벽 바깥 위치 검사 추가. EditMode `CurtainCall.HumanCannon` 576/576 PASS. 실제 멀티 플레이 미확인.
- PlayMode `FinalCannonPlayTests` 전체: 33 PASS / 16 FAIL / 2 SKIP(로컬 봇 실행파일 필요, `ShelterLift_*` 포함). 실패는 톱(FastSaw 3·GroundSaw·일반 위험)·그네(Pole 4)·난이도 2·비행 표시·최종 비행·철망 부상과 `AppliedMetalTransports_FitRoundLoad...`(DepartureTransport `ContainsLoad` — 이번 변경이 건드리지 않은 발판·트램펄린 크기 검사). 이번 변경 전 기준 실행은 하지 않아 기존 실패인지 미확인.

## 2026-11-03 2구간 추격 불 숨김
- 담당자 요청: 배치된 추격 불 2개(`SplitRouteCourse/FirstSectionFire`, `SecondSectionFire`) 중 1구간만 남기고 숨김.
- `SecondSectionFire`: GameObject 비활성 + `available` 끔 → 예고·전진·접촉 사망·표시 모두 없음. 경로 시스템 `fires` 참조와 배치는 남겨 다시 켤 수 있다. 씬 저장.
- 테스트 기대값 갱신: `RouteCourseTests.TwoFireSections_*`(1구간 사용·2구간 숨김), PlayMode `FireWalls_*`(쉼터 대포 발사에도 2구간 불 예고 없음, 상태 "사용 안 함"). EditMode `RouteCourseTests` 17/17 PASS, PlayMode `FireWalls_WaitThirtySecondsAfterDeparture_KeepSpeed_AndRestart` PASS.
- 담당자 확인 요청: 1구간 불은 "출발 이동 장치 출발 또는 출발 대포 1~3 발사" 때 추격 시작. 이미 그렇게 동작함(`transports[0]` 출발 → `ArmFire(0)`, 대포 0~2번 `Cannon`·`DepartureCannon2`·`DepartureCannon3` 발사 → `FireSectionForStation`=0). 코드 변경 없음. 연결을 고정하는 `RouteCourseTests.FirstFire_ArmsFromDepartureTransportOrAnyOfTheThreeDepartureCannons` 추가, `RouteCourseTests` 18/18 PASS. 첫 출발 뒤 30초 예고 후 전진, 한 라운드에 한 번만 켜짐(이후 출발은 예고를 늦추지 않음).

## 2026-11-03 1구간 추격 불: 최종 대포까지 계속 전진
- 담당자 요청: 1구간 끝(z72)에서 멈추지 말고 도착 코스 앞 최종 대포 4대까지 오게.
- `SplitRouteCourse/FirstSectionFire/Boundary`를 (0,0,72) → (0,0,147.13)(FinalCannon1~4 줄)로 옮기고 씬 저장. 속도 0.7m/s·예고 30초·폭 ±25m·판정 높이 24m는 유지 → 쉼터 갑판(y6)·2구간·쿠션(z143)·최종 대포(y6.2) 위도 접촉 사망.
- 이동 거리 약 158m → 전진 약 226초, 예고 포함 약 256초 뒤 종료. 쉼터 대포 탑승점(z77.5) 도달은 전진 시작 후 약 126초.
- 테스트: `RouteCourseTests.TwoFireSections_*`의 1구간 경계 기대값을 최종 대포 줄로 갱신. EditMode `RouteCourseTests` 18/18 PASS, PlayMode `FireWalls_WaitThirtySecondsAfterDeparture_KeepSpeed_AndRestart` PASS. 실제 플레이 난이도(쉼터 이후 압박) 미확인.

## 2026-11-03 1구간 추격 불 예고 30초 → 20초
- 담당자 요청: 출발 대포 발사(또는 출발 이동 장치 출발) 뒤 불이 오기 전 대기를 20초로.
- `FirstSectionFire.warningSeconds` 30 → 20, 씬 저장. 숨긴 2구간 불은 30초 그대로.
- 결과: 켜진 뒤 20초에 z-11에서 전진 시작, 출발 대포 탑승점(z1.5) 약 38초, 대포 본체(z3) 40초, 최종 대포 줄 약 246초 뒤 종료.
- 테스트: `RouteCourseTests.TwoFireSections_*`, PlayMode `FireWalls_*`(20초 예고) 기대값 갱신, 둘 다 PASS(테스트 이름의 Thirty는 유지).

## 2026-11-03 1구간 추격 불 예고 20초 → 25초
- 담당자 재지시: 켜진 뒤 0~25초 예고, 25초에 전진 시작. `FirstSectionFire.warningSeconds` 25, 씬 저장.
- 도달 시각(켜진 시점 기준, 출발 z-11, 0.7m/s): 출발 대포 탑승점 z1.5 약 43초, 출발 대포 z3 45초, 쉼터 대포 탑승점 z77.5 약 151초, 최종 대포 탑승점 z145.63 약 249초, 최종 대포 줄 z147.13 약 251초(4분 11초, 전진 약 226초) 뒤 종료.
- 테스트 기대값 25초로 갱신. EditMode `TwoFireSections_*` PASS, PlayMode `FireWalls_*` PASS.

## 2026-11-03 1구간 추격 불: 구간 도착 시 가속
- 담당자 요청: 1구간 속도는 유지, 생존자 전원이 2구간에 도착하면 빨라지고, 3구간 도착 시 더 빨라지게. 담당자 선택: 1.3배(0.91m/s) → 1.6배(1.12m/s).
- `HumanCannonFireWall`: `stageSpeedMultipliers`·`stageLines`(구간 시작선) 추가. 단계 시각부터 그때 속도로 이어 계산(앞으로 순간이동 없음). `EndTime`·`Tick`도 단계 반영. 앞 단계를 건너뛰면 같은 시각에 함께 올림. 재시작 때만 초기화.
- `HumanCannonRouteSystem.BoostFire`(호스트): 살아 있는 플레이어 전원이 비행 중이 아니고 시작선을 넘었으면 단계 상승. 2구간은 쉼터 갑판(승강기 도착 갑판 포함)·쉼터 대포 착석도 도착으로 본다. 단계 시각은 상태 패킷으로 클라이언트에 전송(검증: 켜진 뒤, 앞 단계 이후). 테스트 HUD 상태에 "전진 중 0.91m/s(2구간)" 표시.
- 씬: `FirstSectionFire/Stage2Line`(z72, 쉼터 갑판 앞), `Stage3Line`(z143, 최종 대포 앞 쿠션 시작) 추가, 배율 1.3/1.6, 저장. 숨긴 2구간 불은 단계 없음.
- 구간 이동 시간: z-11→72(83m) 0.7m/s 약 119초 / z72→143(71m) 0.7 약 101초·0.91 약 78초·1.12 약 63초 / z143→147.13 1.12 약 3.7초.
- 테스트: `RouteGeometryTests` 단계 3개(이어 계산·EndTime·건너뛴 단계·예고 중 가속·수신·시작선), `RouteCourseTests` 씬 단계 구성. EditMode `CurtainCall.HumanCannon` 580/580 PASS, PlayMode `FireWalls_*` PASS. 실제 멀티 플레이에서 가속 시점 체감 미확인.

## 2026-11-03 1구간 추격 불: 빨간 상자 → 불꽃 파티클 벽
- 담당자 요청: 빨간 상자 대신 그 크기(폭 50m·높이 24m)에 맞는 추격 불 파티클.
- 새 프리팹 `Assets/Scenes/Tests/MainStage/HumanCannon/Prefabs/HumanCannonChaseFire.prefab`: 줄타기 `ChaseFireWall`(Task-074)을 복사해 키움(원본·머티리얼 미수정, `ChaseFire.mat` 참조). Flames 수명 1.0~1.6·크기 4.5~7.5(세로 1.4배)·상승 11~17m/s·초당 900, Core 크기 4~6.5·초당 360, Embers 높이 0~12m 방출·상승 6~13m/s, 노이즈 4배 스케일, 점광원 y6·세기 30·범위 45. 모양 배율 모드라 X 배율 50으로 폭을 맞춘다.
- `HumanCannonFireWall.effect`: 있으면 상자 렌더러를 끄고 파티클 벽을 전선 바닥(Front)에 배치·X 배율=판정 폭. 예고 중 방출 35%, 전진 중 100%. 재시작·종료·비활성 때 숨김. 판정·상자 transform(크기 표시)은 그대로.
- 씬: `FirstSectionFire/ChaseFireEffect` 프리팹 인스턴스 연결, `FlameFront` 렌더러 끔, 저장. 숨긴 2구간 불은 상자 그대로(보이지 않음).
- 테스트: `RouteGeometryTests.FireEffect_*`, `RouteCourseTests` 파티클 연결 검사, PlayMode `FireWalls_*`에 예고 중 표시·재시작 숨김 검사. EditMode `CurtainCall.HumanCannon` 581/581 PASS, PlayMode `FireWalls_*` PASS.
- 확인: Play 중 임시 카메라(런타임 복사본 예고 0초)로 출발 바닥 뒤 폭 전체 불꽃 벽 캡처 `Previews/chase-fire-particles.png`. 실제 플레이 체감·성능 미확인.
- 함정: `capture_game_view`의 `save_path`는 `Assets/` 기준이라 `Assets/Tasks/`가 생겼다 → 저장소 `Tasks/`로 옮기고 삭제.

## 2026-11-03 2구간 전원 도착 시 남은 예고 건너뜀
- 담당자 요청: 생존자 전원이 2구간 대포 지면(쉼터)에 닿으면 남은 예고 시간과 상관없이 불이 바로 출발. 담당자 선택: 위치는 그대로, 남은 예고만 건너뜀.
- `HumanCannonFireWall.AdvanceAt` = min(켜진 시각+예고, 2구간 도착 시각). `Advancing`·`RemainingWarning`·`Travelled`·`EndTime`이 이 시각을 쓴다. 도착 판정·네트워크는 기존 2구간 가속(`BoostFire`, z72 시작선·쉼터 갑판·쉼터 대포 착석, 비행 중 제외)을 그대로 사용 → 추가 동기화 값 없음. 이미 전진 중이면 기존처럼 1.3배 가속만.
- 테스트: `RouteGeometryTests.FireStages_SkippedStageRisesTogether_AndArrivalDuringWarningStartsTheChaseAtOnce`로 갱신. EditMode `CurtainCall.HumanCannon` 581/581 PASS, PlayMode `FireWalls_*` PASS. 실제 플레이로 25초 안에 전원 쉼터 도착하는 경우 미확인.

## 2026-11-03 dev 병합·PR 273 갱신 제출
- 커밋 `1282641b`(이동 장치 탑승 차단·추격 불 고도화) push. PR #273이 dev와 충돌 → `origin/dev`(PR #274·#275) 병합.
- 충돌: `HumanCannonPoleSystem.Network.cs` 그네 상태 플래그 바이트. dev는 비트 1~4를 뒤로 앉은 좌석에, Task-081은 비트 1을 톱벽 열림에 썼다 → 비트 0 지상 담당 통과, 비트 1~4 뒤로 앉은 좌석, 비트 5 톱벽 열림(`GateOpenFlag`)으로 나누고 알 수 없는 비트는 거부.
- 병합 후 EditMode `CurtainCall.HumanCannon` 587/587 PASS, PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS. 병합 커밋 `46c97375` push.
- PR #273 제목·본문을 이번 묶음 기준으로 갱신(MERGEABLE). 자동 변경 파일(글꼴·escape-labels·vivox·_ThirdParty·Task-083 미리보기)은 제외. `check-work.ps1` 미실행, 실제 플레이 미확인.
- 함정: Unity 에디터가 백그라운드일 때 Play 테스트가 느려질 수 있어 `editor_focus`로 앞으로 가져왔다. Git Bash의 `ps | grep sleep` 대기 루프는 즉시 끝나므로 실제 시간 기준으로 기다려야 한다.

## 2026-11-04 승강기 트램폴린 반동 동승 차단
- 문제: 트램폴린+운반자 1인이 승강기에 오르고 다른 단원이 트램폴린에서 높이 튀면 같이 올라갔다. 트램폴린 반동은 좌석 없는 비행(`StationIndex -1`)이라 탑승자 판정(발판 위 0~2m)과 이동 중 밀어내기(비행자 제외)를 모두 피했다.
- 수정: `HumanCannonTransport.ContainsAirColumn`(발판 면적 위 높이 제한 없음) 추가. 대기 중 발판 위로 반동 비행 중인 사람도 탑승자로 세어 출발을 막고(함정을 끈 좌측 DepartureTransport는 빨강 발판으로 거부. 함정이 켜진 ShelterLift·ShelterDeparture는 과승으로 바닥이 열림), 이동·복귀 중에는 발판 위 트램폴린 반동을 막아 착지하면 기존 밀어내기로 벽 밖에 내린다.
- 테스트: `RouteGeometryTests.AirColumn_CoversHighTrampolineBounceButNotOutsideOrBelow` 추가. EditMode `CurtainCall.HumanCannon` 587/588(추가 테스트의 -0.15m 경계 부동소수 오차) → 테스트 높이 수정 후 `RouteGeometryTests` 53/53 PASS. `check-work.ps1` 미실행, 실제 멀티 플레이 미확인.
- 후속(사용자 결정): 쉼터 `ShelterLift`·`ShelterDeparture`도 `overloadTrap` 끔 → 세 장치 모두 과승 시 바닥을 열지 않고 빨간 발판으로 출발만 막는다. 씬 변경은 두 값과 Unity가 저장 시 쓴 기본값 `useGrabFacing: 0` 한 줄. 테스트 `RouteCourseTests.EveryLift_RefusesOverloadWithoutOpeningFloor`로 세 장치 확인. EditMode `CurtainCall.HumanCannon` 588/588 PASS. 실제 플레이 미확인.
- 재현 확인(사용자 설명: 트램폴린+1인 탑승 승강기가 움직일 때 트램폴린으로 점프해 대포 없이 같이 가는 악용): PlayMode `FinalCannonPlayTests.MovingLift_TrampolineBouncerCannotRideAlong` 추가. 수정 끈 상태에서는 반동자가 이동 발판 위에 남아 FAIL, 수정 상태에서는 재반동 없이 착지 후 밖으로 밀려나 PASS. 단일 호스트 재현이며 실제 2인 플레이는 미확인.
- 2인 실제 검증(사용자 요청): 로컬 연습 봇에 검사 전용 역할 `LiftJumper`(출발 승강기 뒤 갑판에서 대기 → 출발 즉시 트램폴린으로 뛰어듦)·`LiftBouncer`(운반 중 트램폴린에서 계속 반동) 추가. HUD에는 노출하지 않는다.
  - 새로 찾은 구멍: 운반자가 든 트램폴린은 발판 밖으로 튀어나올 수 있어, 튀어나온 부분 위 반동자는 탑승자로 세지 않았고 이동 중 반동도 막지 않았다(봇이 출발 전부터 반동 → 운반자 혼자로 보고 출발 → 같이 도착).
  - 수정: 운반자가 실려 가는 동안 그 트램폴린의 반동을 모두 막는다(`BlocksTrampolineBounce`). 출발 전에는 운반자가 발판 위일 때 트램폴린 주변(적재 반경+2m, 트램폴린 면보다 위)에서 반동 중인 사람도 탑승자로 센다(`BouncingAbove`).
  - PlayMode `TwoPlayers_JumpOntoDepartedLiftTrampolineDoesNotRideAlong`·`TwoPlayers_BouncingBeforeDepartureDoesNotRideAlong` 추가. 수정을 끄고 봇까지 다시 빌드하면 둘 다 FAIL(봇이 대포 없이 함께 도착), 수정 상태로 PASS. `MovingLift_TrampolineBouncerCannotRideAlong` PASS, EditMode `CurtainCall.HumanCannon` 588/588 PASS.
  - 함정: 봇 빌드는 개인 EditorPrefs의 로그·설문 태그 검사에 막힌다 → 빌드 동안만 끄고 복원. 봇 역할 교대(`SetRole`)는 출발점 재시작을 일으켜 트램폴린이 초기 위치로 돌아간다. 운반 방향은 몸 회전이 아니라 3인칭 시선을 따른다(`CameraViewDirector.SetLookRotation`). 승강로 위로 내민 트램폴린에서 반동한 봇은 떨어져 재시작을 일으키므로 검사는 갑판 쪽으로 돌린다. MCP `run_tests`가 400을 돌려도 실행이 시작된 경우가 있어 재요청하면 실행이 겹쳐 중단된다 → `editor_status`로 먼저 확인.
- 후속(사용자 요청: 남은 1·2·3 모두 적용):
  1. 세 장치 공통 — 판정은 장치 번호별 공통 코드라 DepartureTransport·ShelterLift·ShelterDeparture 모두 같다. 되돌릴 갑판(`BoardingSide`)이 세 장치 모두 실제 바닥(y 4·0·6) 위인지 레이캐스트로 확인.
  2. 네트워크 지연 — 출발 순간 판정을 놓쳐도 이동 중에는 반동이 서버에서 막히고, 착지하면 아래 3과 기존 밀어내기로 내려진다.
  3. 트램폴린 위 버티기 — 이동 중 실려 가는 운반자의 트램폴린 위(면 아래 0.5m~위 2m, 트램폴린 반경+몸 반경)에 선 사람은 소유자 클라이언트가 탑승하던 갑판(`HumanCannonTransport.BoardingSide`)으로 되돌린다.
  - EditMode `CurtainCall.HumanCannon` 589/589 PASS(`BoardingSide_IsBehindTheStartPlatformOppositeTheTravel` 추가). 봇 재빌드 후 PlayMode `RideAlong` 3개 PASS. 트램폴린 위 버티기·쉼터 장치 2개·실제 지연 환경의 2인 플레이는 자동 검사 없음, 사람 확인 필요.

## 2026-11-04 일반 공중그네 숨기기
- 사용자 결정: 톱벽을 작동시키는 연결 그네 2개(`PoleGateCourse/LinkedPole`, `SecondSectionPoleGate/LinkedPole`)만 남기고 나머지 그네는 숨김(삭제 아님). 숨긴 것: `SecondSectionPoleGate/NormalPole`+`ExtendedCourse044/ExistingCeiling2`, `HumanCannonEnhancement/AddedPole0`+`AddedPole0Ceiling`(`m_IsActive: 0`). 비활성 그네는 `HumanCannonPoleSystem.Valid`가 이미 잡기를 막고 HUD도 건너뛴다.
- 테스트: `RouteCourseTests.FourPoles_KeepOrderAndBothCooperationGates`에 표시/숨김 확인 추가. 그네 공통 동작 PlayMode 검사(일반 그네 index 1 사용)는 검사 안에서만 숨긴 그네를 다시 켠다(`ShowHiddenPole`). EditMode `CurtainCall.HumanCannon` 589/589 PASS, PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS. 나머지 그네 PlayMode 묶음은 사용자 요청(핵심만)으로 중단·미실행.

## 2026-11-04 도착지점 전 초록 지면 안전지대
- 사용자 결정: 최종 대포 앞 초록 면(`FinalGreenGround`) 전체를 안전지대로 한다(공중에서 떨어져도 부상 없음). 기존에는 쿠션 3면·준비 갑판만 착지 구역이라 초록 면의 앞쪽 띠와 좌우·뒤 1.2m 경사 테두리(아래가 지상 6.0)에 떨어지면 부상이었다.
- 수정: `FinalCannonCourse/FinalGreenGroundLanding`(초록 면과 같은 31.4×6.2m, 윗면 y=6.0인 BoxCollider + `HumanCannonLandingZone`) 추가. 지상 바닥과 같은 높이라 지형은 바뀌지 않는다. `FinalGreenGround`는 외형 전용(충돌체 없음) 유지.
- 테스트: `FinalCannonCourseTests.GreenCushion_WrapsPreparationDeckOnBackLeftAndRight`에 초록 면 안·밖 판정 추가, `AppliedCannonArtTests` 충돌체 수 61→62. EditMode `CurtainCall.HumanCannon` 589/589 PASS. 실제 낙하 플레이 확인은 사람 몫.

## 2026-11-04 출발지점 지면 거리 표시 일부 삭제
- 사용자 요청: 지면의 노란 줄·검정 판을 지우되 우리가 만든 가이드는 남긴다. 1차로 출발지점(높이 4 출발 갑판)만.
- 삭제(19개, `DistanceMarkers` 아래): 노란 5m 줄 `Forward_-20m/-15m/-10m/-5m`(distance-major), 검정 판 `LabelPlate` 15개(z -14.8~5.2, distance-label). 회색 1m 줄·하늘색 0m 줄·거리 글자(갑판 아래 y=0.04에 있어 원래 보이지 않음)·z>5 구간 표시는 그대로.
- 검사: 이 표시를 참조하는 자동 테스트 없음. 씬 뷰 캡처로 출발 갑판에 노란 줄·검정 판이 없음을 확인.
- 2차(다음 구간, 1구간 지면 z 19.5~40.2): 노란 5m 줄 `Forward_15m~35m` 5개와 검정 판 15개 삭제(20개). `DistanceMarkers`의 활성 노란 줄·검정 판은 이제 없다(비활성으로 숨겨져 있던 z 7.5~15 것은 그대로). 회색·하늘색 줄과 거리 글자는 남김.
- 3차(사용자 요청): 1구간 바닥의 검은 상자 = 구간 표지판 `HumanCannonEnhancement/Section1`("1구간 · 출발" 판+글자) 삭제. 쉼터 표지판(`HumanCannonEnhancement/Shelter`)은 유지. 주의: `HumanCannonEnhancementBuilder`를 다시 실행하면 표지판이 다시 생긴다(빌더 미수정).
- 4차(사용자 수동 삭제): `DistanceMarkers` 루트 전체(회색·하늘색 줄·거리 글자 포함)를 사용자가 씬에서 직접 삭제. 이때 1구간 지상 톱 `GeneralHazards044/GroundSaw1C`(z=22.5)도 함께 지워져 `hazards[8]`이 비고 EditMode 3개가 실패 → 사용자 요청으로 복구. HEAD 씬을 임시 자산으로 열어 원래 오브젝트를 옮겨 왔다(프리팹 연결·형제 순서 8 유지, `hazards[8]` 재연결, 임시 자산 삭제). 복구 후 EditMode `CurtainCall.HumanCannon` 589/589 PASS.
- 최종 검토(사용자 수동 삭제 후): 원래(HEAD) 씬 대비 지워진 것은 `DistanceMarkers` 전체, 구간 표지판 4개의 판·글자(`Section1`·`Final`은 통째, `Shelter`·`Section2`는 빈 부모만 남음), 화면 현재 구간 글자 `HumanCannonSectionCanvas/CurrentSection`. `HumanCannonUiBinding.sectionLabel`이 비지만 코드가 null을 허용해 화면 구간 표시만 사라진다. 끊어진 참조 없음, 판정 오브젝트(승강기·대포·트램폴린·착지 구역·톱·톱벽 그네) 유지, EditMode `CurtainCall.HumanCannon` 589/589 PASS.
- 빈 표지판 부모 `HumanCannonEnhancement/Shelter`·`Section2` 삭제(사용자 요청). 참조하는 테스트 없음.
- 지상 톱 표시 정리(사용자 요청): 씬에는 톱 12개가 있지만 `SawDensity` 0.6으로 플레이 중 9개만 켜진다. 플레이에서 꺼지는 `GroundSaw1A`(z50)·`GroundSaw2AExtra`(z115)·`GroundSaw2B`(z131.5)를 씬에서도 비활성화해 9개만 보이게 했다. 주의: 밀도를 올려도 이 3개는 씬에서 꺼져 있어 나타나지 않는다(다시 켜야 함). `CannonHazardCourseTests`를 "밀도로 켜지는 톱만 활성, 9개"로 갱신. EditMode `CurtainCall.HumanCannon` 589/589 PASS.
- 톱 경로 파란 선(사용자 요청): 플레이 중에는 `HumanCannonHazard.SetVisible`이 레일(LineRenderer)을 실제 경로(`Travel`, 공용 30° 대각선 포함)로 다시 그리지만 씬에는 직선(x ±24, 같은 z)이 저장되어 있었다. 켜진 톱 9개의 레일을 플레이 경로와 같게 저장(대부분 z ±13.9 대각선, 직접 그린 1C·1AExtra는 ±4.2, 1D는 직선 그대로). 꺼진 톱 3개는 손대지 않음. EditMode `CurtainCall.HumanCannon` 589/589 PASS.
- 4·5번 톱 수평(사용자 요청): `GroundSaw1B`(z55.5)·`GroundSaw1BExtra`(z61)의 `authoredPath`를 켜 공용 30° 대각선 대신 작성한 수평 경로(x ±24, z 고정)를 쓰게 하고 파란 레일도 같게 저장. 작성 경로 톱은 밀도와 무관하게 항상 켜지지만 두 톱은 원래 켜져 있어 켜진 톱은 그대로 9개. `CannonHazardCourseTests` 작성 경로 3→5, 대각선 6→4로 갱신. 직전 전체 EditMode 588/589(이 테스트만 실패) → 갱신 후 `CannonHazardCourseTests` 8/8 PASS.
- 4번 톱 기울기(사용자 요청): `GroundSaw1B` 작성 경로를 (-24, 0, -6.6)으로 기울여 파란 선이 1구간 톱벽(`PoleGateCourse/SawGate`, 뒤 가장자리 z48.16)에서 0.7m 밖(가장 가까운 끝 z48.9, 0.74m)에 있게 했다. 다른 끝은 x=24·z62.1이라 5번 톱 수평선(z61)과 x≈20에서 교차한다. EditMode `CurtainCall.HumanCannon` 589/589 PASS.
- 4·5번 톱 최종 배치(사용자 요청 순서대로): 4번 `GroundSaw1B`는 왼쪽 끝(x-24, z48.9, 톱벽 뒤 가장자리 z48.16에서 0.74m)을 고정하고 오른쪽 끝을 z62.1→59.0→57.0으로 낮춤(중심 z55.5→52.95, 5번과 겹침 해소). 5번 `GroundSaw1BExtra`는 중심 z61을 유지하고 4번과 평행(같은 기울기: x ±24, z 56.95↔65.05). 두 선 간격 z 8.05m. `HumanCannonWideCourseTests` 톱 간격 검사를 씬에서 보이는 톱끼리만 보도록 수정(숨긴 1A 제외). EditMode `CurtainCall.HumanCannon` 589/589 PASS(5번 평행 전), 평행 후 `HumanCannonWideCourseTests` 6/6 PASS.
- 5번 톱 `GroundSaw1BExtra`를 다음 구간 쪽(+z)으로 0.3m 이동(중심 z61→61.3, 경로 z 57.25↔65.35, 4번과 간격 8.35m). 레일은 로컬 좌표라 함께 이동.
- 6·7번 톱 X자(사용자 요청): `GroundSaw2C` 중심 z108.25 경로 (-24,116.5)↔(24,100.0), `GroundSaw2D` 중심 z113.75 경로 (-24,105.5)↔(24,122.0), 둘 다 작성 경로. 제약: `ShelterDeparture` 도착 발판+벽(z99.3)·2구간 톱벽 앞면(z122.7)에서 각각 0.7m, 톱 중심 간격 5.5m 유지. 두 톱 위상차가 π라 서로 거울 위치로 움직여 교차점(x≈-8)에서도 간격이 5.5m 이상. `CannonHazardCourseTests` 작성 경로 5→7. EditMode `CurtainCall.HumanCannon` 589/589 PASS. 참고: 8번 `GroundSaw2A`(z126)의 대각선 파란 선은 2구간 톱벽을 가로지른다(미수정).
- 6·7번 X자 중앙 정렬(사용자 정정: X 교차점이 중앙이어야 함): 두 톱 중심을 같은 (0, z) 위치로, 경로 반대 기울기 ±11m. 같은 중심에서 거울로 움직이면 중앙에서 동시에 만나므로 `GroundSaw2D` phase를 2C+π/2(1.047→2.618)로 바꿔 4분의 1주기 어긋나게 함(최소 톱 간격 15.56m, 난이도 속도 1·1.25·1.5 확인). 이후 사용자 요청으로 엘리베이터 쪽으로 0.5m 내림 → 중심 z110.5, 경로 z 99.5↔121.5(톱벽 앞면 z122.7에서 1.2m, 끝점 x=±24는 엘리베이터 x-12~-6에서 떨어져 있어 엘리베이터 위를 지나지 않음). `HumanCannonWideCourseTests` 간격 검사에 X자 쌍 예외(반대 기울기·5.5m 이상 떨어져 지남) 추가. EditMode 589/589 PASS(내리기 전), 내린 뒤 `HumanCannonWideCourseTests` 6/6 PASS.
- 8·9번 톱 X자(사용자 요청): `GroundSaw2A`·`GroundSaw2BExtra`를 6·7번과 같이 같은 중심·반대 기울기(±8.55m)·4분의 1주기 차이(2BExtra phase 3.927→1.571, 최소 톱 간격 12.09m)로 배치. 처음 중심 z132.55(톱벽 뒷면 z123.3·초록 지면 z141.8에서 0.7m씩) → 사용자 요청으로 초록 지면 쪽 0.3m 이동해 중심 z132.85, 경로 z 124.3↔141.4(톱벽에서 1.0m, 초록 지면에서 0.4m). 켜진 톱 9개 모두 작성 경로. 테스트 기대값 갱신(`CannonHazardCourseTests` 작성 경로 9·대각선 0, `HumanCannonWideCourseTests` 2구간 마지막 톱 z132.85). 사용자가 전체 테스트 실행을 중단해 이 변경 뒤 테스트는 미실행.
- 6·7번 X자 전체를 엘리베이터 쪽으로 0.3m 더 내림(사용자는 '5번 6번 X자'라고 했으나 엘리베이터 쪽 X자는 6·7번): 중심 z110.2, 경로 z 99.2↔121.2. 선 끝 x=±24는 모든 톱과 같이 바닥 폭(±22) 밖이며 바닥 안쪽은 z99.2에도 바닥이 있다. 테스트 미실행.
- 6·7번 X자를 0.3m 더 내림(사용자 표현 '5번 6번', 직전과 같은 X자로 해석): 중심 z109.9, 경로 z 98.9↔120.9. 엘리베이터 구간(x-12~-6)에서 선은 z104.4 이상. 테스트 미실행.
- 8·9번 X자를 초록 지면 쪽으로 0.4m 더 올림: 중심 z133.25, 경로 z 124.7↔141.8(끝점 x=±24는 초록 지면 x범위 ±15.7 밖, 초록 지면 가장자리 x=±15.7에서 선은 z138.84로 지면 시작 z141.8에서 약 3m). 톱벽 뒷면에서 1.4m. 테스트 기대값 z133.25로 갱신, 테스트 미실행.
- X자 교차점 전기톱 추가(사용자 요청, 사용자 번호 '5·6번', '7·8번' X = 실제 6·7번·8·9번): 기존 톱을 프리팹 연결째 복제해 `GroundSaw2XCenter1`(중심 z109.9)·`GroundSaw2XCenter2`(z133.25)를 만들고 `GeneralHazards044.hazards` 끝(13·14번째)에 등록. 경로는 교차점을 지나는 수평(x ±24), 작성 경로. X 두 톱이 4분의 1주기마다 번갈아 중앙을 지나므로 새 톱 phase는 X 첫 톱+π/4(1.833, 0.785)로 그 사이에 중앙을 지난다. 최소 톱 간격 X1 9.81m·X2 7.75m(난이도 속도 1·1.25·1.5).
  - 톱이 12→14개가 되며 밀도 계산 개수가 바뀌어 씬에서 숨긴 `GroundSaw2B`·`2AExtra`가 '켜짐'으로 계산됐다 → `HumanCannonHazardCourse.IsHazardEnabled`에 "자기 GameObject가 꺼진 장애물은 꺼짐" 한 줄 추가(`activeSelf`; `activeInHierarchy`는 비활성 루트를 쓰는 테스트 고정물과 충돌). 켜진 톱 11개.
  - 테스트 갱신: 장애물 12→14, 켜진 톱 9→11, 작성 경로 9→11, 2구간 톱 6→8, 간격 검사는 같은 중심 묶음(X자 2 + 수평 1) 단위로. EditMode `CurtainCall.HumanCannon` 589/589 PASS. 실제 플레이·멀티 확인은 사람 몫.
- X자 가로 톱 2개 왕복 4→3초(사용자 요청): `GroundSaw2XCenter1`·`2XCenter2` `periodSeconds` 3. 주기가 X자 톱(4초)과 달라 12초마다 가까워지는 순간이 생기므로 phase를 0~360° 탐색해 최소 간격이 가장 큰 값으로 설정(1: 4.014rad, 최소 2.40m / 2: 1.571rad, 최소 1.88m; 톱날 반지름 0.65라 겹치지 않음, 난이도 속도 무관). `HumanCannonWideCourseTests`: 이 두 톱만 3초 허용, 한 주기 뒤 같은 위치 검사를 각 톱 주기로, 주기가 다른 묶음은 12초 동안 반지름 합+0.3m 초과로 검사. 전체 EditMode 588/589(주기 검사 1개) → 수정 후 `HumanCannonWideCourseTests` 6/6 PASS.
- 톱 경로 파란 선 전부 숨김(사용자 요청): 플레이 중 `HumanCannonHazard.SetVisible`이 레일을 다시 켜므로 `showRail` 직렬화 옵션(기본 true)을 추가하고 `rail.enabled = visible && showRail`. 씬의 지상 톱 14개 모두 `showRail=false`, LineRenderer 비활성. 레일 컴포넌트·경로 값은 남아 있어 다시 켜면 그대로 보인다. `HumanCannonWideCourseTests` 6/6, `HumanCannonPoleSawTests` 10/10 PASS. 전체 EditMode는 미실행.

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
