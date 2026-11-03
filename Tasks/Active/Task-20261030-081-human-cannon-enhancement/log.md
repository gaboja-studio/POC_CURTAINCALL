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
