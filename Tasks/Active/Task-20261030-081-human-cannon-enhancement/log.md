# Log

## 2026-10-30 작업 준비
- 현재 브랜치에 Task 생성. 담당자 @lakuase2, 공유 대상 dev. 다음: 담당자가 현재 구현을 조사하고 상세 범위를 정한다.
- `doctor.ps1 -SkipEditor`: FAIL. 외부 에셋 submodule 미초기화, 생성 전 현재 브랜치 Task 부재, 기존 Task ID 077·078·079 중복. Unity CLI 버전 차이, skill links 부재, gh 미로그인 경고. 문서 준비에는 필수적인 차단은 없으나 Unity 작업 시 외부 에셋 준비 상태를 확인해야 한다.
- `check-work.ps1` 실행을 시작했으나 결과를 회수하지 못해 완료 여부 미확인. Unity 검사·자동 테스트는 이번 문서 준비에서 확인하지 않았다.

## 2026-10-30 고도화 항목 수령
- @lakuase2가 항목 8개와 우선순위를 전달. todo에 우선순위별로 정리하고 Unity 에디터에서 씬 계층·이동 장치 코드를 조사해 진입점을 적었다.
- 확인한 사실: 좌측 이동 장치(DepartureTransport)는 이미 1인 탑승 대기 후 자동 출발하며 과승 시 바닥이 열린다. 요청은 과승 시 작동하지 않는 방식으로 바뀌는 것이다. 도착지점 그네는 `CeilingPole1_4`(z=174)다. 낙하 위치 그림자는 아직 구현이 없다.
- 우선순위는 작업 순서가 아니라고 담당자가 확인. 다음: 담당자가 지정한 항목부터 착수. 수평톱 공통 에셋은 외줄타기 Task-074와 겹치므로 수정 전 알린다.

