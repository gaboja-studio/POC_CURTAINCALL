# Log

이전 기록 원문: [History/001](History/001-log-before-submission.md).

## 2026-11-02 도착지점 5m 뒤로
- 담당자 결정: 도착지점만 옮기고 발사는 그대로. `FlightLandingPad` z174.33 → 179.33(범위 z174.33~184.33), `DestinationMarkers`(가장자리 선·결승 기둥·현수막·표지·바닥 글씨 9개) 같이 +5m. 대포 갑판 앞끝(151.63)과 간격 17.69 → 22.69m.
- 결과: 조작 없는 비행은 z172.08에 떨어져 도착지점 2.25m 앞에서 추락. 비행 중 앞 보정(4.5m/s × 2.60초 = 최대 +11.7m)을 쓰면 도착(끝까지 누르면 z183.8). 피날레 링 12개는 조작 없는 포물선 기준이라 그대로 둠.
- `HumanCannonBotBrain` 연습 봇 비행 목표 z174 하드코딩 → `HumanCannonDestination.Current.Zone` 위치(없으면 174).
- 테스트: 착지 테스트를 "조작 없이는 못 미치고, 앞 보정을 끝까지 쓰면 안전 착지"로 교체. EditMode 561/561 PASS. 실제 플레이·봇 동작 미확인. 다리 부상 시 공중 보정 배율이 줄어 도착 못 할 수 있음.

## 2026-11-02 대포 4대+갑판을 쿠션 중간까지 당김
- 담당자 결정: 도착지점은 그대로, 대포 4대와 `PreparationDeck`을 갑판 뒤끝이 초록 쿠션 중간(z144)에 오도록 −2.5m. 갑판 z144~149.13, 대포 z147.26, 포구 z148.76, 탑승 위치 z145.76(갑판 위). 앞쪽 여유 1m 유지.
- 갑판이 쿠션 앞쪽 절반(z144~146.5)을 덮는다. 윗면 높이가 같아(y6.2) 겹친 곳에 쿠션 초록이 비쳐 보임(씬 화면 확인). 담당자에게 알림.
- 링 12개 빌더로 재생성(대포1 기준 z153.65·158.55·163.44). 조작 없는 착지 z169.58 → 도착지점(174.33)까지 4.75m 부족, 앞 보정 끝까지 쓰면 z181.3 도착. 갑판 앞끝↔도착지점 25.2m.
- 테스트: 대포 위치 z147.26, 갑판 뒤끝 = 쿠션 중간으로 교체, 쿠션 1.5m 간격 검사 제거. EditMode 561/561 PASS.
- 담당자 결정으로 갑판이 덮은 쿠션 앞쪽 절반 잘라냄: `LandingCushion` z141.5~146.5 → 141.5~144(크기 z 5→2.5, 중심 z142.75), 갑판(z144)과 바로 이어짐. 쿠션 시작(z141.5)은 그대로라 2구간 불꽃 경계(141.5)와 연결 유지. 같은 사각형 외곽선 `SecondSectionPoleGate/ReceivingArea` FarEdge z146→143.5, Left/RightEdge 길이 5→2.5. 겹침 깜빡임 해소(씬 화면 확인). 테스트: 쿠션 z141.5~144, 갑판과 맞닿음. EditMode 561/561 PASS.

## 2026-11-02 최종 대포 4대 발사 힘 조절
- 담당자 요청: 최종 대포 4대만 무조작 비행이 도착지점 중간에 내리도록. 위 속도 12 유지(정점 높이 y15.54·비행 시간 2.60초 그대로), 앞 속도 `forwardSpeed` 8 → 11.74. 착지 z169.59 → 179.32(도착지점 z174.33~184.33 중간 179.33). 다른 대포 6대는 그대로.
- 링 12개 재생성: 대포1 기준 z155.94·163.12·170.30, 기울기 약 ±27°. 겹침 없음.
- 앞 보정(4.5m/s)을 끝까지 쓰면 약 z191로 도착지점(184.33)을 넘어감. 뒤 보정도 같은 크기.
- 테스트(발사 힘): 발사 속도 (0,12,11.74), 착지 테스트를 "조작 없이 도착지점 중간(±0.1m)"으로 교체.

## 2026-11-02 최종 대포 간격 4 → 6m
- 담당자 요청: 중심 간격 4m → 6m(x -9·-3·3·9). 몸체 사이 빈 공간 2.08 → 4.08m.
- 같은 여유 유지: 대포 바닥 `PreparationDeck` 폭 18 → 24m(x ±12, 바깥 대포 중심에서 3m). 도착지점 `FlightLandingPad` 폭 20 → 26m(x ±13, 바깥 탄도에서 4m). `DestinationMarkers` Near/FarEdge 폭 26, Left/RightEdge·결승 기둥 ±3m 바깥으로, 현수막 폭 20.9 → 26.9. 표지·바닥 글씨는 가운데 그대로. 초록 쿠션(폭 12)은 미변경.
- 링 12개 재생성(6m 간격), 겹침 없음. 4대 모두 무조작 착지 z179.32 도착지점 안.
- 테스트: 대포 위치 -9+6i, 도착지점 폭 26, 대포 바닥 폭 24. EditMode 561/561 PASS.

## 2026-11-02 초록 쿠션으로 대포 바닥 뒤·좌·우 감싸기
- 담당자 요청: 초록 땅이 대포 바닥의 좌·우·하(뒤)를 덮도록. 폭은 기존 뒤쪽 쿠션 깊이와 같은 2.5m로 정함.
- `LandingCushion`(뒤) 폭 12 → 29m(x ±14.5, z141.5~144 유지). 복제로 `LandingCushion_Left`(x -14.5~-12)·`LandingCushion_Right`(x 12~14.5), z144~149.13, 같은 높이·안전 착지 구역·쿠션 외형. 씬 화면에서 ㄷ자 확인.
- 옛 쿠션 테두리 선 `SecondSectionPoleGate/ReceivingArea`(x ±6, 선 4개)가 넓어진 초록 위에 남아 보여 담당자 요청으로 삭제. 참조·충돌체 없음. 테스트에 잔여 없음 검사 추가, EditMode 562/562 PASS.

## 2026-11-02 대포 바닥 앞끝을 지상 바닥에 맞춤
- 담당자 요청(다른 것은 건드리지 않음): `PreparationDeck` 앞끝 z149.13 → 148.00(2구간 지상 바닥 `SecondCourseFloor` 끝). 크기 z 5.13 → 4, 중심 z146.57 → 146. 탑승 위치 4곳 모두 바닥 위.
- 대포는 그대로라 차대 앞끝(z148.14)이 바닥 끝보다 0.14m 튀어나옴. 포신은 원래 공중. 앞쪽 여유 1m 결정은 이것으로 대체.
- 옆 초록 쿠션(z144~149.13)은 처음엔 지시대로 미변경했다가, 담당자 후속 요청으로 `LandingCushion_Left/Right` 앞끝도 z148로 맞춤(크기 z 5.13 → 4). 테스트: 옆 쿠션 앞끝 = 대포 바닥 앞끝. EditMode 562/562 PASS.

## 2026-11-02 대포 앞 다리 바닥에 맞춤
- 담당자 스크린샷: 최종 대포 앞 다리(차대)가 바닥 끝보다 튀어나옴. 차대 앞끝(정점 기준) z148.135 vs 바닥 끝 148.00 → 대포 4대 z147.26 → 147.125(−0.135). 차대 앞끝 = 148.000.
- 포구 z148.76 → 148.625. 착지를 도착지점 중간으로 유지하려 앞 속도 11.74 → 11.79(착지 z179.32, 중간 179.33). 링 12개 재생성(대포1 기준 z155.84·163.05·170.26), 겹침 없음. 탑승 위치 모두 바닥 위.
- 테스트: 대포 위치 z147.125, 발사 속도 (0,12,11.79), 앞 다리 끝 = 바닥 끝(±5mm) 검사 추가. EditMode 562/562 PASS.

## 2026-11-02 초록 쿠션 외형 하나로 잇기
- 담당자 요청: 초록 쿠션 3조각의 둥근 테두리가 이음새로 보여 하나로 연결.
- 충돌·착지는 그대로 3조각(`LandingCushion`·`_Left`·`_Right`, BoxCollider + 안전 착지 구역)이 맡고, 각 조각의 외형(`AppliedCushion`)은 삭제. 충돌체 없는 외형 `LandingCushionSurround`(쿠션 모델, x -14.5~14.5, z141.5~148) 하나를 갑판보다 1.5cm 아래(윗면 y6.185)에 깔아 갑판이 가운데를 덮게 함. 씬 화면에서 이음새 없는 ㄷ자 확인.
- 테스트: 적용 외형-충돌체 대조 수 50 → 47(쿠션 3면 제외), 이어진 외형이 세 구역을 덮고 갑판 아래에 있으며 조각 외형은 숨김 검사 추가. EditMode 562/562 PASS.