## 2026-10-30 톱 외형 교체·판정 비교
- 인간대포 톱 외형을 `TightropeArtKit/HorizontalSaw.prefab` 인스턴스로 교체. 대상: `GeneralHazards044/GroundSaw*/Visual`(12개), 두 `SawGate/SawVisual/GateSawDisc*`(88슬롯). 기존 `AppliedSaw`·`AppliedGateSaw`(Synty 톱 메시 + Steel)는 삭제. 해저드·관문·슬롯 위치는 바꾸지 않았다.
- 판정 유지 방법: 지상 톱은 기존 규칙(외형 = 판정, 반지름 0.65·두께 0.24)에 맞춰 비균등 확대. 관문은 렌더러 bounds가 판정이므로 얇은 날 2장을 슬롯 앞뒤(±0.25)에 겹쳐 기존 0.6m 두께·43.6m 폭을 유지. 관문 날의 Sparks 파티클은 판정 bounds와 성능 때문에 끔. 지상 톱은 Sparks 유지.
- 판정 비교: 외줄타기 가로/정면 톱 판정 지름 1.0·두께 0.1, 외형 1.2(외형이 0.1m 더 큼). 인간대포 지상 톱 판정 지름 1.3·두께 0.24·외형 = 판정, 중심 높이 0.3(윗면 0.42, 외줄 가로 톱 윗면 0.2). 관문은 원판 판정이 아니라 톱 렌더러 AABB와 몸 상자(+0.15 여유) 겹침.
- 검사: Unity EditMode `HumanCannon` 필터 495/495 PASS. 첫 시도는 외형이 판정과 달라 4건 FAIL → 위 방식으로 수정 후 통과. PlayMode 미실행. 실제 플레이 외형은 사람 확인 필요.
- 담당자 결정: 지상 톱 판정 크기는 외줄타기와 맞추지 않고 현재 값(지름 1.3·두께 0.24)을 유지한다.
- 톱 외곽선이 멀리서 검게 뭉쳐 보이는 문제: `HumanCannonOutline`은 거리와 무관한 2px 고정 외곽선이라 작은 톱니를 덮었다(외줄타기는 5→14m 선형 감쇠). `HumanCannonOutline`에 선택형 거리 감쇠(`fadeWithDistance`, 기본 꺼짐)를 추가하고 지상 톱 12·관문 톱벽 2에만 켰다. 봉·트램폴린은 기존 그대로.
- 검사: EditMode `HumanCannon` 필터 500/500 PASS(감쇠 테스트 5건 추가). 플레이 화면에서의 외곽선은 아직 사람 확인 전.
- 관문 톱 재배치(담당자 요청): 쌍 배치(틈 0.21/0.40 교대)를 없애고 88장을 틈 0.3m 등간격으로 배치(날 두께 0.194, 간격 0.494). 슬롯 44개 구조는 유지. 톱벽 전체 폭은 43.6→43.19m로 양 끝이 0.2m씩 줄어 관문 판정 범위도 그만큼 줄었다. EditMode `HumanCannon` 500/500 PASS.
- 관문 톱벽 눈부심(담당자 요청): 외줄타기 공용 `HorizontalSaw.mat`(금속 0.85·광택 맵·반사 켜짐)은 두고, 사본 `HumanCannon/Materials/human-cannon-gate-saw-matte.mat`(광택 맵 제거, 금속 0.2, 매끄러움 0.1, 하이라이트·환경 반사 끔)을 만들어 두 관문 날 176개 렌더러에만 적용. 지상 톱은 기존 재질 유지.
- 1구간 첫 톱벽 앞 지상 톱 3개 재배치(담당자 스케치): `HumanCannonHazard.authoredPath` 추가(공용 30° 대각선·밀도 미적용, 항상 켜짐). 1AExtra z41 왼45.6→오36.4, 1D z32.5 거의 수평, 1C z23.5 왼29→오18. 관련 테스트(30° 고정, 밀도 개수) 갱신.
- 미해결: 1C 레일이 좌측 이동 장치 도착 발판(x-12~-6, z18~26) 위 z≈25.6을 지나 도착 직후 플레이어를 벨 수 있다. `HumanCannonWideCourseTests.GroundSaws_HaveDistinctRails...`(도착 발판 앞 2m 확보) FAIL. 담당자에게 조정안 확인 중.
- 담당자 결정으로 10° 무교차 배치로 확정: 1C z28(왼32.2→오23.8), 1D z34 수평, 1AExtra z40.1(왼44.3→오35.9). 도착 발판 앞 2m·톱벽 여유 확보. 15°는 톱벽(z47)을 넘어 불가. 검사: EditMode `HumanCannon` 499/500 후 테스트 수정분 재컴파일, `HumanCannonWideCourseTests` 6/6 PASS.
- 좌측 이동 장치 도착 위치(담당자 요청): `DepartureTransport/Destination` z 22→20. 발판 뒤쪽 끝(z16)이 `Floor_0_0` 뒤쪽 끝과 맞는다. 발판 앞(z24~26, x-12~-6)에 2m 빈칸이 생겨 `Floor_1_2`(z26)까지 비어 있다. EditMode `HumanCannon` 500/500 PASS.
- 도착 발판 앞 빈칸 메움: `Floor_1_2`를 z26~35 → z24~35로 늘림(콘크리트 외형은 루트 크기를 따라감). 레이캐스트로 빈칸 없음 확인.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 시작 바닥 확장(담당자 요청, 계단 덮어도 됨): `Ground/Floor_0_0`·`Floor_0_1` 바깥쪽을 x -15 → -23(계단 바깥 끝)으로 늘림. 높이 3.5~4, z -11~7 그대로. EditMode `HumanCannon` 500/500 PASS.
- 주의: 아래 `ReturnStairs`(구덩이 복귀 계단)는 남아 있으나 위가 바닥 판(아래면 y3.5)으로 막혀, 계단 윗부분(디딤 높이 약 2m 이상, z≈-7 이후)에서 머리가 걸려 시작 바닥으로 올라올 수 없다. 좌측 이동 장치 과승 시 바닥 개방(구덩이 낙하)이 남아 있는 동안 복귀 경로가 없다. 과승 시 미작동으로 바꾸는 (1) 항목과 함께 정리 필요.
- 시작 바닥 폭을 1구간 바닥(x -22~22)에 맞춤(담당자 재요청): `Floor_0_0`·`Floor_0_1` 왼쪽 -23→-22, `Floor_2_0`·`Floor_2_1` 오른쪽 15→22. 계단 위 x -23~-22에 1m 틈이 다시 열림. EditMode `HumanCannon` 결과는 아래.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 시작 지점 아래 구간·계단 삭제(담당자 요청): `SplitRouteCourse/LiftSafetyLesson` 전체(RecoveryFloor·StairBottom·StairTop·ReturnStairs 40칸·RecoveryDirection) 삭제. 씬 내 다른 참조 없음. 과승 낙하자는 y -10 아래에서 `HumanCannonRouteSystem` 복귀 처리. 테스트 갱신: 낙하 공간을 fallHeight까지 검사, 안전 바닥·계단 테스트를 "아래 구간 없음" 테스트로 교체, 아트 바닥 수 81→38. `HumanCannonLiftTrapBuilder`(에디터 도구)는 남아 있어 다시 실행하면 구간이 재생성된다.
- 검사: 콜라이더 수 90→47 갱신 후 EditMode `HumanCannon` 결과는 다음 줄.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 엘리베이터 3대(DepartureTransport·ShelterLift·ShelterDeparture) 좌우·앞뒤 절반(담당자 요청, "일단"): 발판 6×8 → 3×4, halfSize (3,4)→(1.5,2). 중심·출발·도착 위치 유지.
- 영향(미해결): ① 모든 출발·도착 자리에서 기존 6×8 구멍이 남아 발판 둘레(좌우 1.5m·앞뒤 2m)가 빈칸. ② 트램폴린 적재 반경 2.48m > 발판 반폭 1.5m라 DepartureTransport는 트램폴린을 실을 수 없어 출발 조건(사람+트램폴린)을 만족하지 못함. ③ EditMode 498/500: `ShelterAccess_ConnectsFirstCourseToLowerLiftAndUpperLiftToDeck`, `ShelterDirectJump_CannotReachLowerLiftOrAccessEvenWithCapsuleAndSkinAllowance` FAIL(쉼터 리프트 접근로와 발판 사이 2m 틈).
- 축소 리프트 빈틈 메움(담당자 요청): 출발은 앞쪽 끝, 도착은 뒤쪽 끝을 구멍 가장자리에 붙이도록 이동 — DepartureTransport (-9,4,5)→(-9,0,18)[도착 뒤쪽 끝 z16 유지], ShelterDeparture (-9,6,82)→(-9,6,97), ShelterLift (-30.5,0,68)→(-23.5,6,78)[접근로 끝 z66·갑판 x-22에 붙임]. 남은 구멍은 이웃 바닥 복제 `ElevatorFill_*` 12장으로 메움(Ground·FirstCourseFloor·ShelterDeck·SecondCourseFloor 각 3장). 이동 방향 앞쪽(공중 구간)은 원래 설계대로 비어 있음. `HumanCannonGroundMarkers.courseFloors`에 새 코스 바닥 6개 추가. 테스트 갱신: 쉼터 리프트 좌표, 바닥 면 38→50, 콜라이더 47→59. EditMode `HumanCannon` 500/500 PASS. 트램폴린(반경 2.48)은 3×4 발판에 여전히 실을 수 없음.
- 정정: 이동 장치 출발 조건(`HumanCannonRouteSystem.CanStart`)은 탑승자가 트램폴린을 들고 있고 발이 발판 위에 있는지만 본다. 트램폴린 크기 검사(`HumanCannonTransport.ContainsLoad`)는 테스트에서만 쓰여 실제 출발을 막지 않는다. 앞선 '트램폴린을 실을 수 없어 출발 불가' 기록은 틀렸다.
- 이동 장치 3대 좌우 폭 3→5.5m(담당자 요청, 트램폴린 지름 약 4.95m 고려), 앞뒤 4m 유지. halfSize (2.75,2). 구멍(x-12~-6)은 유지하고 메움 바닥을 새 폭에 맞춤(옆 0.25m, 앞뒤 x-11.75~-6.25). ShelterLift는 +x 끝 고정으로 (-31.75,0,68)→(-24.75,6,78). 테스트 좌표 갱신. EditMode `HumanCannon` 500/500 PASS.
- 쉼터 리프트 경로 꺾기(담당자 요청): `HumanCannonTransport`에 선택 경유점 `via` 추가(두 구간 길이 비례, 같은 속도). ShelterLift/Via (-31.75,6,78): 정면 위 대각선으로 갑판 높이까지 오른 뒤 오른쪽(+x)으로 이동. 경로가 13.6→18.66m로 길어져 이동 시간 3.00→4.12초(속도 유지). 경로 상 걸리는 고정 물체 없음. 테스트에 경로 모양 검사 추가.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 좌측 이동 장치 위치 이동(담당자 요청): 1구간 바닥 왼쪽 끝 → 구멍 6m. 발판 중심 x -9 → -13.25(구멍 x -16~-10.5). 도착 쪽: FirstCourseFloor/Floor_0_0 x-22~-16 z16~20, Floor_0_1 x-22~-12 z20~26, ElevatorFill_Front x-12~-10.5 z20~24, ElevatorFill_Right x-10.5~-6 z16~24, ElevatorFill_Left 삭제. 출발 쪽: Ground/Floor_0_1 x-22~-16, ElevatorFill_Back x-16~-10.5 z-1~3, ElevatorFill_Right x-10.5~-6, ElevatorFill_Left 삭제. 지면 표식 목록 정리(20). 이동 경로 걸림 없음. 테스트 바닥 수 50→48, 콜라이더 59→57. EditMode `HumanCannon` 500/500 PASS.
- 1구간 지상 톱 3개를 3.5m 앞으로(담당자 요청: 첫 톱을 20m 거리선 z24.5에): 1C z24.5, 1D z30.5, 1AExtra z36.6. 간격·10° 각도 유지. 도착 발판 앞(z20)과 아래 톱 레일 사이 5.7m.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 1구간 지상 톱 재배치(담당자 요청): 2번 1D를 27m 거리선 z31.5, 3번 1AExtra를 z37.6(2번과 기존 간격 유지). 톱날 가장자리 최소 틈 1-2 1.47m, 2-3 0.57m, 3번-톱벽 4.22m. EditMode `HumanCannon` 500/500 PASS.
- 1번 톱(1C)만 z24.5→25.4: 1-2 틈을 2-3과 같은 0.57m로, 엘리베이터 도착 발판 앞과 6.6m. 2·3번 유지. EditMode 결과는 다음 줄.
- 검사: EditMode `HumanCannon` 500/500 PASS.
- 1번 톱(1C)을 18m 거리선 z22.5로(담당자 요청). 왼28.7→오18.3... 실제 왼26.73→오18.27, 1-2 틈 3.47m, 엘리베이터 도착 발판 앞과 3.70m(중심-발판 앞 2.5m, 규칙 2m 이상 충족). EditMode `HumanCannon` 500/500 PASS.
- 3번 톱(1AExtra) z37.6→39.6(담당자 요청: 톱벽 쪽으로 조금 더). 2-3 틈 2.57m, 톱벽과 2.22m.
- 검사: EditMode `HumanCannon` 500/500 PASS.