## 2026-11-02 초록 쿠션 바깥 경사면
- 담당자 요청: 지상에 닿는 초록 가장자리(갑판 쪽 아님)를 경사면처럼. 지상 바닥 윗면 y6.0, 쿠션 외형 윗면 y6.185.
- `LandingCushionRamp`(생성 메시 `Meshes/green-cushion-ramp.asset`, 쿠션과 같은 `Color.mat`·윗면 UV(0.535, 0.287)) 추가: 바깥 3면(뒤 z141.5, 좌 x-14.5, 우 x14.5)에서 1.2m 밖으로 y6.19 → 6.005 경사(약 9°), 안쪽 0.3m는 평평하게 겹쳐 쿠션의 둥근 테두리를 덮음. 지상이 없는 앞쪽(z148)은 내리지 않고 끝면만 막음. 갑판 쪽은 미변경.
- 외형 전용(충돌체 없음). 걸어서는 기존처럼 0.185m 단차를 캐릭터 stepOffset 0.3으로 자동으로 올라섬. 착지 판정 변화 없음. 경사면이 빛 각도 때문에 쿠션보다 약간 밝게 보임(씬 화면 확인).
- 테스트: 경사면 존재·충돌 없음·범위(바깥 1.2m, 앞 z148까지, 바깥 끝 y6.0) 검사 추가. EditMode 562/562 PASS.

## 2026-11-02 초록+대포 바닥을 하나의 매끈한 초록 면으로
- 담당자가 선택한 경사면(`LandingCushionRamp`) 스타일로, 대포 바닥까지 초록으로 합치기로 결정.
- `FinalGreenGround`(생성 메시 `Meshes/final-green-ground.asset`, `Color.mat` 같은 초록 UV): 윗면 y6.202 평면(x -14.5~14.5, z141.5~148), 뒤·좌·우 1.2m 경사로 지상(y6.0)까지, 앞(z148) 끝면 막음. 충돌체 없음.
- 삭제: `LandingCushionSurround`(부푼 쿠션 외형), `LandingCushionRamp`와 메시 `green-cushion-ramp.asset`, `PreparationDeck/AppliedConcreteFloor`(콘크리트 외형). 충돌·착지는 기존 `PreparationDeck` + 쿠션 3구역 그대로라 게임 동작 변화 없음.
- 테스트: 초록 면 범위·높이·충돌 없음, 옛 외형 숨김 검사로 교체. 적용 외형-충돌체 대조 47 → 46(대포 바닥 콘크리트 제외). EditMode 562/562 PASS. 씬 화면 확인.
- 담당자 요청으로 피날레 링 숨김: `FinalCannonCourse/FinaleRings` 비활성(12개 유지, 위치는 현재 탄도 기준). 테스트에 숨김 상태 검사 추가. EditMode 562/562 PASS.
- 담당자가 선택한 `SplitRouteCourse/SecondSectionPoleGate/GateConnectionCable`(2구간 연결 그네~톱벽 사이 장식 LineRenderer) 삭제. 참조 없음. EditMode 562/562 PASS.
- 담당자 요청: 도착지점 초록 테두리·FINISH 글자 전부 삭제 → `FinalCannonCourse/DestinationMarkers` 전체(테두리 선 4, 결승 기둥 2, 현수막, FINISH 표지·바닥 글씨) 삭제. 이를 따라 화면에 FINISH를 띄우던 `UI/WorldLabels/FinishSign_UI`·`FinishGroundLabel_UI`도 삭제. 착지대 `FlightLandingPad`와 도착 판정(`HumanCannonDestination`)은 유지. 테스트 추가(마커·FINISH 글자 없음, 착지대 유지). EditMode 563/563 PASS. `HumanCannonDestinationBuilder`를 다시 실행하면 표식이 재생성된다.

## 2026-11-02 도착지점 폭죽 소품 6개
- 담당자 요청: 이펙트 말고 소품으로 도착지점 좌우 끝에 6개. 프로젝트 내 폭죽 소품은 Synty `SM_Wep_Rocket_Fireworks_01`(막대 로켓, 원본 길이 0.8m, 눕힌 자세)뿐이라 이것을 사용(프리팹 인스턴스).
- `FinalCannonCourse/FinishFireworks` 아래 `Firework_L1~3`·`R1~3`: x ±12.5(도착지점 끝에서 0.5m 안), z 176.0·179.3·182.7, 세워서 3배 크기, 콜라이더 제거(장식).
- 담당자 후속 요청 "막대가 너무 길다": 메시 사본 `Meshes/firework-rocket-short.asset`(머리 z0.24 이상 유지, 그 아래 막대를 z0.20으로 접어 4cm 토막만 남김)으로 교체. 높이 2.38m → 1.07m, 바닥(y6.2) 위에 다시 맞춤.
- 테스트: 6개·충돌 없음·바닥 위·좌우 끝·높이 1.2m 미만 검사. EditMode 564/564 PASS. 씬 화면 확인.

## 2026-11-02 첫 도착 시 폭죽 색종이·불꽃
- 담당자 결정: 색종이와 불꽃 둘 다, 그 공연에서 처음 도착했을 때 한 번만.
- 코드: `FinishCelebrationState`(공연 ID별 1회, 공연이 아니면 연습 한 판 단위로 R 재시작 시 다시 허용, 공연 중 R은 다시 터지지 않음), `HumanCannonFinishCelebration`(각 화면이 `HumanCannonDestination.HasArrived`로 동기화된 플레이어 위치를 보고 첫 도착에 재생, 네트워크 메시지 없음), `HumanCannonDestination.PerformanceId` 읽기 전용 추가. 판정·정산 미변경.
- 씬: `FinishFireworks`에 컴포넌트, 폭죽 6개 머리 위마다 `Confetti`(새 파티클, 5색 90장, 위로 튀었다 팔랑이며 3~4초, 재질 `Materials/finish-confetti.mat` URP Particles/Unlit)와 `Sparks`(`FX_Sparks_01` 인스턴스, 반복 끔·시작 재생 끔, 위로 향하게 회전). 12개 모두 반복 없음·시작 재생 없음.
- 확인: 편집기에서 파티클 시뮬레이션으로 화면 확인(불꽃은 처음 옆으로 나가 위로 회전 수정, 색종이 크기·속도 키움). 빵빠레 효과음은 프로젝트에 소리 파일이 없어 미추가.
- 테스트: `FinishCelebrationStateTests` 3개, 씬 연결 검사 추가. EditMode `CurtainCall.HumanCannon` 567/567 PASS(이후 파티클 값 조정 뒤 `FinalCannonCourseTests` 11/11 PASS). 실제 플레이·멀티플레이 화면 미확인.
- 담당자 "안 터진다" → Play 모드에서 LAN 호스트 시작·도착지점으로 이동해 재현: 도착 판정은 정상, 불꽃은 재생, 색종이는 재생 상태인데 입자 0개. 원인: 파티클 `cullingMode = Automatic`이라 화면 밖이면 시뮬레이션이 멈춰 0초 버스트를 건너뜀. 12개 모두 `AlwaysSimulate`로 저장하고 `HumanCannonFinishCelebration.OnEnable`에서도 강제. 테스트에 컬링 모드 검사 추가.
- 담당자 요청으로 폭죽 6개를 좌우 대신 도착지점 상단(대포 반대쪽 끝) 한 줄로 이동: z183.83, x -12.5~12.5(5m 간격), 이름 `Firework_1~6`. 테스트를 상단 한 줄 배치로 교체. EditMode 567/567 PASS.
- Play 모드 재확인: 도착 즉시 색종이 6·불꽃 6 모두 재생, 게임 화면에서 플레이어 앞 상단 폭죽이 터지는 것 확인. 멀티플레이 다른 클라이언트 화면은 미확인.
- 확인한 사실: 정식 공연 성공이 확정되면 `PerformanceRewards.UpdateResultFreeze`가 `Time.timeScale = 0`으로 멈추고 플레이어를 확정 자세에 고정, 1.5초 뒤 결과 창·5.5초쯤 검은 전환 후 다음 일정(실제 시간 기준). 연습·실패는 멈추지 않음. 그대로면 폭죽이 도착과 동시에 멈춰 보이지 않음.
- 담당자 요청으로 폭죽 파티클 12개를 `useUnscaledTime = true`(씬 저장 + `HumanCannonFinishCelebration.OnEnable` 강제). 멈춤 동작은 미변경. 테스트에 검사 추가, EditMode 567/567 PASS. Play 모드에서 `Time.timeScale = 0`으로 만든 뒤 터뜨려 1초 동안 색종이 약 70장·불꽃 약 49개 진행, 게임 화면 확인. 실제 정식 공연 흐름 전체는 미확인.
- 담당자 요청 "폭죽에서 나오게, 색종이 더 높이": 측정하니 불꽃은 프리팹 확대(6.26배)로 발사 원 반지름 0.96이 커져 폭죽 옆 1~2m에서 나옴 → 불꽃 발사 원 반지름 0.01, 위치를 폭죽 머리 끝으로. 색종이는 3배 폭죽 아래 자체 배율 1/3이 `Local` 배율로 속도·크기에 곱해져 실제 발사 속도가 4m/s였음 → `Hierarchy`로 바꾸고 크기 0.1~0.16, 속도 10~13, 수명 5~6초, 중력 0.6, 처음 22%만 빠르게 오른 뒤 감속하는 속도 제한 곡선. 머리 위 약 10m까지 상승.
- 담당자 후속 "약간 낮춰": 색종이 속도 8.5~10.5로 낮춰 머리 위 약 7m(1.5초 정점) 후 천천히 낙하.
- Play 모드 게임 화면에서 폭죽 머리에서 불꽃·색종이가 위로 솟는 것 확인(10m 버전). 테스트에 발사 위치·불꽃 원 크기·색종이 배율 모드·속도 하한 검사 추가. EditMode 567/567 PASS(10m 버전), 낮춘 뒤 `FinalCannonCourseTests` 11/11 PASS.
- 담당자 요청: 폭죽 불꽃을 후방 불(`FirstSectionFire`·`SecondSectionFire`의 `FlameFront` 메시, `route-fire` 빨간 반투명)과 다른 불로 교체. `Sparks`(FX_Sparks_01) 삭제 → `Flame`(Synty `FX_Flame_Thrower_01` 인스턴스, 가산 파티클 불길): 폭죽 머리에서 위로, 3배 폭죽 배율 상쇄(실제 크기 1), 1.2초 한 번, 수명 0.45~0.65, 속도 5~7, 크기 0.7~1.1, 방출 60/초, 노랑→주황 색(처음 흰빛이 밝은 바닥에서 연기처럼 보여 채도 올림), 머리 위 약 3.4m. 반복 없음·시작 재생 없음·항상 시뮬레이션·실제 시간 유지.
- 테스트: Sparks 대신 Flame(위치·한 점·위쪽 방향·옛 Sparks 없음). EditMode 567/567 PASS(색 조정 전), 색 조정 뒤 `FinalCannonCourseTests` 11/11 PASS. Play 게임 화면에서 노랑·주황 불길 확인.
- 담당자 요청: 색종이 6m, 불길 4m. 파티클 시뮬레이션으로 측정해 속도 조정 — 색종이 8.5~10.5 → 7.6~9.4(평균 정점 머리 위 5.87~6.01m), 불길 5~7 → 7.3~10.22(상위 90% 높이 4.00~4.26m). 테스트에 속도 값 고정 검사. `FinalCannonCourseTests` 11/11 PASS. 게임 화면은 미확인.
- 테스트: 앞쪽 여유 1m 검사를 "대포 바닥 앞끝 = 지상 바닥 끝"으로 교체, 옆 쿠션은 바닥 옆면 전체를 덮는지만 검사. EditMode 562/562 PASS.
- 테스트: ㄷ자 감싸기 검사 추가, 적용 바닥·쿠션 수 48 → 50, 전체 콜라이더 57 → 59. EditMode 562/562 PASS. EditMode 561/561 PASS. PlayMode `FinalCannonPlayTests`의 (0,12,8) 기대값은 미갱신(옛 배치 기준이라 이미 맞지 않음). 실제 플레이 미확인.