- 2026-10-30 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함)
- 커밋 전 검사: `check-work.ps1` 컴파일 PASS, 형식/규칙 PASS, 연결 PASS, 테스트 SKIPPED(스크립트 기본값). 별도로 EditMode `HumanCannon` 500/500 PASS. `ProjectSettings/Packages/com.unity.services.vivox/Settings.json` 자동 변경은 커밋에서 제외.
- dev 동기화(담당자 요청): origin/dev(24f004bd) 병합. `HumanCannon.unity` 충돌 3곳은 모두 우리 쪽 옛 톱 외형 삭제와 dev(Task-082, #241) 새 모델 프리팹 인스턴스 추가가 맞닿은 것 — 삭제한 옛 문서 6개는 버리고 dev 추가 문서 6개는 유지, 씬 내부 참조 누락 0. `Assets/_ThirdParty` 포인터는 dev 값(a1511645)으로 갱신. 병합 후 씬 확인: 새 대포·트램폴린 외형과 이번 변경 모두 유지, 누락 스크립트·프리팹 0. EditMode `HumanCannon` 501/501 PASS. 주의: dev의 새 규칙(동기화 전 Unity 종료 확인)을 지키지 못하고 에디터가 켜진 채 병합했다(씬 미저장 변경 없음 확인 후 재열기).

## 2026-11-02 좌측 이동 장치 과승 처리 변경
- 브랜치: `feat/human-cannon/enhancement`가 dev에 이미 병합되어 고유 커밋이 없어 dev(=origin/dev 7259e8f4)로 빨리감기 후 전환. 파일 변경 없음.
- 담당자 결정: 2명 이상이면 바닥을 열지 않고 출발만 막는다. 1명+트램폴린이면 발판 초록, 2명 이상 또는 트램폴린 없음이면 발판 빨강.
- 구현: `HumanCannonTransport.overloadTrap`(기본 켜짐) 추가, `DepartureTransport`만 끔. 끈 장치는 `LiftTrapState`를 돌리지 않고, 대기 중 과승·운반자 아님을 `Transit.Rejected`로 계산해 호스트→클라이언트로 동기화(트랜짓당 83→84바이트). 발판 색 `RejectedColor`(빨강). ShelterLift·ShelterDeparture는 기존 함정 유지.
- 남은 점: 이동 중 다른 사람이 뛰어올라도 멈추거나 떨어뜨리지 않고 계속 이동한다(편승자는 따라 운반되지 않음). 툴팁 문구는 기존 그대로.
- 씬 저장 시 Unity가 다른 작업의 코드 필드 변화(`fireSeversLimbs`·`continuousSwingInput` 등 기본값 추가, `barrel*Seconds` 제거)도 함께 재직렬화했다.
- 테스트: 과승 낙하 공간 테스트를 `FirstLift_RefusesOverloadWithoutOpeningFloor`로 교체, 발판 색 테스트에 빨강 추가. EditMode `CurtainCall.HumanCannon` 559/559 PASS. PlayMode·실제 2인 플레이 미확인.
- 2026-11-02 검사: 안전성=PASS; 문서/규칙=PASS; 컴파일=PASS; 테스트=SKIPPED(자동 테스트 선택 안 함, EditMode는 위에서 별도 실행)

## 2026-11-02 코스 첫 공중 그네 삭제
- 담당자 요청 "1번 공중 그네 삭제" → 확인 결과 코스 첫 그네 `PoleGateCourse/NormalPole`(x-14, z34). 그네와 위 천장·줄(`ExtendedCourse044/ExistingCeiling0`: 들보·고정쇠 2·줄 2) 삭제. `HumanCannonPoleSystem.poles`에서 제거해 7→6개, 이후 인덱스가 1씩 당겨짐(LinkedPole이 0번).
- 테스트 갱신: 봉 개수 7→6, 휴식 봉 기대 위치에서 z34 제거, 천장 잔여물 없음 검사 추가. EditMode `CurtainCall.HumanCannon` 559/559 PASS.
- 남은 점: PlayMode `FinalCannonPlayTests`의 그네 번호(`GrabEnhancementPole(1)`, `Pole 6`=AddedPole0)는 이미 옛 10개 배치 기준이라 이번 삭제와 무관하게 맞지 않는다. 에디터 빌더(`HumanCannonCourseExtensionBuilder`·`PoleSwingVisualBuilder`·`CourseTuningBuilder`)는 `ExistingCeiling0~3` 인덱스를 가정하므로 다시 실행하지 않는다.
- 담당자가 씬에서 선택한 `PoleGateCourse/GateConnectionCable`(1구간 연결 그네~톱벽 사이 장식용 LineRenderer 줄) 삭제. 부모 외 참조·런타임 코드·테스트 사용 없음. 씬 저장.

## 2026-11-02 도착지점 공중 그네 삭제
- `ExtendedCourse044/CeilingPole1_4`(x-4, z174, FlightLandingPad 위)와 천장·줄 `CeilingPole1_4Ceiling` 삭제. `HumanCannonPoleSystem.poles` 6→5개(LinkedPole·NormalPole·LinkedPole·AddedPole0·AddedPole2).
- `HumanCannonBotBrain`의 z174 목표는 착지대 좌표라 그네와 무관, 유지.
- 테스트 갱신: 봉 5개·휴식 봉 3개, 기대 위치에서 z174 제거, 그네·천장 잔여 없음 검사. EditMode `CurtainCall.HumanCannon` 559/559 PASS. 실제 플레이 미확인.

## 2026-11-02 피날레 탄도 링 12개
- 담당자 요청: 도착지점 직전 최종 대포 4대(x -6·-2·2·6, z152)의 무조작 탄도 위에 링. 대포마다 상승·정점·하강 3개, 총 12개("링 4개, 총 12개"는 4대×3개로 해석).
- 탄도: 포구(y8.2, z153.5), 속도 (0,12,8), 중력 9.81. 정점 1.223초 후 발 기준 y15.54·z163.29. 링 시간 = 정점 시간 × 0.5·1·1.5 → z158.39·163.29·168.18.
- 링 중심은 몸통 중심 경로(발 기준 탄도 + 캡슐 높이 절반 0.75). 링 면은 그 지점 진행 방향을 마주봄(상승·하강 ±36.9°). 링 반지름 1.3(안쪽 1.2), 관 굵기 0.1, 충돌 없음, 그림자 끔. 대포 간격 4m라 이웃 링과 1.4m 떨어짐. 겹치는 콜라이더·렌더러 없음.
- 생성: `HumanCannonFinaleRingBuilder`(메뉴 `Tools/CurtainCall/인간 대포/피날레 탄도 링 배치`), 메시 `Meshes/finale-ring.asset`, 재질 `Materials/finale-ring.mat`(금색 URP Lit). 씬 `FinalCannonCourse/FinaleRings`.
- 테스트: `FinaleRings_CenterEachUnsteeredFlightAtRiseApexAndFall` 추가. EditMode `CurtainCall.HumanCannon` 560/560 PASS. 링 통과 판정·점수 등 게임 효과는 없음(장식). 실제 플레이 화면 미확인.
- 링 통과 효과는 착수 직후 담당자 요청으로 중단(변경 없음). 방식은 담당자가 다시 정한다.

## 2026-11-02 최종 대포 4대 앞으로 당김
- 담당자 요청: 초록 발판(`LandingCushion`, z141.5~146.5)과 대포 사이 약 1.5m. 대포 뒤끝(바퀴, 렌더러 기준) z150.24 → 148.00, 대포 위치 z152 → 149.76(−2.24). 포구 z153.5 → 151.26. 착지 예상 z174.3 → 172.1(착지대 z169.3~179.3 안).
- 바닥 번호 `CannonLabel1~4`(4.2m)는 그대로 옮기면 쿠션에 걸쳐 0.654배로 줄여 z146.6~149.41(갑판 안, 기존처럼 대포 아래 1.41m 겹침).
- 피날레 링 12개는 빌더로 다시 만들어 새 탄도에 맞춤(z156.15·161.05·165.94).
- 테스트: 대포 위치 기대값 z149.76, 쿠션-대포 간격 1.5m 검사 추가. EditMode 559/560 — `ThreeRestPoles_...` FAIL: `AddedPole2`(x12, z136)와 FinalCannon4 거리 16.62m < 규칙 17m(Task-044 휴식 봉은 대포에서 17m 초과). 담당자에게 처리 방법 확인 중.

## 2026-11-02 도착지점에 가장 가까운 그네 삭제
- 담당자 요청: 도착지점에서 가장 가까운 공중 그네와 줄 삭제 → `HumanCannonEnhancement/AddedPole2`(x12, z136, 착지대까지 40.8m)와 천장·줄 `AddedPole2Ceiling` 삭제. 남은 그네 4개(1구간 LinkedPole, 2구간 NormalPole·LinkedPole, AddedPole0). 위 17m 규칙 실패는 이 그네가 사라져 해소.
- 테스트: 봉 4개·휴식 봉 2개, 잔여 없음 검사. EditMode `CurtainCall.HumanCannon` 560/560 PASS.

## 2026-11-02 최종 대포 앞 갑판 축소
- 최종 대포 4대에는 콜라이더가 없고 `PreparationDeck` BoxCollider(z146.5~156.5) 위에 놓여 있다. 지면에 닿는 부분은 보이는 `Chassis`(바닥 y6.2)로 앞끝 z150.63, 포신은 공중에서 z151.64까지. 숨겨진 옛 모델(cannon_001/002, Barrel 사본)은 제외.
- 담당자 요청: 발사 방향 쪽 여유를 1m로 → 갑판 앞끝 156.5 → 151.63(뒤끝 146.5 쿠션 연결 유지). 갑판 크기 z 10 → 5.13, 중심 z151.5 → 149.065. 콘크리트 외형도 같이 줄어듦. 잘린 구간 위 다른 물체 없음, 탑승 위치 4곳 모두 갑판 위.
- 테스트: 앞쪽 여유 1m 검사 추가. EditMode `CurtainCall.HumanCannon` 560/560 PASS. 실제 화면 미확인.
- 대포 앞 바닥의 흰 숫자 `FinalCannonCourse/CannonLabel1~4`(TextMesh) 삭제(담당자 요청). 참조 없음. `HumanCannonFinalCannonBuilder`는 다시 실행하면 숫자를 만든다. 삭제 확인 테스트 추가, EditMode 561/561 PASS.

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