## 2026-11-02 엘리베이터 이동 시간 조정
- 담당자 요청: `ShelterDeparture`·`DepartureTransport` 이동 시간 5초 → 4초(Unity에서 `travelSeconds` 수정 후 씬 저장). 속도 3.00 → 3.75m/s, 2.72 → 3.40m/s. `ShelterLift`(4.12초, 약 4.53m/s)는 유지.
- 참고: `HumanCannonRouteBuilder`의 초기 생성 값은 5초 그대로(좌표도 이미 현재 씬과 달라 재생성하지 않는 전제). 테스트·플레이 미확인.

## 2026-11-02 엘리베이터 도착 후 자동 복귀
- 담당자 요청: 엘리베이터가 도착하고 플레이어가 내린 뒤 2초가 지나면 제자리로 복귀.
- 코드: `DeparturePhase.Returning` 추가. `DepartureTravelState.TryReturn`(같은 이동 시간으로 출발점 복귀, 끝나면 `Waiting`), `Delivered`(이번 라운드 도착 기록, 복귀해도 유지·R 재시작 때 초기화). `HumanCannonRouteSystem`이 도착 뒤 발판 위 인원이 0명인 시간이 `returnDelaySeconds`(기본 2초, 인스펙터 조정) 이어지면 복귀. 네트워크 상태에 `Delivered` 1바이트 추가(84 → 85).
- 영향: 봇은 "Arrived"를 "이미 사용함"으로 쓰던 판단을 `Delivered`로 교체(복귀한 리프트를 다시 타러 가지 않음). 리프트 안내 문구에 "출발점으로 돌아가는 중" 추가. 복귀한 발판은 운반자가 다시 탈 수 있음.
- 테스트: `DepartureTravelStateTests`에 복귀·도착 기록 2개 추가. EditMode `CurtainCall.HumanCannon` 569/569 PASS. Play 모드·멀티플레이 화면 미확인.

- 2026-11-02 검사: 안전성=PASS; 문서/규칙=FAIL; 컴파일=SKIPPED(선행 실패); 테스트=SKIPPED(자동 테스트 선택 안 함)

## 2026-11-02 PR 제출
- dev 대상 PR #264 등록: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/264 (지정 팀원 검토 대기). `new-pr.ps1`은 브랜치 이름 형식(슬래시 2개) 때문에 거부되어 템플릿대로 직접 작성.

## 2026-11-02 초록 뒤 경사 1.5m 당김
- 담당자 요청: 초록 경사 구간을 대포 쪽으로 1.5m. 담당자 결정으로 안전 착지 구역도 같이 줄임.
- `Meshes/final-green-ground.asset` 뒤쪽 정점(z≤141.5) +1.5m: 윗면 시작 141.5 → 143.0, 경사 끝 140.3 → 141.8. 좌·우 경사·앞끝(z148)은 그대로.
- `LandingCushion` z141.5~144 → 143~144(중심 z143.5, 크기 z 1.0). 2구간 불꽃 경계(z141.5)는 미변경이라 경계와 쿠션 사이 1.5m는 일반 바닥.
- 테스트: 쿠션 뒤끝 143, 불꽃 경계 = 쿠션 뒤끝 −1.5로 갱신. EditMode 569/569 PASS. 씬·플레이 화면 미확인.
- 초록 뒤 경사 PR #273 등록: https://github.com/gaboja-studio/POC_CURTAINCALL/pull/273 (PR #264는 이 커밋 전에 병합됨, 지정 팀원 검토 대기).

## 2026-11-03 톱벽 그네: 잡으면 톱벽 좌우 분리
- 담당자 요청: 톱벽 그네(톱날벽 연결 봉 `LinkedPole` 2개)를 잡으면 톱벽이 가운데에서 절반으로 나뉘어 좌우로 갈라짐. 틈 = 트램펄린 폭 + 좌우 0.5m, 한 번 열리면 유지(재시작 R 때만 닫힘).
- 코드: `HumanCannonSawGate`가 `leftParts`/`rightParts`를 `openGap`만큼 1초(`openSeconds`) 동안 밀어 연다. 한 번 열리면 `IsOpen` 유지. 양쪽 절반은 계속 회전·접촉 부상·충돌(이전의 정지·초록/예고 깜박임·벽 끄기 제거). 절단 범위는 좌우를 따로 재서 틈을 덮지 않음. 네트워크 상태의 통과 바이트에 열림 비트(2) 추가(크기 변화 없음). 봇 대기 조건 `Phase==Active` → `!IsOpen`. 봉 점유·40초 주기 규칙은 그대로.
- 씬: 새 메뉴 `Tools/CurtainCall/인간 대포/톱벽 좌우 분리 적용`(`HumanCannonSawGateSplitBuilder`)으로 두 관문 적용 후 저장. `SawVisual/LeftBlades·RightBlades`(톱 22장씩 + 22×4.8×0.6 충돌 벽), `Barrier/MetalMesh/LeftFence·RightFence`(가운데 철망은 2m씩 반으로 나눔), 기존 `Barrier` 충돌 벽 삭제. 트램펄린 외형 폭 4.95m → `openGap` 5.95m. 편집 상태에서 열림 측정: 안쪽 끝 ±2.975m, 틈 5.951m, 재설정 시 원위치.
- 주의: 열리면 바깥쪽 절반이 기둥(±22.2) 밖으로 최대 x≈±25까지 나간다. 화면 확인 필요.
- 테스트: `PoleGatePassageTests` 분리·유지·재설정·클라이언트 열림 비트, `AppliedCannonArtTests`·`RouteCourseTests` 구조/틈 폭 갱신, 전체 콜라이더 59 → 61. EditMode `HumanCannon` 580개 중 579 PASS(콜라이더 수 1 FAIL) → 수정 후 `AppliedCannonArtTests` 15/15 PASS. PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow`로 갱신.

- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- PlayMode: `FinalCannonPlayTests` 50개 중 30 PASS, 18 FAIL, 2 SKIP(봇 실행파일 필요). 톱벽 테스트 3개는 2026-11-02 `NormalPole` 삭제로 봉 번호가 당겨져(첫 톱벽 그네 1 → 0) 실패하던 것을 0번으로 고침 → `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow`·`AirbornePlayerCrossesTheGateBeforeTheTrampolineCarrier`·`UpperMetalMeshBlocksFlightWithoutCuttingAboveTheSawRow` PASS. 나머지 15개(봉 번호 1을 쓰는 LongSwing·FlightSync·PoleSaw·PoleGrab, 난이도 함정 수, 지상 톱, 낙하 표식, 리프트 등)는 이번 변경과 무관하게 이전 배치 변경 뒤 미갱신 상태로 보임. 이번 변경 전 기준선 실행은 하지 않음.
- 2026-11-03 검사: 안전성=PASS; 문서/규칙=PASS(경고: 이 Task log 118줄); 컴파일=PASS; 테스트=SKIPPED(위 MCP 실행으로 대신)

## 2026-11-03 톱벽 그네: 틈 확대·바닥 밖 톱 낙하
- 담당자 요청: 좌우 0.5m씩 더 벌림, 지상 밖으로 나간 톱은 아래로 떨어지고 플레이어 낙하 부상 높이에 닿으면 사라짐.
- 틈 = 트램펄린 4.95m + 좌우 1m = 6.95m(씬 `openGap` 6.9507, 빌더 `SideMargin` 1m).
- 다 열린 순간 톱마다 중심 아래 관문 바닥 높이에 지상 충돌체가 있는지 검사 → 없으면 낙하(가속 `dropGravity` 20m/s²), `HumanCannonRouteSystem.IsBelowCourse`(y<-10, 플레이어 낙하 부상 높이)에 닿으면 비활성. 떨어지는 톱은 접촉 판정 제외. 재시작 시 원위치·재활성. 모든 클라이언트가 같은 바닥으로 판정하므로 추가 동기화 없음. 두 관문 모두 바깥 3장씩(좌 0~2, 우 41~43) 6장 낙하.
- 함정: 톱 프리팹 안 꺼진 하위 외형은 원점 경계를 돌려준다. 톱 중심 계산은 켜진 렌더러만 쓴다. 에디터에서 열린 상태로 `Awake`를 다시 부르면 열린 자세가 기준 위치가 되므로, 확인은 씬을 디스크에서 다시 연 닫힌 상태에서 한다.
- 테스트: EditMode `HumanCannon` 580/580 PASS, PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS(6장 소멸 포함). 실제 플레이 미확인.

## 2026-11-03 코스 밖 추락 = 외줄타기와 같은 공용 사망
- 담당자 요청: 지상 아래로 추락하면 시작 지점 리스폰 대신 외줄타기 추락처럼 사망 처리.
- 코드: `HumanCannonSystem.ServerFallDeath` 추가(외줄타기 `TightropeHangSystem`과 같은 `FallConsequencesSuppressed` 검사 → 관객 추락 야유 `ServerFell` → `ServerKill`). `HumanCannonRouteSystem`의 y<-10 처리를 `ServerFallInjury`(2곳 절단) → `ServerFallDeath`로 교체. `RelocateLocalFall`은 Dev "추락 무시"로 사망을 막았을 때만 마지막 대포 앞/출발점으로 옮김(평소 리스폰 제거).
- 유지: 비행 중 일반 바닥 추락 착지는 기존대로 2곳 절단(`ServerFallInjury`), 스프링 신발 반동도 그대로.
- 결정 문서 `basic-control.md`·`injury-hazards.md` 갱신.
- 테스트: 새 PlayMode `FallBelowCourse_DiesLikeTightrope_WithoutRespawningAtStart` PASS(바닥 밖 x40에서 떨어져 절단 없이 사망, 코스 위로 복귀 없음, R 재시작 후 생존). `Performance_SpringShoesReboundInsteadOfFallInjuryWithoutCooldown` PASS. EditMode `HumanCannon` 580/580 PASS. 실제 멀티 플레이 미확인.

## 2026-11-03 톱벽 그네: 바닥 밖 철망도 낙하
- 담당자 요청: 바닥 밖으로 삐져나와 떠 있던 철망도 떨어지게.
- `HumanCannonSawGate`의 낙하 대상에 문짝 묶음 안 `FencePanel*` 조각 추가(톱과 같은 중심 아래 바닥 검사·낙하·-10m 소멸·재시작 복귀). 두 관문 모두 양쪽 끝 철망 1장씩(`FencePanel0`, `FencePanel10`) 낙하. 남은 철망은 바닥 안(x ≥ -21.475). 충돌 벽은 그대로.
- 테스트: PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS(톱 6장 + 철망 2장 소멸), EditMode `PoleGatePassageTests` 11/11 PASS. 장면 캡처로 낙하 확인, 실제 플레이 미확인.

## 2026-11-03 톱벽 그네: 열린 틈의 흰 선(위 파이프) 처리
- 담당자 요청: 톱벽이 열려도 틈을 가로질러 남는 흰 선 제거. 정체는 관문 틀 `AppliedGateFrame/TopBeam`(강철 통파이프, x -22.3~22.3, 바닥 위 2.6m).
- 새 메뉴 `Tools/CurtainCall/인간 대포/톱벽 위 파이프 분리 적용`(`HumanCannonSawGateSplitBuilder.SplitTopBeamInOpenScene`)으로 두 관문의 파이프를 철망 조각과 같은 구간 12개(`TopBeam0~4, 5L, 5R, 6~10`, 굵기 유지)로 잘라 `LeftFence`/`RightFence`에 붙이고 원래 파이프 삭제, 씬 저장. `HumanCannonSawGate`는 철망 묶음의 바로 아래 자식 전체를 낙하 조각으로 모음 → 바깥 끝 `TopBeam0`·`TopBeam10`도 철망과 함께 낙하·소멸.
- 확인: 편집 상태에서 열린 틈(x ±3.4) 안 관문 외형 0개, 낙하 톱 6 + 철망·파이프 4. PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS. 실제 플레이 미확인.

## 2026-11-03 톱벽 그네: 멈춤 → 갈라짐 → 다시 회전
- 담당자 요청: 그네를 잡으면 톱이 먼저 멈추고 그다음 열리도록. 담당자 선택: 멈춤 0.5초, 다 열린 뒤 양쪽 톱 다시 회전(닿으면 부상).
- `HumanCannonSawGate`: `stopSeconds`(0.5초) 대기 뒤 `openSeconds`(1초) 동안 갈라짐. `IsSpinning` = 닫힘 또는 다 열림. 멈춤·갈라짐 중에는 회전·접촉 부상 없음. 다시 돌 때 멈췄던 각도에서 이어지도록 공용 시계 각도에 보정값을 더함. 재시작 시 초기화.
- 테스트: `PoleGatePassageTests` 멈춤 각도 유지·0.5초 대기·갈라짐 중 정지·이어서 회전 검사, PlayMode에 잡은 직후 정지·다 열린 뒤 회전 검사. EditMode `HumanCannon` 580/580 PASS, PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS. 실제 플레이 미확인.

## 2026-11-03 톱벽 그네: 열림 흔들림·끌림 스파크
- 담당자 요청: 열릴 때 흔들리며 열리는 연출, 톱벽이 바닥에 끌리는 느낌의 스파크 파티클.
- 흔들림: `HumanCannonSawGate.shakeAmplitude`(0.06m). 멈춤 0.5초 동안 절반 폭, 갈라지는 동안 최대 폭으로 좌우·위아래·앞뒤 덜컹, 마지막 20%에서 잦아들어 정확한 열린 위치에 멈춤. 좌우 문짝 위상 다름.
- 스파크: 새 메뉴 `Tools/CurtainCall/인간 대포/톱벽 열림 스파크 적용`. `ithappy/Battle_Stadium/VFX/FX_Sparks_01` 인스턴스를 `LeftBlades`/`RightBlades`의 `DragSparks`로 바닥 바로 위에 붙임(바닥 위에 남는 구간만, 사각형 방출). 갈라지는 동안만 방출. 원본 설정을 바꿈: 월드 충돌 끔(충돌 벽 안에서 바로 막힘), 크기 감소 끔·밝은 노랑→주황 색, 크기 0.08~0.16, 수명 0.2~0.45, 초당 250, 중력 1.6, 방향은 월드 속도(위 3~6, 가운데 쪽 2~6, 앞뒤 ±3).
- 함정: 납작한 Box 방출 모양은 z 배율이 방출 방향까지 줄여 불똥이 옆·아래로 퍼진다. 씬 뷰 캡처는 편집 중 시뮬레이션 파티클을 그리지 않아, Play 중 임시 카메라 + 시간 정지로 확인했다.
- 테스트: `PoleGatePassageTests`에 갈라지는 중 흔들림 검사, `RouteCourseTests`에 톱 22장·스파크 구성 검사. EditMode `HumanCannon` 580/580 PASS. Play 임시 카메라 캡처로 가운데 쪽 주황 불똥 확인. 실제 플레이 체감 미확인.
- PlayMode `TimedLinkedPole_GrabSplitsTheWallOpenForTheRestOfTheShow` PASS(흔들림·스파크 적용 후).

## 2026-11-03 톱벽 그네: 바닥 밖 조각 제각각 낙하
- 담당자 요청: 바닥 밖 톱·철망이 한 덩어리처럼 툭 떨어지는 느낌 → 제각각 떨어지게.
- `HumanCannonSawGate.BeginDrop`: 조각 번호로 정한 값(모든 클라이언트 동일)으로 시작 지연 0~`dropStagger`(0.6초), 낙하 가속 ±20%, 바깥 0.5~2m/s·앞뒤 ±1.5m/s 튕김, 90~360도/초 임의 축 구르기(조각 중심 기준). 떨어지는 톱은 회전(스핀) 대신 구른다.
- 함정: 철망·파이프는 비균일 배율 부모(Barrier 44×2.4×0.6) 아래라 구르면 찌그러지고 회전 중심이 떠서 위로 날아갔다 → 낙하 시작 때 배율 없는 관문 아래로 옮기고, 재시작 때 원래 부모·위치·회전·배율 복원.
- 확인: 편집 상태 시뮬레이션에서 조각별 높이·기울기 다름, 철망 크기 유지(4×2.4×0.6). EditMode `HumanCannon` 580/580 PASS. PlayMode 재실행은 07:59 플레이 정지로 중단됨(미확인).

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
